// These are the necessary using statements.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Messages;

namespace GetIoTDataForSpace
{
	[GQIMetaData(Name = "Parking Space Detail Data Source")]
	public sealed class ParkingSpaceDetailDataSource : IGQIDataSource, IGQIOnInit, IGQIInputArguments, IGQIOnPrepareFetch
	{
		private GQIDMS _dms;
		private string _spaceId;
		private string _spaceName;
		private List<GQIRow> _detailRows;
		private IGQILogger _logger;

		private readonly Dictionary<string, (int DmaID, int ElementID, int TablePID)> _sourceMap = new Dictionary<string, (int, int, int)>(StringComparer.OrdinalIgnoreCase)
		{
            // Keyword     DMA ID   Element ID   Table PID
            { "SCC",       (1004671,      565,        500) },
			{ "Kotromanic", (1004671,      566,        500) },
			{ "Kampus",    (1004671,      567,        500) },
		};

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			return default;
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				new GQIStringArgument("Space ID") { IsRequired = true }, 
                new GQIStringArgument("Space Name") { IsRequired = true }, 
            };
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_spaceId = args.GetArgumentValue<string>("Space ID");
			_spaceName = args.GetArgumentValue<string>("Space Name");
			return default;
		}

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Property"), 
                new GQIStringColumn("Value"),    
            };
		}

		public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
		{
			_detailRows = new List<GQIRow>();
			try
			{
				var source = _sourceMap.FirstOrDefault(kvp => _spaceName.Contains(kvp.Key)).Value;
				if (source.DmaID == 0)
				{
					_logger.Warning($"Could not determine the source element for space name: '{_spaceName}'");
					_detailRows.Add(CreateErrorRow("Configuration Error", "Source element not found for this space."));
					return null;
				}

				var rawTableData = GqiGetPartialTable.GetTable(_dms, source.DmaID, source.ElementID, source.TablePID);
				if (rawTableData == null || !rawTableData.Any())
				{
					_logger.Warning($"IoT simulation table for element {source.DmaID}/{source.ElementID} is empty.");
					_detailRows.Add(CreateErrorRow("Data Error", "Source table is empty."));
					return null;
				}

				if (!rawTableData.TryGetValue(_spaceId, out object[] targetRow))
				{
					_logger.Warning($"Could not find row with key '{_spaceId}' in the table.");
					_detailRows.Add(CreateErrorRow("Data Error", $"Space ID '{_spaceId}' not found."));
					return null;
				}

				_detailRows.Add(CreateDetailRow("Name", targetRow, 1));

				string status = Convert.ToDouble(targetRow[4], CultureInfo.InvariantCulture) > 0.5 ? "Occupied" : "Free";
				_detailRows.Add(new GQIRow(new GQICell[] { new GQICell { Value = "Status" }, new GQICell { Value = status } }));


				_logger.Information($"Successfully created detail view for space '{_spaceId}'.");
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to generate space details.");
				_detailRows.Add(CreateErrorRow("Script Error", ex.Message));
			}
			return null;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return new GQIPage(_detailRows.ToArray()) { HasNextPage = false };
		}

		private GQIRow CreateDetailRow(string propertyName, object[] sourceRow, int columnIndex)
		{
			var value = (columnIndex < sourceRow.Length) ? Convert.ToString(sourceRow[columnIndex]) : "N/A";
			return new GQIRow(new GQICell[]
			{
				new GQICell { Value = propertyName },
				new GQICell { Value = value },
			});
		}

		private GQIRow CreateErrorRow(string property, string value)
		{
			return new GQIRow(new GQICell[]
			{
				new GQICell { Value = property },
				new GQICell { Value = value },
			});
		}
	}

	public static class GqiGetPartialTable
	{
		public static IDictionary<string, object[]> GetTable(GQIDMS gqiDms, int dmaId, int elementId, int tableId, int keyColumnIndex = 0)
		{
			var message = new GetPartialTableMessage(dmaId, elementId, tableId, new[] { "forceFullTable=true" });
			var response = (ParameterChangeEventMessage)gqiDms.SendMessage(message);

			if (response == null)
			{
				throw new InvalidOperationException("Failed to retrieve table data. Response is null.");
			}

			return BuildDictionary(response, keyColumnIndex);
		}

		private static IDictionary<string, object[]> BuildDictionary(ParameterChangeEventMessage response, int keyColumnIndex)
		{
			if (response == null) throw new ArgumentNullException(nameof(response));

			var result = new Dictionary<string, object[]>();
			if (response.NewValue == null || response.NewValue.ArrayValue == null) return result;

			ParameterValue[] columns = response.NewValue.ArrayValue;
			if (columns.Length == 0) return result;
			if (keyColumnIndex >= columns.Length) throw new ArgumentException("Invalid key column index.", nameof(keyColumnIndex));

			if (columns[keyColumnIndex].ArrayValue == null) return result;

			string[] keyMap = new string[columns[keyColumnIndex].ArrayValue.Length];
			int rowNumber = 0;
			foreach (ParameterValue keyCell in columns[keyColumnIndex].ArrayValue)
			{
				string primaryKey = Convert.ToString(keyCell.CellValue.InteropValue, CultureInfo.CurrentCulture);
				if (primaryKey == null) continue;

				result[primaryKey] = new object[columns.Length];
				keyMap[rowNumber] = primaryKey;
				rowNumber++;
			}

			int columnNumber = 0;
			foreach (ParameterValue column in columns)
			{
				rowNumber = 0;
				if (column.ArrayValue != null)
				{
					foreach (ParameterValue cell in column.ArrayValue)
					{
						if (rowNumber < keyMap.Length && keyMap[rowNumber] != null)
						{
							result[keyMap[rowNumber]][columnNumber] = cell.CellValue.ValueType == ParameterValueType.Empty ? null : cell.CellValue.InteropValue;
						}
						rowNumber++;
					}
				}
				columnNumber++;
			}

			return result;
		}
	}
}