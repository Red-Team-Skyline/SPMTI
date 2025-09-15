using System;
using System.Collections.Generic;
using System.Linq;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.ManagerStore;
using Skyline.DataMiner.Net.Sections;

namespace PopulateEVFromData
{
	public class Script
	{
		private const string DOM_MODULE_NAME = "parking_lot_manager";
		private const string STATION_DEFINITION_NAME = "EVStation";

		
			private readonly List<EVStationData> masterStationList = new List<EVStationData>
		{
            // Set 1: SCC
            new EVStationData("1", "Space A-1 (SCC)", 43.854817543764106, 18.406878362162796),
			new EVStationData("2", "Space A-2 (SCC)", 43.854811741359725, 18.406907195908907),
			new EVStationData("3", "Space A-3 (SCC)", 43.85480835662358, 18.406937370759493),
			new EVStationData("4", "Space A-4 (SCC)", 43.854802554218345, 18.406962181192196),
			new EVStationData("5", "Space A-5 (SCC)", 43.854798202414024, 18.406996379356194),
			new EVStationData("6", "Space A-6 (SCC)", 43.85479820241417, 18.40702923641882),
			new EVStationData("7", "Space A-7 (SCC)", 43.85479916948183, 18.407060752373873),
			new EVStationData("8", "Space A-8 (SCC)", 43.85479288354179, 18.40709092722446),
			new EVStationData("9", "Space A-9 (SCC)", 43.854789498804585, 18.407121102075042),
			new EVStationData("10", "Space A-10 (SCC)", 43.854790949406286, 18.407153288584393),

            // Set 2: Kotromanića
            new EVStationData("1", "Space B-1 (Kotromanića)", 43.8541484953469, 18.408483690974172),
			new EVStationData("2", "Space B-2 (Kotromanića)", 43.854117039271294, 18.40850076044735),
			new EVStationData("3", "Space B-3 (Kotromanića)", 43.854086950835594, 18.408519726528656),
			new EVStationData("4", "Space B-4 (Kotromanića)", 43.85405275941296, 18.408544382434354),
			new EVStationData("5", "Space B-5 (Kotromanića)", 43.85402540626076, 18.408559555299398),
			new EVStationData("6", "Space B-6 (Kotromanića)", 43.854004891388385, 18.40856903834005),
			new EVStationData("7", "Space B-7 (Kotromanića)", 43.853955655665835, 18.40859179763762),
			new EVStationData("8", "Space B-8 (Kotromanića)", 43.8539283024691, 18.408606970502664),
			new EVStationData("9", "Space B-9 (Kotromanića)", 43.853900949259774, 18.408628781496166),
			new EVStationData("10", "Space B-10 (Kotromanića)", 43.85388458592652, 18.4086431687604),

            // Set 3: Kampus
            new EVStationData("1", "Space C-1 (Kampus)", 43.856954462270735, 18.395204734853067),
			new EVStationData("2", "Space C-2 (Kampus)", 43.85690172662319, 18.39520788216699),
			new EVStationData("3", "Space C-3 (Kampus)", 43.856840776423795, 18.395211071868946),
			new EVStationData("4", "Space C-4 (Kampus)", 43.85679765124292, 18.39521426157117),
			new EVStationData("5", "Space C-5 (Kampus)", 43.85673957595298, 18.395220640975083),
			new EVStationData("6", "Space C-6 (Kampus)", 43.85668150060424, 18.395217451272973),
			new EVStationData("7", "Space C-7 (Kampus)", 43.85657914985438, 18.39522303325104),
			new EVStationData("8", "Space C-8 (Kampus)", 43.856538324505856, 18.39523021008069),
			new EVStationData("9", "Space C-9 (Kampus)", 43.856484849007856, 18.39523738691014),
			new EVStationData("10", "Space C-10 (Kampus)", 43.85643309848075, 18.395239779186607),
	};

