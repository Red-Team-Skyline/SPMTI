namespace IoT_Simulation_Data_Source
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Globalization;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages;

	[GQIMetaData(Name = "IoT Simulation Data Source")]
	public sealed class IoTDataSource : IGQIDataSource, IGQIOnInit, IGQIInputArguments, IGQIOnPrepareFetch
	{
		private GQIDMS _dms;
		private int _dmaId;
		private int _elementId;
		private int _tableId;
		private IDictionary<string, object[]> _simulationData;
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
				new GQIStringColumn("ID"),
				new GQIStringColumn("Parameter"),
				new GQIDoubleColumn("Lower Bound"),
				new GQIDoubleColumn("Upper Bound"),
				new GQIDoubleColumn("Numeric Value"),
				new GQIStringColumn("String Value"),
				new GQIStringColumn("Unit"),
				new GQIStringColumn("Simulation"),
				new GQIStringColumn("Growth Rate"),
				new GQIStringColumn("Group 1"),
				new GQIDoubleColumn("Latitude (Group 2)"),
				new GQIDoubleColumn("Longitude (Group 3)"),
				new GQIStringColumn("Update Intervals"),
				new GQIStringColumn("Is Active"),
			};
		}

		public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
		{
			try
			{
				_simulationData = GqiGetPartialTable.GetTable(_dms, _dmaId, _elementId, _tableId);
				_logger.Information($"Retrieved {_simulationData.Count} rows from the simulation element.");
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to retrieve simulation data.");
				_simulationData = new Dictionary<string, object[]>();
			}
			return null;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			var rows = _simulationData.Select(kvp => CreateRow(kvp.Value)).ToArray();
			return new GQIPage(rows) { HasNextPage = false };
		}

		private GQIRow CreateRow(object[] originalCells)
		{
			var cells = new List<GQICell>();

			// Define the expected types for each column index, matching GetColumns().
			// This makes the code robust and easy to read.
			var expectedTypes = new Type[]
			{
				typeof(string), // 0: ID
                typeof(string), // 1: Parameter
                typeof(double), // 2: Lower Bound
                typeof(double), // 3: Upper Bound
                typeof(double), // 4: Numeric Value
                typeof(string), // 5: String Value
                typeof(string), // 6: Unit
                typeof(string), // 7: Simulation  
                typeof(string), // 8: Growth Rate (Assuming string, change if needed)
                typeof(string), // 9: Group 1
                typeof(double), // 10: Latitude (Group 2)
                typeof(double), // 11: Longitude (Group 3)
                typeof(string), // 12: Update Intervals
                typeof(string), // 13: Is Active
            };

			for (int i = 0; i < 14; i++)
			{
				var value = (i < originalCells.Length) ? originalCells[i] : null;
				var expectedType = expectedTypes[i];
				object convertedValue = null;

				try
				{
					if (value != null)
					{
						if (expectedType == typeof(double))
						{
							
							convertedValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
						}
						else if (expectedType == typeof(string))
						{
	
							convertedValue = Convert.ToString(value, CultureInfo.InvariantCulture);
						}
						else
						{
							convertedValue = value;
						}
					}
				}
				catch (FormatException)
				{
					convertedValue = null;
				}

				cells.Add(new GQICell { Value = convertedValue });
			}

			return new GQIRow(cells.ToArray());
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

			// Handle potentially empty tables
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
