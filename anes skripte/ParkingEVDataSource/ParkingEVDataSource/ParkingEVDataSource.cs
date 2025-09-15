using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.ManagerStore;
using Skyline.DataMiner.Net.Messages;
using Skyline.DataMiner.Net.Sections;

namespace ParkingEVDataSource
{
    [GQIMetaData(Name = "Parking EV Data")]
    public sealed class ParkingWithEVDataSource : IGQIDataSource, IGQIOnInit, IGQIOnPrepareFetch
	{
        private GQIDMS _dms;
        private IGQILogger _logger;
        private List<EnrichedParkingSpace> _enrichedData;

        private const string DOM_MODULE_NAME = "parking_lot_manager";
        private const string PARKING_SPACE_DEFINITION_NAME = "NewParkingSpaces";

        private readonly Dictionary<string, (int DmaID, int ElementID, int TablePID)> _parkingSources =
            new Dictionary<string, (int, int, int)>(StringComparer.OrdinalIgnoreCase)
            {
                { "SCC",  (1004671, 565, 500) },
                { "Kotromanić", (1004671, 587, 500) },
                { "Kampus", (1004671, 567, 500) }
            };

        private readonly Dictionary<string, (int DmaID, int ElementID, int TablePID)> _evSources =
            new Dictionary<string, (int, int, int)>(StringComparer.OrdinalIgnoreCase)
            {
                { "SCC",  (1004671, 568, 500) },
                { "Kotromanić", (1004671, 569, 500) },
                { "Kampus", (1004671, 570, 500) }
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
                new GQIStringColumn("Id"),
                new GQIStringColumn("Name"),
                new GQIDoubleColumn("Latitude"),
                new GQIDoubleColumn("Longitude"),
                new GQIDoubleColumn("ParkingValue"),
                new GQIDoubleColumn("EVValue")
            };
        }
		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[0]; // No inputs needed
		}
		public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
        {
            _enrichedData = new List<EnrichedParkingSpace>();

            try
            {
                var engine = new MockEngine(_dms);
                var domHelper = new DomHelper(engine.SendSLNetMessages, DOM_MODULE_NAME);

                var parkingDef = domHelper.DomDefinitions.ReadAll()
                    .FirstOrDefault(d => d.Name.Equals(PARKING_SPACE_DEFINITION_NAME, StringComparison.OrdinalIgnoreCase));
                if (parkingDef == null)
                    throw new InvalidOperationException($"DOM Definition '{PARKING_SPACE_DEFINITION_NAME}' not found.");

                var sections = GetSectionsForDefinition(domHelper, parkingDef);
                var fieldCache = new ParkingSpaceFieldDescriptorCache(sections);

                // Read all DOM spaces
                var allSpaces = domHelper.DomInstances.ReadAll()
                    .Where(i => i.DomDefinitionId.Id == parkingDef.ID.Id)
                    .ToList();

                // Load EV tables for all locations (first 10 rows)
                var evTables = new Dictionary<string, List<object[]>>();
                foreach (var kvp in _evSources)
                {
                    var table = GqiGetPartialTable.GetTable(_dms, kvp.Value.DmaID, kvp.Value.ElementID, kvp.Value.TablePID);
                    evTables[kvp.Key] = table.Values.Take(10).ToList();
                }

                // Group spaces by location keyword
                var spacesByLocation = allSpaces.GroupBy(space =>
                {
                    string name = space.GetFieldValue<string>(fieldCache.NameSection, fieldCache.NameField).Value;
                    return _parkingSources.Keys.FirstOrDefault(k => !string.IsNullOrEmpty(name) &&
                        name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
                });

                foreach (var locationGroup in spacesByLocation)
                {
                    string locationKey = locationGroup.Key;
                    if (locationKey == null)
                    {
                        _logger.Warning("Some spaces did not match any location key.");
                        continue;
                    }

                    // Load the correct parking table for this location
                    var parkingInfo = _parkingSources[locationKey];
                    var parkingTable = GqiGetPartialTable.GetTable(_dms, parkingInfo.DmaID, parkingInfo.ElementID, parkingInfo.TablePID);

                    var evRows = evTables.ContainsKey(locationKey) ? evTables[locationKey] : new List<object[]>();
                    int evIndex = 0;

                    foreach (var space in locationGroup)
                    {
                        string id = space.GetFieldValue<string>(fieldCache.IdSection, fieldCache.IdField).Value?.Trim();
                        string name = space.GetFieldValue<string>(fieldCache.NameSection, fieldCache.NameField).Value;
                        double latitude = space.GetFieldValue<double>(fieldCache.LatitudeSection, fieldCache.LatitudeField).Value;
                        double longitude = space.GetFieldValue<double>(fieldCache.LongitudeSection, fieldCache.LongitudeField).Value;

                        // Parking numeric value
                        object[] parkingRow = null;
                        if (id != null)
                        {
                            if (!parkingTable.TryGetValue(id, out parkingRow))
                            {
                                // fallback: match by string-trimmed ID
                                parkingRow = parkingTable.Values.FirstOrDefault(r => r.Length > 0 && r[0]?.ToString().Trim() == id);
                            }
                        }

                        double parkingValue = (parkingRow != null && parkingRow.Length > 4 && parkingRow[4] != null)
                            ? Convert.ToDouble(parkingRow[4], CultureInfo.InvariantCulture)
                            : -1.0;

                        // EV numeric value (first 10 only)
                        double evValue = -1.0;
                        if (evIndex < evRows.Count && evRows[evIndex].Length > 4 && evRows[evIndex][4] != null)
                        {
                            evValue = Convert.ToDouble(evRows[evIndex][4], CultureInfo.InvariantCulture);
                        }
                        evIndex++;

                        _enrichedData.Add(new EnrichedParkingSpace
                        {
                            Id = id,
                            Name = name,
                            Latitude = latitude,
                            Longitude = longitude,
                            ParkingValue = parkingValue,
                            EVValue = evValue
                        });
                    }
                }

                _logger.Information($"Successfully enriched data for {_enrichedData.Count} parking spaces.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to enrich Parking + EV data.");
            }

            return null;
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            var rows = _enrichedData.Select(data => new GQIRow(new GQICell[]
            {
                new GQICell { Value = data.Id },
                new GQICell { Value = data.Name },
                new GQICell { Value = data.Latitude },
                new GQICell { Value = data.Longitude },
                new GQICell { Value = data.ParkingValue },
                new GQICell { Value = data.EVValue }
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

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			throw new NotImplementedException();
		}

		private class EnrichedParkingSpace
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double ParkingValue { get; set; }
            public double EVValue { get; set; }
        }

        private class ParkingSpaceFieldDescriptorCache
        {
            public SectionDefinition IdSection { get; }
            public FieldDescriptor IdField { get; }
            public SectionDefinition NameSection { get; }
            public FieldDescriptor NameField { get; }
            public SectionDefinition LatitudeSection { get; }
            public FieldDescriptor LatitudeField { get; }
            public SectionDefinition LongitudeSection { get; }
            public FieldDescriptor LongitudeField { get; }

            public ParkingSpaceFieldDescriptorCache(List<SectionDefinition> sections)
            {
                SectionDefinition tempSection;
                IdField = FindField(sections, "Space ID", out tempSection);
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
                            result[keyMap[rowNumber]][col] = cell.CellValue.ValueType == ParameterValueType.Empty ? null : cell.CellValue.InteropValue;
                        }
                        rowNumber++;
                    }
                }
            }
            return result;
        }
    }
}
