
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Messages;

namespace ParkingAggregationForLot
{
	[GQIMetaData(Name = "Parking Lot Summary")]
	public sealed class ParkingLotSummaryDataSource : IGQIDataSource, IGQIOnInit, IGQIInputArguments, IGQIOnPrepareFetch
	{
		private GQIDMS _dms;
		private int _dmaId;
		private int _elementId;
		private int _tableId;
		private List<ParkingLotSummary> _lotSummaries;
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
			// Here, we define the fields for our *summary table*, which is very different from the IoT table!
			return new GQIColumn[]
			{
				new GQIStringColumn("Lot ID"),        // e.g., "Lot A"
                new GQIStringColumn("Location Name"), // e.g., "Main St. Entrance"
                new GQIDoubleColumn("Capacity"),      // e.g., 100
                new GQIDoubleColumn("Occupied Spaces"),  // e.g., 68
                new GQIDoubleColumn("Occupancy Percentage"), // e.g., 68.00
                new GQIDoubleColumn("Latitude"),
				new GQIDoubleColumn("Longitude"),
			};
		}

		public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
		{
			// Now, read the data and perform the aggregation.
			_lotSummaries = new List<ParkingLotSummary>();
			try
			{
				// Get all the parking space rows from the IoT element
				var rawTable = GqiGetPartialTable.GetTable(_dms, _dmaId, _elementId, _tableId);

				// Create a dictionary to hold the information for each lot
				var lotData = new Dictionary<string, List<RawSpaceData>>();

				// Iterate all the rows in the table and save the information
				foreach (var row in rawTable)
				{
					string lotId = Convert.ToString(row.Value[9]); // "Group 1" - the parent lot ID.
					if (!lotData.ContainsKey(lotId))
					{
						lotData[lotId] = new List<RawSpaceData>();
					}

					// The raw data about the space
					var newRaw = new RawSpaceData();
					newRaw.Status = Convert.ToString(row.Value[5]);
					newRaw.Latitude = Convert.ToDouble(Convert.ToString(row.Value[10]), CultureInfo.InvariantCulture);
					newRaw.Longitude = Convert.ToDouble(Convert.ToString(row.Value[11]), CultureInfo.InvariantCulture);

					lotData[lotId].Add(newRaw);
				}

				// Loop over all lots
				foreach (var lot in lotData)
				{
					// Number of points for each lot
					int count = lot.Value.Count;

					// Create a new parking lot summary object
					var lotSummary = new ParkingLotSummary();
					lotSummary.LotId = lot.Key;
					lotSummary.SpaceCount = count;

					// Count the number of Occupied spaces for each
					var occupied = lot.Value.Where(x => x.Status == "Occupied").Count();
					lotSummary.OccupiedCount = occupied;

					lotSummary.OccupancyPercentage = (double)occupied / count * 100;

					//Set average Lat and Long for each
					lotSummary.Latitude = Math.Round(lot.Value.Average(a => a.Latitude), 6);
					lotSummary.Longitude = Math.Round(lot.Value.Average(a => a.Longitude), 6);

					// Set Default Values
					lotSummary.LocationName = lot.Key + " - " + string.Format("{0:N0}", count) + " spaces";
					lotSummary.Capacity = count;

					//Add it to the global var
					_lotSummaries.Add(lotSummary);
				}

				_logger.Information($"Aggregated data for {_lotSummaries.Count} parking lots.");
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to aggregate parking lot data.");
			}

			return null;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			// Return the aggregated lot summaries in GQIRows.
			var rows = _lotSummaries.Select(lotSummary => new GQIRow(new GQICell[] {
				new GQICell { Value = lotSummary.LotId },
				new GQICell { Value = lotSummary.LocationName },
				new GQICell { Value = lotSummary.Capacity },
				new GQICell { Value = lotSummary.OccupiedCount },
				new GQICell { Value = lotSummary.OccupancyPercentage },
				new GQICell { Value = lotSummary.Latitude },
				new GQICell { Value = lotSummary.Longitude }
			})).ToArray();

			return new GQIPage(rows) { HasNextPage = false };
		}

		public class RawSpaceData
		{
			public string Status { get; set; }
			public double Latitude { get; set; }
			public double Longitude { get; set; }
		}
		private class ParkingLotSummary
		{
			public string LotId { get; set; }
			public string LocationName { get; set; }
			public double Capacity { get; set; }
			public int SpaceCount { get; set; }
			public int OccupiedCount { get; set; }
			public double OccupancyPercentage { get; set; }
			public double Latitude { get; set; }
			public double Longitude { get; set; }
		}
	}

	// This is the helper class from your lamp script, proven to work in your environment.
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

			// Handle potentially empty tables
			if (columns[keyColumnIndex].ArrayValue == null) return result;

			string[] keyMap = new string[columns[keyColumnIndex].ArrayValue.Length];
			int rowNumber = 0;
			foreach (ParameterValue keyCell in columns[keyColumnIndex].ArrayValue)
			{
				string primaryKey = Convert.ToString(keyCell.CellValue.InteropValue, CultureInfo.CurrentCulture);
				if (primaryKey == null) continue; // Skip rows with null primary keys

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