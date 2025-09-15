using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Messages;
using Skyline.DataMiner.Automation;

namespace Aggregated_Lot_Metrics_DataSource
{
	[GQIMetaData(Name = "Aggregated_Lot_Metrics_DataSource")]
	public sealed class MultiLotSummaryDataSource : IGQIDataSource, IGQIOnInit, IGQIOnPrepareFetch
	{
		private GQIDMS _dms;
		private List<LotMetrics> _lotMetrics;
		private IGQILogger _logger;

		private readonly List<(string LotID, int DmaID, int ElementID, int TablePID, double Lat, double Lon)> _dataSources = new List<(string, int, int, int, double, double)>
		{
            //         Lot Name   DMA ID   Element ID   Table PID   Latitude   Longitude
            ("Lot A", 1004671,      565,        500,        43.854432,   18.406943), // SCC
            ("Lot B", 1004671,      587,        500,        43.853788,   18.407895), // Kotromanica
            ("Lot C", 1004671,      567,        500,        43.85697,   18.396338), // Kampus
        };

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			return default;
		}

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("LotID"),
				new GQIDoubleColumn("Capacity"),
				new GQIDoubleColumn("CurrentOccupancy"),
				new GQIIntColumn("OccupancyPercent"),
				new GQIDoubleColumn("Latitude"),
				new GQIDoubleColumn("Longitude"),
			};
		}

		public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
		{
			_lotMetrics = new List<LotMetrics>();
			try
			{
				foreach (var source in _dataSources)
				{
					var rawTableData = GqiGetPartialTable.GetTable(_dms, source.DmaID, source.ElementID, source.TablePID);
					if (rawTableData == null || !rawTableData.Any())
					{
						_logger.Warning($"IoT simulation table for {source.LotID} is empty or could not be read.");
						continue;
					}

					var allRows = rawTableData.Values.ToList();

					const int numericValueColumnIndex = 4;

					int capacity = allRows.Count();
					int occupiedCount = allRows.Count(row => Convert.ToDouble(row[numericValueColumnIndex], CultureInfo.InvariantCulture) > 0.5);
					
					int occupancyPercent = (capacity > 0)
					? (int)Math.Round((occupiedCount / (double)capacity) * 100.0)
					: 0;

					_lotMetrics.Add(new LotMetrics
					{
						LotID = source.LotID,
						Capacity = capacity,
						CurrentOccupancy = occupiedCount,
						OccupancyPercent = occupancyPercent,
						Latitude = source.Lat,
						Longitude = source.Lon,
					});
				}

				_logger.Information($"Aggregated metrics for {_lotMetrics.Count} parking lots.");
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to aggregate parking lot metrics.");
			}
			return null;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			var rows = _lotMetrics.Select(metrics => new GQIRow(new GQICell[] {
				new GQICell { Value = metrics.LotID },
				new GQICell { Value = Convert.ToDouble(metrics.Capacity) },
				new GQICell { Value = Convert.ToDouble(metrics.CurrentOccupancy) },
				new GQICell { Value = metrics.OccupancyPercent },
				new GQICell { Value = metrics.Latitude },
				new GQICell { Value = metrics.Longitude },
			})).ToArray();

			return new GQIPage(rows) { HasNextPage = false };
		}

		private class LotMetrics
		{
			public string LotID { get; set; }
			public int Capacity { get; set; }
			public int CurrentOccupancy { get; set; }
			public int OccupancyPercent { get; set; }
			public double Latitude { get; set; }
			public double Longitude { get; set; }
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

	namespace Skyline.Scripting
	{
		namespace Generic
		{
			public static class AutomationScriptMethods
			{
				public static GetProtocolInfoResponseMessage GetProtocol(string protocolName, string version)
				{
					var GetProtocolMsg = new GetProtocolMessage
					{
						Protocol = protocolName,
						Version = version
					};

					return Engine.SLNet.SendSingleResponseMessage(GetProtocolMsg) as GetProtocolInfoResponseMessage;
				}
			}
		}
	}
}