		public void Run(Engine engine)
		{
			var domHelper = new DomHelper(engine.SendSLNetMessages, DOM_MODULE_NAME);

			engine.GenerateInformation($"Attempting to retrieve DOM Definition '{STATION_DEFINITION_NAME}'...");
			var stationDomDefinition = domHelper.DomDefinitions.ReadAll().FirstOrDefault(domDef => domDef.Name == STATION_DEFINITION_NAME);
			if (stationDomDefinition == null)
			{
				engine.GenerateInformation($"DOM Definition '{STATION_DEFINITION_NAME}' not found.");
				return;
			}
			engine.GenerateInformation($"Found DOM Definition: {stationDomDefinition.Name}");

			var sectionDefinition = GetSectionDefinition(engine, domHelper, stationDomDefinition);
			if (sectionDefinition == null) return;
			var fieldCache = new EVStationFieldDescriptorCache(sectionDefinition);

			var allStationInstances = domHelper.DomInstances.ReadAll()
				.Where(i => i.DomDefinitionId.Id == stationDomDefinition.ID.Id).ToList();

			int processedCount = 0;
			foreach (var stationData in masterStationList)
			{
				try
				{
					var existingInstance = allStationInstances.FirstOrDefault(instance =>
					{
						var fieldValue = instance.GetFieldValue<string>(sectionDefinition, fieldCache.IdField);
						return fieldValue != null && fieldValue.Value == stationData.Id;
					});

					if (existingInstance == null)
					{
						var newInstance = new DomInstance { DomDefinitionId = stationDomDefinition.ID };
						PopulateInstanceFields(newInstance, sectionDefinition, fieldCache, stationData);
						domHelper.DomInstances.Create(newInstance);
						engine.GenerateInformation($"Created DOM Instance for EV Station {stationData.Id}");
					}
					else
					{
						PopulateInstanceFields(existingInstance, sectionDefinition, fieldCache, stationData);
						domHelper.DomInstances.Update(existingInstance);
						engine.GenerateInformation($"Updated DOM Instance for EV Station {stationData.Id}");
					}
					processedCount++;
				}
				catch (Exception ex)
				{
					engine.GenerateInformation($"Error processing EV Station {stationData.Id}: {ex}");
				}
			}
			engine.GenerateInformation($"Script finished. Successfully processed {processedCount} EV Station instances.");
		}
		private void PopulateInstanceFields(DomInstance instance, SectionDefinition section, EVStationFieldDescriptorCache cache, EVStationData data)
		{
			instance.AddOrUpdateFieldValue(section, cache.IdField, data.Id);
			instance.AddOrUpdateFieldValue(section, cache.NameField, data.Name);
			instance.AddOrUpdateFieldValue(section, cache.LatitudeField, data.Latitude);
			instance.AddOrUpdateFieldValue(section, cache.LongitudeField, data.Longitude);
		}

		private SectionDefinition GetSectionDefinition(Engine engine, DomHelper domHelper, DomDefinition domDef)
		{
			var sectionLink = domDef.SectionDefinitionLinks.FirstOrDefault();
			if (sectionLink == null)
			{
				engine.GenerateInformation($"No SectionDefinitionLink found in '{domDef.Name}'.");
				return null;
			}
			var sectionDefinition = domHelper.SectionDefinitions.ReadAll().FirstOrDefault(x => x.GetID().Id == sectionLink.SectionDefinitionID.Id);
			if (sectionDefinition == null)
			{
				engine.GenerateInformation($"SectionDefinition with ID '{sectionLink.SectionDefinitionID.Id}' not found.");
				return null;
			}
			return sectionDefinition;
		}

		private class EVStationFieldDescriptorCache
		{
			public FieldDescriptor IdField { get; }
			public FieldDescriptor NameField { get; }
			public FieldDescriptor LatitudeField { get; }
			public FieldDescriptor LongitudeField { get; }

			public EVStationFieldDescriptorCache(SectionDefinition sectionDefinition)
			{
				IdField = GetFieldDescriptor(sectionDefinition, "ID");
				NameField = GetFieldDescriptor(sectionDefinition, "Name");
				LatitudeField = GetFieldDescriptor(sectionDefinition, "Latitude");
				LongitudeField = GetFieldDescriptor(sectionDefinition, "Longitude");
			}

			private FieldDescriptor GetFieldDescriptor(SectionDefinition sectionDefinition, string name)
			{
				var fd = sectionDefinition.GetAllFieldDescriptors().FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
				if (fd == null) throw new Exception($"Field '{name}' not found in SectionDefinition '{sectionDefinition.GetName()}'.");
				return fd;
			}
		}
	}

	public class EVStationData
	{
		public string Id { get; }
		public string Name { get; }
		public double Latitude { get; }
		public double Longitude { get; }

		public EVStationData(string id, string name, double latitude, double longitude)
		{
			Id = id;
			Name = name;
			Latitude = latitude;
			Longitude = longitude;
		}
	}
}