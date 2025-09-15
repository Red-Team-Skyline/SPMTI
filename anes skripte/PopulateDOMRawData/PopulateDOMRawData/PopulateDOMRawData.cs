
using System;
using System.Collections.Generic;
using System.Linq;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.ManagerStore;
using Skyline.DataMiner.Net.Sections;

namespace PopulateDOMRawData
{
	public class Script
	{
		private const string DOM_MODULE_NAME = "parking_lot_manager";      
		private const string SPACE_DEFINITION_NAME = "NewParkingSpaces";
		private readonly List<ParkingSpaceData> masterSpaceList = new List<ParkingSpaceData>
{
	new ParkingSpaceData("298", "Space C-298 (Kampus)", true, 43.858148, 18.396665),
new ParkingSpaceData("299", "Space C-299 (Kampus)", false, 43.858123, 18.396668),
new ParkingSpaceData("300", "Space C-300 (Kampus)", true, 43.858095, 18.396672),
new ParkingSpaceData("301", "Space C-301 (Kampus)", true, 43.858074, 18.396678),
new ParkingSpaceData("302", "Space C-302 (Kampus)", false, 43.858051, 18.396681),
new ParkingSpaceData("303", "Space C-303 (Kampus)", false, 43.858030, 18.396682),
new ParkingSpaceData("304", "Space C-304 (Kampus)", true, 43.858007, 18.396680),
new ParkingSpaceData("305", "Space C-305 (Kampus)", false, 43.857982, 18.396682),
new ParkingSpaceData("306", "Space C-306 (Kampus)", true, 43.857955, 18.396683),
new ParkingSpaceData("307", "Space C-307 (Kampus)", true, 43.857913, 18.396687),
new ParkingSpaceData("308", "Space C-308 (Kampus)", false, 43.857889, 18.396693),
new ParkingSpaceData("309", "Space C-309 (Kampus)", false, 43.857867, 18.396697),
new ParkingSpaceData("310", "Space C-310 (Kampus)", true, 43.857843, 18.396704),
new ParkingSpaceData("311", "Space C-311 (Kampus)", false, 43.857818, 18.396703),
new ParkingSpaceData("312", "Space C-312 (Kampus)", true, 43.857781, 18.396704),
new ParkingSpaceData("313", "Space C-313 (Kampus)", false, 43.857754, 18.396704),
new ParkingSpaceData("314", "Space C-314 (Kampus)", true, 43.857733, 18.396704),
new ParkingSpaceData("315", "Space C-315 (Kampus)", false, 43.857700, 18.396709),
new ParkingSpaceData("316", "Space C-316 (Kampus)", false, 43.857497, 18.395968),
new ParkingSpaceData("317", "Space C-317 (Kampus)", true, 43.857499, 18.395907),
new ParkingSpaceData("318", "Space C-318 (Kampus)", false, 43.857496, 18.395840),
new ParkingSpaceData("319", "Space C-319 (Kampus)", true, 43.857493, 18.395754),
new ParkingSpaceData("320", "Space C-320 (Kampus)", false, 43.857493, 18.395690),
new ParkingSpaceData("321", "Space C-321 (Kampus)", true, 43.857493, 18.395601),
new ParkingSpaceData("322", "Space C-322 (Kampus)", false, 43.857490, 18.395530),
new ParkingSpaceData("323", "Space C-323 (Kampus)", true, 43.857487, 18.395449),
new ParkingSpaceData("324", "Space C-324 (Kampus)", false, 43.857483, 18.395357),
new ParkingSpaceData("325", "Space C-325 (Kampus)", true, 43.857481, 18.395288),
new ParkingSpaceData("326", "Space C-326 (Kampus)", false, 43.857481, 18.395212),
new ParkingSpaceData("327", "Space C-327 (Kampus)", true, 43.857482, 18.395144),
new ParkingSpaceData("328", "Space C-328 (Kampus)", false, 43.857567, 18.395440),
new ParkingSpaceData("329", "Space C-329 (Kampus)", true, 43.856573, 18.396089),
new ParkingSpaceData("330", "Space C-330 (Kampus)", false, 43.856576, 18.396128),
new ParkingSpaceData("331", "Space C-331 (Kampus)", true, 43.856580, 18.396155),
new ParkingSpaceData("332", "Space C-332 (Kampus)", false, 43.856576, 18.396191),
new ParkingSpaceData("333", "Space C-333 (Kampus)", true, 43.856581, 18.396225),
new ParkingSpaceData("334", "Space C-334 (Kampus)", false, 43.856583, 18.396252),
new ParkingSpaceData("335", "Space C-335 (Kampus)", true, 43.856583, 18.396291),
new ParkingSpaceData("336", "Space C-336 (Kampus)", false, 43.856583, 18.396329),
new ParkingSpaceData("337", "Space C-337 (Kampus)", true, 43.856585, 18.396365),
new ParkingSpaceData("338", "Space C-338 (Kampus)", false, 43.856586, 18.396402),
new ParkingSpaceData("339", "Space C-339 (Kampus)", true, 43.856595, 18.396433),
new ParkingSpaceData("340", "Space C-340 (Kampus)", false, 43.856609, 18.396513),
new ParkingSpaceData("341", "Space C-341 (Kampus)", true, 43.856612, 18.396543),
new ParkingSpaceData("342", "Space C-342 (Kampus)", false, 43.856614, 18.396576),
new ParkingSpaceData("343", "Space C-343 (Kampus)", true, 43.856661, 18.396602),
new ParkingSpaceData("344", "Space C-344 (Kampus)", false, 43.856654, 18.396573),
new ParkingSpaceData("345", "Space C-345 (Kampus)", true, 43.856652, 18.396508),
new ParkingSpaceData("346", "Space C-346 (Kampus)", false, 43.856641, 18.396470),
new ParkingSpaceData("347", "Space C-347 (Kampus)", true, 43.856644, 18.396435),
new ParkingSpaceData("348", "Space C-348 (Kampus)", false, 43.856636, 18.396402),
new ParkingSpaceData("349", "Space C-349 (Kampus)", true, 43.856631, 18.396364),
new ParkingSpaceData("350", "Space C-350 (Kampus)", false, 43.856633, 18.396319),
new ParkingSpaceData("351", "Space C-351 (Kampus)", true, 43.856631, 18.396284),
new ParkingSpaceData("352", "Space C-352 (Kampus)", false, 43.856628, 18.396249),
new ParkingSpaceData("353", "Space C-353 (Kampus)", true, 43.856626, 18.396218),
new ParkingSpaceData("354", "Space C-354 (Kampus)", false, 43.856626, 18.396184),
new ParkingSpaceData("355", "Space C-355 (Kampus)", true, 43.856627, 18.396148),
new ParkingSpaceData("356", "Space C-356 (Kampus)", false, 43.856622, 18.396115),
new ParkingSpaceData("357", "Space C-357 (Kampus)", true, 43.856619, 18.396090),
new ParkingSpaceData("358", "Space C-358 (Kampus)", false, 43.856831, 18.396861),

};

