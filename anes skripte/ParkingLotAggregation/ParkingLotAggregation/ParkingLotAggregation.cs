using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Messages;

namespace Pivoted_Parking_Metrics
{
	[GQIMetaData(Name = "Pivoted Parking Metrics")]
	public sealed class PivotedParkingMetricsDataSource : IGQIDataSource, IGQIOnInit, IGQIInputArguments, IGQIOnPrepareFetch
	{
		private GQIDMS _dms;
		private int _dmaId;
		private int _elementId;
		private int _tableId;
		private OverallMetrics _metrics; 
		private IGQILogger _logger;

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
				new GQIStringArgument("Element ID") { IsRequired = true },
				new GQIIntArgument("Table PID") { IsRequired = true },
			};
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			var elementFullId = args.GetArgumentValue<string>("Element ID");
			var saElement = elementFullId.Split('/');
			_dmaId = Convert.ToInt32(saElement[0]);
			_elementId = Convert.ToInt32(saElement[1]);
			_tableId = args.GetArgumentValue<int>("Table PID");
			return default;
		}

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Name"),  
                new GQIDoubleColumn("Value"), 
            };
		}

		public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
		{
			_metrics = new OverallMetrics(); 
			try
			{
				var rawTableData = GqiGetPartialTable.GetTable(_dms, _dmaId, _elementId, _tableId);
				if (rawTableData == null || !rawTableData.Any())
				{
					_logger.Warning("IoT simulation table is empty or could not be read.");
					return null;
				}

				var allRows = rawTableData.Values.ToList();

				const int numericValueColumnIndex = 4;

				int capacity = allRows.Count();
				int occupiedCount = allRows.Count(row => Convert.ToDouble(row[numericValueColumnIndex]) > 0.5);
				double occupancyPercentage = (capacity > 0) ? Math.Round(((double)occupiedCount / capacity) * 100.0, 2) : 0;

				_metrics.Capacity = capacity;
				_metrics.CurrentOccupancy = occupiedCount;

				_logger.Information($"Aggregated metrics. Capacity: {capacity}, Occupied: {occupiedCount}");
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to aggregate overall parking metrics.");
			}
			return null;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			var capacityRow = new GQIRow(new GQICell[]
			{
				new GQICell { Value = "Capacity" },
				new GQICell { Value = Convert.ToDouble(_metrics.Capacity) } 
            });

			var occupancyRow = new GQIRow(new GQICell[]
			{
				new GQICell { Value = "CurrentOccupancy" },
				new GQICell { Value = Convert.ToDouble(_metrics.CurrentOccupancy) } 
            });


			var allRows = new GQIRow[] { capacityRow, occupancyRow };

			return new GQIPage(allRows) { HasNextPage = false };
		}
		private class OverallMetrics
		{
			public int Capacity { get; set; } = 0;
			public int CurrentOccupancy { get; set; } = 0;
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