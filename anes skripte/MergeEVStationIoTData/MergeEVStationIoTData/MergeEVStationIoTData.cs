using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.ManagerStore;
using Skyline.DataMiner.Net.Messages;
using Skyline.DataMiner.Net.Sections;

namespace MergeEVStationIoTData
{
	[GQIMetaData(Name = "Enriched EV Station Data Source")]
	public sealed class EnrichedEVStationDataSource : IGQIDataSource, IGQIOnInit, IGQIOnPrepareFetch
	{
		private GQIDMS _dms;
		private List<EnrichedEVStation> _enrichedData;
		private IGQILogger _logger;

		private readonly Dictionary<string, (int DmaID, int ElementID, int TablePID)> _sourceMap = new Dictionary<string, (int, int, int)>(StringComparer.OrdinalIgnoreCase)
		{
            // Keyword       DMA ID   Element ID   Table PID
            { "SCC",        (1004671,      568,        500) },
			{ "Kotromanic", (1004671,      569,        500) },
			{ "Kampus",     (1004671,      570,        500) },
		};

		private const string DOM_MODULE_NAME = "parking_lot_manager";
		private const string EV_STATION_DEFINITION_NAME = "EVStation";

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
				new GQIStringColumn("StationID"),
				new GQIStringColumn("StationName"),
				new GQIDoubleColumn("Latitude"),
				new GQIDoubleColumn("Longitude"),
				new GQIDoubleColumn("NumericValue"),
				new GQIStringColumn("StringValue"),
				new GQIStringColumn("Unit"),
				new GQIStringColumn("SimulationType"),
			};
		}

		public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
		{
			_enrichedData = new List<EnrichedEVStation>();
			try
			{
				var engine = new MockEngine(_dms);
				var domHelper = new DomHelper(engine.SendSLNetMessages, DOM_MODULE_NAME);

				var evStationDefinition = domHelper.DomDefinitions.ReadAll().FirstOrDefault(d => d.Name.Equals(EV_STATION_DEFINITION_NAME, StringComparison.OrdinalIgnoreCase));
				if (evStationDefinition == null) throw new InvalidOperationException($"DOM Definition '{EV_STATION_DEFINITION_NAME}' not found.");

				var sections = GetSectionsForDefinition(domHelper, evStationDefinition);
				var fieldCache = new EVStationFieldDescriptorCache(sections);

				var iotTables = new Dictionary<string, IDictionary<string, object[]>>();
				foreach (var source in _sourceMap)
				{
					var table = GqiGetPartialTable.GetTable(_dms, source.Value.DmaID, source.Value.ElementID, source.Value.TablePID);
					iotTables[source.Key] = table;
				}

				var allEvStations = domHelper.DomInstances.ReadAll().Where(i => i.DomDefinitionId.Id == evStationDefinition.ID.Id).ToList();

				foreach (var instance in allEvStations)
				{
					string stationId = instance.GetFieldValue<string>(fieldCache.IdSection, fieldCache.IdField).Value;
					string stationName = instance.GetFieldValue<string>(fieldCache.NameSection, fieldCache.NameField).Value;
					double latitude = instance.GetFieldValue<double>(fieldCache.LatitudeSection, fieldCache.LatitudeField).Value;
					double longitude = instance.GetFieldValue<double>(fieldCache.LongitudeSection, fieldCache.LongitudeField).Value;

					string sourceKey = _sourceMap.Keys.FirstOrDefault(key => stationName.Contains(key));

					object[] iotRow = null;
					if (sourceKey != null && iotTables.ContainsKey(sourceKey))
					{
						iotTables[sourceKey].TryGetValue(stationId, out iotRow);
					}

					_enrichedData.Add(new EnrichedEVStation
					{
						StationID = stationId,
						StationName = stationName,
						Latitude = latitude,
						Longitude = longitude,
						NumericValue = (iotRow != null && iotRow.Length > 4 && iotRow[4] != null) ? Convert.ToDouble(iotRow[4], CultureInfo.InvariantCulture) : -1.0,
						StringValue = (iotRow != null && iotRow.Length > 5) ? Convert.ToString(iotRow[5]) : "N/A",
						Unit = (iotRow != null && iotRow.Length > 6) ? Convert.ToString(iotRow[6]) : "N/A",
						SimulationType = (iotRow != null && iotRow.Length > 7) ? Convert.ToString(iotRow[7]) : "N/A",
					});
				}
				_logger.Information($"Successfully enriched data for {_enrichedData.Count} EV Stations.");
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to enrich EV Station data.");
			}
			return null;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			var rows = _enrichedData.Select(data => new GQIRow(new GQICell[]
			{
				new GQICell { Value = data.StationID },
				new GQICell { Value = data.StationName },
				new GQICell { Value = data.Latitude },
				new GQICell { Value = data.Longitude },
				new GQICell { Value = data.NumericValue },
				new GQICell { Value = data.StringValue },
				new GQICell { Value = data.Unit },
				new GQICell { Value = data.SimulationType },
			})).ToArray();

			return new GQIPage(rows) { HasNextPage = false };
		}

		private List<SectionDefinition> GetSectionsForDefinition(DomHelper domHelper, DomDefinition domDef)
		{
			var sections = new List<SectionDefinition>();
			var allSections = domHelper.SectionDefinitions.ReadAll();
			foreach (var link in domDef.SectionDefinitionLinks)
			{
				var section = allSections.FirstOrDefault(s => s.GetID().Id == link.SectionDefinitionID.Id);
				if (section != null) sections.Add(section);
			}
			return sections;
		}

		private class EnrichedEVStation
		{
			public string StationID { get; set; }
			public string StationName { get; set; }
			public double Latitude { get; set; }
			public double Longitude { get; set; }
			public double NumericValue { get; set; }
			public string StringValue { get; set; }
			public string Unit { get; set; }
			public string SimulationType { get; set; }
		}

		private class EVStationFieldDescriptorCache
		{
			public SectionDefinition IdSection { get; }
			public FieldDescriptor IdField { get; }
			public SectionDefinition NameSection { get; }
			public FieldDescriptor NameField { get; }
			public SectionDefinition LatitudeSection { get; }
			public FieldDescriptor LatitudeField { get; }
			public SectionDefinition LongitudeSection { get; }
			public FieldDescriptor LongitudeField { get; }

			public EVStationFieldDescriptorCache(List<SectionDefinition> sections)
			{
				SectionDefinition tempSection;
				IdField = FindField(sections, "Id", out tempSection);
				IdSection = tempSection;
				NameField = FindField(sections, "Name", out tempSection);
				NameSection = tempSection;
				LatitudeField = FindField(sections, "Latitude", out tempSection);
				LatitudeSection = tempSection;
				LongitudeField = FindField(sections, "Longitude", out tempSection);
				LongitudeSection = tempSection;
			}

			private FieldDescriptor FindField(List<SectionDefinition> sections, string fieldName, out SectionDefinition owner)
			{
				foreach (var sec in sections)
				{
					var f = sec.GetAllFieldDescriptors().FirstOrDefault(fd => fd.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
					if (f != null) { owner = sec; return f; }
				}
				throw new Exception("Field not found: " + fieldName);
			}
		}
	}

	public class MockEngine
	{
		private readonly GQIDMS _dms;
		public MockEngine(GQIDMS dms) { _dms = dms; }
		public Func<DMSMessage[], DMSMessage[]> SendSLNetMessages => messages =>
		{
			var results = new List<DMSMessage>();
			foreach (var message in messages)
			{
				var result = _dms.SendMessage(message);
				if (result != null) results.Add(result);
			}
			return results.ToArray();
		};
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