		public void Run(Engine engine)
		{
			var domHelper = new DomHelper(engine.SendSLNetMessages, DOM_MODULE_NAME);

			engine.GenerateInformation($"Attempting to retrieve DOM Definition '{SPACE_DEFINITION_NAME}'...");
			var spaceDomDefinition = domHelper.DomDefinitions.ReadAll().FirstOrDefault(domDef => domDef.Name == SPACE_DEFINITION_NAME);
			if (spaceDomDefinition == null)
			{
				engine.GenerateInformation($"DOM Definition '{SPACE_DEFINITION_NAME}' not found.");
				return;
			}
			engine.GenerateInformation($"Found DOM Definition: {spaceDomDefinition.Name}");

			var sections = GetSectionsForDefinition(engine, domHelper, spaceDomDefinition);
			if (!sections.Any()) return;

			var fieldCache = new SpaceFieldDescriptorCache(sections);

			var allSpaceInstances = domHelper.DomInstances.ReadAll()
				.Where(i => i.DomDefinitionId.Id == spaceDomDefinition.ID.Id).ToList();

			int processedCount = 0;
			foreach (var spaceData in masterSpaceList)
			{
				try
				{
					var existingInstance = allSpaceInstances.FirstOrDefault(instance =>
					{
						var fieldValue = instance.GetFieldValue<string>(fieldCache.SpaceIdSection, fieldCache.SpaceIdField);
						return fieldValue != null && fieldValue.Value == spaceData.SpaceId;
					});

					if (existingInstance == null)
					{
						var newInstance = new DomInstance { DomDefinitionId = spaceDomDefinition.ID };
						PopulateInstanceFields(newInstance, fieldCache, spaceData);
						domHelper.DomInstances.Create(newInstance);
						engine.GenerateInformation($"Created DOM Instance for space {spaceData.SpaceId}");
					}
					else
					{
						PopulateInstanceFields(existingInstance, fieldCache, spaceData);
						domHelper.DomInstances.Update(existingInstance);
						engine.GenerateInformation($"Updated DOM Instance for space {spaceData.SpaceId}");
					}
					processedCount++;
				}
				catch (Exception ex)
				{
					engine.GenerateInformation($"Error processing space {spaceData.SpaceId}: {ex}");
				}
			}
			engine.GenerateInformation($"Script finished. Successfully processed {processedCount} space instances.");
		}

