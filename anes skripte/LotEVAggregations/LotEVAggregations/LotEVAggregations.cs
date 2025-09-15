using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Messages;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.ManagerStore;
using Skyline.DataMiner.Net.Sections;

namespace LotEVAggregations
{
	[GQIMetaData(Name = "LotEVAggregations")]
	public sealed class GridFriendlyEVOccupancyDataSource : IGQIDataSource, IGQIOnInit, IGQIOnPrepareFetch
	{
		private GQIDMS _dms;
		private IGQILogger _logger;
		private List<(string Name, string Value)> _gridData;

		// Hardcoded sources for lots and their EV/parking tables
		private readonly Dictionary<string, (int DmaID, int ElementID, int TablePID)> _parkingSources =
			new Dictionary<string, (int, int, int)>(StringComparer.OrdinalIgnoreCase)
			{
				{ "SCC",        (1004671, 565, 500) },
				{ "Kotromanić", (1004671, 587, 500) },
				{ "Kampus",     (1004671, 567, 500) }
			};

		private readonly Dictionary<string, (int DmaID, int ElementID, int TablePID)> _evSources =
			new Dictionary<string, (int, int, int)>(StringComparer.OrdinalIgnoreCase)
			{
				{ "SCC",        (1004671, 568, 500) },
				{ "Kotromanić", (1004671, 569, 500) },
				{ "Kampus",     (1004671, 570, 500) }
			};

		// Electricity price in Bosnia in USD/kWh
		private const double PriceUsdPerKwh = 0.115;

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
				new GQIStringColumn("Name"),
				new GQIStringColumn("Value")
			};
		}

		public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
		{
			_gridData = new List<(string Name, string Value)>();

			try
			{
				foreach (var lotKey in _parkingSources.Keys)
				{
					// Load parking and EV tables
					var parkingInfo = _parkingSources[lotKey];
					var evInfo = _evSources[lotKey];

					var parkingTable = GqiGetPartialTable.GetTable(_dms, parkingInfo.DmaID, parkingInfo.ElementID, parkingInfo.TablePID);
					var evTable = GqiGetPartialTable.GetTable(_dms, evInfo.DmaID, evInfo.ElementID, evInfo.TablePID);

					// Only consider up to 10 EV spaces
					var evRows = evTable.Values.Take(10).ToList();
					var parkingList = parkingTable.Values.Take(evRows.Count).ToList();

					double evOccupied = 0;
					double totalPower = 0.0;
					int n = Math.Min(evRows.Count, parkingList.Count);

					for (int i = 0; i < n; i++)
					{
						var parkingRow = parkingList[i];
						if (parkingRow.Length > 4 && parkingRow[4] != null)
						{
							double parkingValue = Convert.ToDouble(parkingRow[4], CultureInfo.InvariantCulture);
							if (parkingValue >= 0.5)
							{
								evOccupied++;
							}
						}

						// Power usage from EV row (assume kW usage is stored in col 4)
						var evRow = evRows[i];
						if (evRow.Length > 4 && evRow[4] != null)
						{
							double evValue = Convert.ToDouble(evRow[4], CultureInfo.InvariantCulture);
							totalPower += evValue;
						}
					}

					double occupancyPercent = (evOccupied / 10.0) * 100.0;
					double costPerHourUsd = totalPower * PriceUsdPerKwh;

					// Add rows for grid
					_gridData.Add(("Lot Name", lotKey));
					_gridData.Add(("EV Occupied", evOccupied.ToString("0", CultureInfo.InvariantCulture)));
					_gridData.Add(("EV %", occupancyPercent.ToString("0", CultureInfo.InvariantCulture)));
					_gridData.Add(("Total EV Power (kW)", totalPower.ToString("0.##", CultureInfo.InvariantCulture)));
					_gridData.Add(("Total Cost / hr (USD)", "$" + costPerHourUsd.ToString("0.00", CultureInfo.InvariantCulture)));
				}

				_logger.Information($"Grid data prepared for {_parkingSources.Count} lots. Total rows: {_gridData.Count}");
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to prepare grid data for EV occupancy.");
			}

			return null;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			var rows = _gridData.Select(d => new GQIRow(new GQICell[]
			{
				new GQICell { Value = d.Name },
				new GQICell { Value = d.Value }
			})).ToArray();

			return new GQIPage(rows) { HasNextPage = false };
		}
	}

	public static class GqiGetPartialTable
	{
		public static IDictionary<string, object[]> GetTable(GQIDMS gqiDms, int dmaId, int elementId, int tableId, int keyColumnIndex = 0)
		{
			var message = new GetPartialTableMessage(dmaId, elementId, tableId, new[] { "forceFullTable=true" });
			var response = (ParameterChangeEventMessage)gqiDms.SendMessage(message);
			if (response == null) throw new InvalidOperationException("Failed to retrieve table data. Response is null.");
			return BuildDictionary(response, keyColumnIndex);
		}

		private static IDictionary<string, object[]> BuildDictionary(ParameterChangeEventMessage response, int keyColumnIndex)
		{
			var result = new Dictionary<string, object[]>();
			if (response?.NewValue?.ArrayValue == null) return result;

			ParameterValue[] columns = response.NewValue.ArrayValue;
			if (columns.Length == 0) return result;

			string[] keyMap = new string[columns[keyColumnIndex].ArrayValue.Length];
			int rowNumber = 0;

			foreach (var keyCell in columns[keyColumnIndex].ArrayValue)
			{
				string primaryKey = Convert.ToString(keyCell.CellValue.InteropValue, CultureInfo.InvariantCulture)?.Trim();
				if (primaryKey == null) continue;
				result[primaryKey] = new object[columns.Length];
				keyMap[rowNumber] = primaryKey;
				rowNumber++;
			}

			for (int col = 0; col < columns.Length; col++)
			{
				rowNumber = 0;
				if (columns[col].ArrayValue != null)
				{
					foreach (var cell in columns[col].ArrayValue)
					{
						if (rowNumber < keyMap.Length && keyMap[rowNumber] != null)
						{
							result[keyMap[rowNumber]][col] =
								cell.CellValue.ValueType == ParameterValueType.Empty ? null : cell.CellValue.InteropValue;
						}
						rowNumber++;
					}
				}
			}
			return result;
		}
	}
}