		private void PopulateInstanceFields(DomInstance instance, SpaceFieldDescriptorCache cache, ParkingSpaceData data)
		{
			bool isOccupiedValue = data.IsOccupied;
			instance.AddOrUpdateFieldValue(cache.SpaceIdSection, cache.SpaceIdField, data.SpaceId);
			instance.AddOrUpdateFieldValue(cache.NameSection, cache.NameField, data.Name);
			instance.AddOrUpdateFieldValue(cache.OccupancySection, cache.OccupancyField, data.IsOccupied);
			instance.AddOrUpdateFieldValue(cache.LatitudeSection, cache.LatitudeField, data.Latitude);
			instance.AddOrUpdateFieldValue(cache.LongitudeSection, cache.LongitudeField, data.Longitude);
		}

		private List<SectionDefinition> GetSectionsForDefinition(Engine engine, DomHelper domHelper, DomDefinition domDef)
		{
			var sections = new List<SectionDefinition>();
			var allSections = domHelper.SectionDefinitions.ReadAll(); 
			if (!domDef.SectionDefinitionLinks.Any())
			{
				engine.GenerateInformation($"No SectionDefinitionLinks found in '{domDef.Name}'.");
				return sections;
			}

			foreach (var link in domDef.SectionDefinitionLinks)
			{
				var section = allSections.FirstOrDefault(s => s.GetID().Id == link.SectionDefinitionID.Id);
				if (section != null)
				{
					sections.Add(section);
				}
			}
			engine.GenerateInformation($"Found {sections.Count} linked SectionDefinitions.");
			return sections;
		}

		private class SpaceFieldDescriptorCache
		{
			public SectionDefinition SpaceIdSection { get; }
			public FieldDescriptor SpaceIdField { get; }
			public SectionDefinition NameSection { get; }
			public FieldDescriptor NameField { get; }
			public SectionDefinition OccupancySection { get; }
			public FieldDescriptor OccupancyField { get; }
			public SectionDefinition LatitudeSection { get; }
			public FieldDescriptor LatitudeField { get; }
			public SectionDefinition LongitudeSection { get; }
			public FieldDescriptor LongitudeField { get; }

			public SpaceFieldDescriptorCache(List<SectionDefinition> sections)
			{
				SectionDefinition tempSection;
				SpaceIdField = FindField(sections, "Space ID", out tempSection);
				SpaceIdSection = tempSection;

				NameField = FindField(sections, "Name", out tempSection);
				NameSection = tempSection;

				OccupancyField = FindField(sections, "Occupancy", out tempSection);
				OccupancySection = tempSection;

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

	public class ParkingSpaceData
	{
		public string SpaceId { get; }
		public string Name { get; }
		public bool IsOccupied { get; }
		public double Latitude { get; }
		public double Longitude { get; }

		public ParkingSpaceData(string spaceId, string name, bool isOccupied, double latitude, double longitude)
		{
			SpaceId = spaceId;
			Name = name;
			IsOccupied = isOccupied;
			Latitude = latitude;
			Longitude = longitude;
		}
	}
}