﻿using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Hoi4UnitHistoryGenerator.Model;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Hoi4UnitHistoryGenerator
{
    using TDir = Dictionary<string, Dictionary<string, string>>;

    partial class Entrypoint
    {
        [GeneratedRegex("^([A-Z]{3})\\.xlsx")]
        private static partial Regex RegTemplateFileName();

        [GeneratedRegex("division_template_\\S+")]
        private static partial Regex RegDivisionTemplate();

        static void Main()
        {
            string[] files = Directory.GetFiles("templates", "*.xlsx");
            foreach (var item in files)
            {
                string relativePath = Path.GetRelativePath("templates", item);
                var match = RegTemplateFileName().Match(relativePath);
                if (match.Success)
                {
                    string countryTag = match.Groups[1].Value;
                    try
                    {
                        Console.Out.WriteLine($"Handling xlsx file: {item}");
                        HandleXlsx(countryTag, item);
                        Console.Out.WriteLine("Success.");
                    }
                    catch (Exception ex)
                    {
                        Console.Out.WriteLine($"Fail: {ex.Message}");
                    }
                }
            }
        }

        private static void HandleXlsx(string countryTag, string filename)
        {
            using var doc = SpreadsheetDocument.Open(filename, false);
            if (doc.WorkbookPart is null)
            {
                Console.Error.WriteLine($"{filename} - Illegal xlsx: workbook part is missing");
                return;
            }

            SharedStringTablePart? sharedStringPart = doc.WorkbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();

            var workbook = doc.WorkbookPart!.Workbook;
            if (workbook.Sheets is null)
            {
                Console.Error.WriteLine($"{filename} - Illegal xlsx: sheet part is missing");
                return;
            }

            TDir directory = [];
            List<DivisionTemplate> divisionTemplates = [];
            List<DivisionEntity> divisionEntities = [];
            List<EquipmentVariant> armorVariants = [];
            List<EquipmentVariant> planeVariants = [];
            List<EquipmentVariant> shipVariants = [];
            List<Fleet> fleets = [];
            List<AirBase> airBases = [];

            SheetData? directorySheet = workbook.Sheets.Elements<Sheet>().
                Where(sheet => "dictionary" == sheet.Name).
                Select(sheet => (doc.WorkbookPart.GetPartById(sheet.Id!) as WorksheetPart)!.Worksheet.Elements<SheetData>().FirstOrDefault()!).
                FirstOrDefault();
            if (directorySheet is not null)
            {
                directory = LoadDictionary(directorySheet, sharedStringPart);
            }

            foreach (var sheet in workbook.Sheets.Elements<Sheet>())
            {
                var id = sheet.Id;
                WorksheetPart worksheetPart = (doc.WorkbookPart.GetPartById(sheet.Id!) as WorksheetPart)!;
                SheetData sheetData = worksheetPart.Worksheet.Elements<SheetData>().FirstOrDefault()!;

                var name = sheet.Name;
                if ("division_entities" == name)
                {
                    divisionEntities = LoadDivisionEntities(sheetData, sharedStringPart, directory);
                }
                else if ("armor_variants" == name)
                {
                    armorVariants = LoadEquipmentVariants(sheetData, sharedStringPart, directory);
                }
                else if ("plane_variants" == name)
                {
                    planeVariants = LoadEquipmentVariants(sheetData, sharedStringPart, directory);
                }
                else if ("ship_variants" == name)
                {
                    shipVariants = LoadEquipmentVariants(sheetData, sharedStringPart, directory);
                }
                else if ("fleets" == name)
                {
                    fleets = LoadFleets(sheetData, sharedStringPart, directory);
                }
                else if ("air_wings" == name)
                {
                    airBases = LoadAirBases(sheetData, sharedStringPart, directory);
                }
                else if (RegDivisionTemplate().IsMatch(name!))
                {
                    DivisionTemplate? divisionTemplate = LoadDivisionTemplate(sheetData, sharedStringPart, directory);
                    if (divisionTemplate is not null)
                    {
                        divisionTemplates.Add(divisionTemplate);
                    }
                }
            }

            Directory.CreateDirectory("output/history/units");

            using (TextWriter textWriter = new StreamWriter(new FileStream($"output/history/units/{countryTag}.txt", FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                textWriter.WriteLine("# Auto-generated by hoi4-unit-history-generator");

                textWriter.WriteLine("instant_effect = {");
                foreach (var ev in armorVariants)
                {
                    ev.Print(textWriter);
                    textWriter.WriteLine();
                }
                textWriter.WriteLine("}");
                textWriter.WriteLine();

                foreach (var divisionTemplate in divisionTemplates)
                {
                    divisionTemplate.Print(textWriter);
                    textWriter.WriteLine();
                }
                textWriter.WriteLine();

                textWriter.WriteLine("units = {");
                foreach (var divisionEntity in divisionEntities)
                {
                    divisionEntity.Print(textWriter);
                    textWriter.WriteLine();
                }
                textWriter.WriteLine("}");
            }

            using (TextWriter textWriter = new StreamWriter(new FileStream($"output/history/units/{countryTag}_navy.txt", FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                textWriter.WriteLine("# Auto-generated by hoi4-unit-history-generator");

                textWriter.WriteLine("instant_effect = {");
                foreach (var shipVariant in shipVariants)
                {
                    shipVariant.Print(textWriter);
                    textWriter.WriteLine();
                }
                textWriter.WriteLine("}");
                textWriter.WriteLine();

                textWriter.WriteLine("units = {");
                foreach (var fleet in fleets)
                {
                    fleet.Print(textWriter, countryTag);
                    textWriter.WriteLine();
                }
                textWriter.WriteLine("}");
            }

            using (TextWriter textWriter = new StreamWriter(new FileStream($"output/history/units/{countryTag}_air.txt", FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                textWriter.WriteLine("# Auto-generated by hoi4-unit-history-generator");

                textWriter.WriteLine("instant_effect = {");
                foreach (var ev in planeVariants)
                {
                    ev.Print(textWriter);
                    textWriter.WriteLine();
                }
                textWriter.WriteLine("}");
                textWriter.WriteLine();

                textWriter.WriteLine("air_wings = {");
                foreach (var airBase in airBases)
                {
                    airBase.Print(textWriter, countryTag);
                    textWriter.WriteLine();
                }
                textWriter.WriteLine("}");
            }
        }

        private static TDir LoadDictionary(SheetData sheet, SharedStringTablePart? sharedStringPart)
        {
            List<Row> rows = [.. sheet.Elements<Row>()];
            if (rows.Count == 0)
            {
                return [];
            }

            int iCategory = -1;
            int iName = -1;
            int iId = -1;

            List<string> headers = ConvertRowToCells(rows[0], sharedStringPart, 0);
            for (int i = 0; i < headers.Count; i++)
            {
                string header = headers[i];

                if (header == "category")
                {
                    if (iCategory != -1)
                        Console.Error.WriteLine("Column 'category' has already exist.");
                    else
                        iCategory = i;
                }
                else if (header == "name")
                {
                    if (iName != -1)
                        Console.Error.WriteLine("Column 'name' has already exist.");
                    else
                        iName = i;
                }
                else if (header == "id")
                {
                    if (iId != -1)
                        Console.Error.WriteLine("Column 'id' has already exist.");
                    else
                        iId = i;
                }
                else
                {
                    Console.Error.WriteLine($"Unrecognized dictionary header [{header}]");
                }
            }

            if (iCategory == -1)
            {
                Console.Error.WriteLine("Nessesary dictionary column 'category' is missing.");
                return [];
            }
            if (iName == -1)
            {
                Console.Error.WriteLine("Nessesary dictionary column 'name' is missing.");
                return [];
            }
            if (iId == -1)
            {
                Console.Error.WriteLine("Nessesary dictionary column 'id' is missing.");
                return [];
            }

            TDir results = [];

            for (int i = 1; i < rows.Count; i++)
            {
                List<string> cellValues = ConvertRowToCells(rows[i], sharedStringPart, headers.Count);

                string category = "";
                string name = "";
                string id = "";

                for (int j = 0; j < cellValues.Count; j++)
                {
                    if (iCategory == j)
                    {
                        category = cellValues[j];
                    }
                    if (iName == j)
                    {
                        name = cellValues[j];
                    }
                    if (iId == j)
                    {
                        id = cellValues[j];
                    }
                }

                var dirInCategory = results.GetValueOrDefault(category);
                if (dirInCategory == null)
                {
                    dirInCategory = [];
                    results[category] = dirInCategory;
                }

                dirInCategory[name] = id;
            }

            return results;
        }

        private static DivisionTemplate? LoadDivisionTemplate(SheetData sheet, SharedStringTablePart? sharedStringPart, TDir directory)
        {
            List<Row> rows = [.. sheet.Elements<Row>()];
            if (rows.Count == 0)
            {
                return null;
            }

            DivisionTemplate divisionTemplate = new();

            List<string> headerNames = ConvertRowToCells(rows[0], sharedStringPart, 0);

            List<List<string>> columns = [];

            for (int i = 1; i < rows.Count; i++)
            {
                List<string> cellValues = ConvertRowToCells(rows[i], sharedStringPart, headerNames.Count);
                for (int j = 0; j < cellValues.Count; j++)
                {
                    List<string> column;
                    if (columns.Count <= j)
                    {
                        column = [];
                        columns.Add(column);
                    }
                    else
                    {
                        column = columns[j];
                    }

                    column.Add(cellValues[j]);
                }
            }

            for (int i = 0; i < columns.Count; i++)
            {
                string rawHeader = headerNames[i];
                string header = directory.GetValueOrDefault("column_name", []).GetValueOrDefault(rawHeader, rawHeader);
                if (header == "Support")
                {
                    List<string> support0 = [];
                    for (int j = 0; j < columns[i].Count; j++)
                    {
                        string name = columns[i][j];
                        if (name.Length > 0)
                        {
                            support0.Add(directory.GetValueOrDefault("unit_name", []).GetValueOrDefault(name, name));
                        }
                    }
                    divisionTemplate.Support.Add(support0);
                }
                else if (header == "Regiment")
                {
                    List<string> regiment = [];
                    for (int j = 0; j < columns[i].Count; j++)
                    {
                        string name = columns[i][j];
                        if (name.Length > 0)
                        {
                            regiment.Add(directory.GetValueOrDefault("unit_name", []).GetValueOrDefault(name, name));
                        }
                    }
                    divisionTemplate.Regiments.Add(regiment);
                }
                else if (header == "Name")
                {
                    for (int j = 0; j < columns[i].Count; j++)
                    {
                        string name = columns[i][j];
                        if (name.Length > 0)
                        {
                            divisionTemplate.Name = name;
                            break;
                        }
                    }
                }
                else if (header == "DivisionNamesGroup")
                {
                    for (int j = 0; j < columns[i].Count; j++)
                    {
                        string name = columns[i][j];
                        if (name.Length > 0)
                        {
                            divisionTemplate.DivisionNameGroup = name;
                            break;
                        }
                    }
                }
                else if (header == "IsLocked")
                {
                    for (int j = 0; j < columns[i].Count; j++)
                    {
                        string name = columns[i][j];
                        if (name.Length > 0)
                        {
                            divisionTemplate.IsLocked = int.Parse(name);
                            break;
                        }
                    }
                }
            }


            return divisionTemplate;
        }

        private static List<DivisionEntity> LoadDivisionEntities(SheetData sheet, SharedStringTablePart? sharedStringPart, TDir directory)
        {
            List<Row> rows = [.. sheet.Elements<Row>()];
            if (rows.Count == 0)
            {
                return [];
            }

            List<PropertyInfo?> properties = [];
            {
                List<string> headerNames = ConvertRowToCells(rows[0], sharedStringPart, 0);
                foreach (var rawHeader in headerNames)
                {
                    string header = directory.GetValueOrDefault("column_name", []).GetValueOrDefault(rawHeader, rawHeader);
                    properties.Add(typeof(DivisionEntity).GetProperty(header));
                }
            }

            List<DivisionEntity> results = new(rows.Count - 1);

            for (int i = 1; i < rows.Count; i++)
            {
                DivisionEntity divisionEntity = new();

                List<string> cellValues = ConvertRowToCells(rows[i], sharedStringPart, properties.Count);
                for (int j = 0; j < cellValues.Count; j++)
                {
                    var property = properties[j];
                    property?.SetValue(divisionEntity, Convert.ChangeType(cellValues[j], property.PropertyType));
                }

                results.Add(divisionEntity);
            }

            return results;
        }

        private static List<EquipmentVariant> LoadEquipmentVariants(SheetData sheet, SharedStringTablePart? sharedStringPart, TDir directory)
        {
            List<Row> rows = [.. sheet.Elements<Row>()];
            if (rows.Count == 0)
            {
                return [];
            }

            List<PropertyInfo?> properties = [];
            List<string> headers = [];
            int iVariantName = -1;
            {
                List<string> headerNames = ConvertRowToCells(rows[0], sharedStringPart, 0);
                for (int i = 0; i < headerNames.Count; i++)
                {
                    string rawHeader = headerNames[i];
                    string header = directory.GetValueOrDefault("column_name", []).GetValueOrDefault(rawHeader, rawHeader);
                    headers.Add(header);

                    if ("Name" == header)
                    {
                        iVariantName = i;
                    }

                    properties.Add(typeof(EquipmentVariant).GetProperty(header));
                }
            }

            if (iVariantName == -1)
            {
                return [];
            }

            Dictionary<string, EquipmentVariant> resultMap = new(rows.Count - 1);
            List<EquipmentVariant> resultList = new(rows.Count - 1);
            for (int i = 1; i < rows.Count; i++)
            {
                EquipmentVariant equipmentVariant;

                List<string> cellValues = ConvertRowToCells(rows[i], sharedStringPart, headers.Count);

                string shipVariantName = cellValues[iVariantName];
                if (shipVariantName.Length == 0)
                {
                    if (resultList.Count == 0)
                    {
                        continue;
                    }
                    else
                    {
                        equipmentVariant = resultList.Last();
                    }
                }
                else
                {
                    var sv = resultMap.GetValueOrDefault(shipVariantName);
                    if (sv == null)
                    {
                        equipmentVariant = new();
                        resultList.Add(equipmentVariant);
                    }
                    else
                    {
                        equipmentVariant = sv;
                    }
                }

                string slotName = "";
                string equipment = "";
                string upgradeItem = "";
                int upgradeLevel = 0;
                for (int j = 0; j < cellValues.Count; j++)
                {
                    string columnValue = cellValues[j];
                    if (columnValue.Length == 0)
                    {
                        continue;
                    }

                    PropertyInfo? propertyInfo = properties[j];
                    if (propertyInfo is null)
                    {
                        string column = headers[j];
                        if ("Slot" == column)
                        {
                            slotName = columnValue;
                        }
                        else if ("Equipment" == column)
                        {
                            equipment = columnValue;
                        }
                        else if ("UpgradeItem" == column)
                        {
                            upgradeItem = columnValue;
                        }
                        else if ("UpgradeLevel" == column)
                        {
                            upgradeLevel = int.Parse(columnValue);
                        }
                    }
                    else
                    {
                        propertyInfo.SetValue(equipmentVariant, Convert.ChangeType(columnValue, propertyInfo.PropertyType));
                    }
                }

                if (slotName.Length > 0 && equipment.Length > 0)
                {
                    equipmentVariant.Modules[slotName] = equipment;
                }
                if (upgradeItem.Length > 0 && upgradeLevel > 0)
                {
                    equipmentVariant.Upgrades[upgradeItem] = upgradeLevel;
                }
            }

            return resultList;
        }

        private static List<Fleet> LoadFleets(SheetData sheet, SharedStringTablePart? sharedStringPart, TDir directory)
        {
            List<Row> rows = [.. sheet.Elements<Row>()];
            if (rows.Count == 0)
            {
                return [];
            }

            List<PropertyInfo?> properties = [];
            int iFleetName = -1;
            int iTaskForceName = -1;
            int iShipName = -1;
            {
                var headerNames = ConvertRowToCells(rows[0], sharedStringPart, 0);
                for (int i = 0; i < headerNames.Count; i++)
                {
                    string rawHeader = headerNames[i];
                    string header = directory.GetValueOrDefault("column_name", []).GetValueOrDefault(rawHeader, rawHeader);

                    if ("Name" == header)
                    {
                        iFleetName = i;
                    }
                    if ("TaskForce.Name" == header)
                    {
                        iTaskForceName = i;
                    }
                    if ("TaskForce.Ship.Name" == header)
                    {
                        iShipName = i;
                    }


                    if (header.StartsWith("TaskForce.Ship."))
                    {
                        string propertyName = header["TaskForce.Ship.".Length..];
                        properties.Add(typeof(WarShip).GetProperty(propertyName));
                    }
                    else if (header.StartsWith("TaskForce."))
                    {
                        string propertyName = header["TaskForce.".Length..];
                        properties.Add(typeof(TaskForce).GetProperty(propertyName));
                    }
                    else
                    {
                        properties.Add(typeof(Fleet).GetProperty(header));
                    }
                }

                if (iFleetName == -1 || iTaskForceName == -1 || iShipName == -1)
                {
                    return [];
                }

                Dictionary<string, Fleet> fleetMap = new(rows.Count - 1);
                List<Fleet> fleetList = new(rows.Count - 1);

                Dictionary<Fleet, Dictionary<string, TaskForce>> taskForceMap = [];
                Dictionary<TaskForce, Dictionary<string, WarShip>> shipMap = [];

                for (int i = 1; i < rows.Count; i++)
                {
                    List<string> cellValues = ConvertRowToCells(rows[i], sharedStringPart, headerNames.Count);
                    Fleet fleet;
                    TaskForce taskForce;
                    WarShip warShip;

                    string fleetName = cellValues[iFleetName];
                    if (fleetName.Length == 0)
                    {
                        if (fleetList.Count == 0)
                        {
                            continue;
                        }
                        else
                        {
                            fleet = fleetList.Last();
                        }
                    }
                    else
                    {
                        var f = fleetMap.GetValueOrDefault(fleetName);
                        if (f is null)
                        {
                            fleet = new();
                            fleetList.Add(fleet);
                        }
                        else
                        {
                            fleet = f;
                        }
                    }

                    Dictionary<string, TaskForce>? taskForceMap1 = taskForceMap.GetValueOrDefault(fleet);
                    if (taskForceMap1 is null)
                    {
                        taskForceMap1 = [];
                        taskForceMap[fleet] = taskForceMap1;
                    }

                    string taskForceName = cellValues[iTaskForceName];
                    if (taskForceName.Length == 0)
                    {
                        if (fleet.TaskForces.Count == 0)
                        {
                            continue;
                        }
                        else
                        {
                            taskForce = fleet.TaskForces.Last();
                        }
                    }
                    else
                    {
                        var tf = taskForceMap1.GetValueOrDefault(taskForceName);
                        if (tf is null)
                        {
                            taskForce = new();
                            fleet.TaskForces.Add(taskForce);
                        }
                        else
                        {
                            taskForce = tf;
                        }

                    }

                    Dictionary<string, WarShip>? shipMap1 = shipMap.GetValueOrDefault(taskForce);
                    if (shipMap1 is null)
                    {
                        shipMap1 = [];
                        shipMap[taskForce] = shipMap1;
                    }

                    string shipName = cellValues[iShipName];
                    if (shipName.Length == 0)
                    {
                        if (taskForce.WarShips.Count == 0)
                        {
                            continue;
                        }
                        else
                        {
                            warShip = taskForce.WarShips.Last();
                        }
                    }
                    else
                    {
                        var ship = shipMap1.GetValueOrDefault(shipName);
                        if (ship is null)
                        {
                            warShip = new();
                            taskForce.WarShips.Add(warShip);
                        }
                        else
                        {
                            warShip = ship;
                        }
                    }

                    for (int j = 0; j < cellValues.Count; j++)
                    {
                        PropertyInfo? property = properties[j];
                        if (property is null)
                        {
                            continue;
                        }

                        string columnValue = cellValues[j];
                        if (columnValue.Length == 0)
                        {
                            continue;
                        }

                        object? obj;
                        object convertedValue = Convert.ChangeType(columnValue, property.PropertyType);
                        if (property.DeclaringType == typeof(Fleet))
                        {
                            obj = fleet;
                        }
                        else if (property.DeclaringType == typeof(TaskForce))
                        {
                            obj = taskForce;
                        }
                        else if (property.DeclaringType == typeof(WarShip))
                        {
                            obj = warShip;
                        }
                        else
                        {
                            obj = null;
                        }

                        if (obj != null)
                        {
                            property.SetValue(obj, convertedValue);
                        }
                    }
                }

                return fleetList;
            }
        }

        private static List<AirBase> LoadAirBases(SheetData sheet, SharedStringTablePart? sharedStringPart, TDir directory)
        {
            List<Row> rows = [.. sheet.Elements<Row>()];
            if (rows.Count == 0)
            {
                return [];
            }

            List<PropertyInfo?> properties = [];
            var headerNames = ConvertRowToCells(rows[0], sharedStringPart, 0);
            for (int i = 0; i < headerNames.Count; i++)
            {
                string rawHeader = headerNames[i];
                string header = directory.GetValueOrDefault("column_name", []).GetValueOrDefault(rawHeader, rawHeader);
                properties.Add(typeof(AirWing).GetProperty(header));
            }

            List<AirWing> airWings = new(rows.Count - 1);
            for (int i = 1; i < rows.Count; i++)
            {
                AirWing airWing = new();
                var columnValues = ConvertRowToCells(rows[i], sharedStringPart, 0);
                for (int j = 0; j < columnValues.Count; j++)
                {
                    if (columnValues[j].Length == 0)
                    {
                        continue;
                    }

                    var property = properties[j];
                    if (property is null)
                    {
                        continue;
                    }

                    property.SetValue(airWing, Convert.ChangeType(columnValues[j], property.PropertyType));
                }

                if (airWing.Location == 0)
                {
                    if (airWings.Count == 0)
                    {
                        continue;
                    }
                    else
                    {
                        airWing.Location = airWings.Last().Location;
                    }
                }
                airWings.Add(airWing);
            }

            List<AirBase> airBases = [];
            Dictionary<int, AirBase> locationMap = [];

            foreach (var airWing in airWings)
            {
                var ab = locationMap.GetValueOrDefault(airWing.Location);
                if (ab is null)
                {
                    ab = new()
                    {
                        Location = airWing.Location
                    };
                    locationMap[airWing.Location] = ab;
                    airBases.Add(ab);
                }

                ab.AirWings.Add(airWing);
            }

            return airBases;
        }

        private static List<string> ConvertRowToCells(Row rowData, SharedStringTablePart? sharedStringPart, int minCount)
        {
            List<string> cellValues = [];
            foreach (var cell in rowData.Elements<Cell>())
            {
                int column = ParseColumnIdFromReference(cell.CellReference!);
                int diff = column - cellValues.Count;
                for (int i = 0; i < diff; i++)
                {
                    cellValues.Add("");
                }

                cellValues.Add(GetStringFromCell(cell, sharedStringPart));
            }

            {
                int diff = minCount - cellValues.Count;
                for (int i = 0; i < diff; i++)
                {
                    cellValues.Add("");
                }
            }

            return cellValues;
        }

        static int ParseColumnIdFromReference(string cellReference)
        {
            var span = cellReference.AsSpan();
            int column = 0;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] >= 'A' && span[i] <= 'Z')
                {
                    column *= 26;
                    column += span[i] - 'A';
                }
                else
                {
                    break;
                }
            }
            return column;
        }

        private static string GetStringFromCell(Cell cell, SharedStringTablePart? sharedStringPart)
        {
            string value = cell.CellValue?.Text ?? "";
            if (sharedStringPart is not null && cell.DataType is not null && cell.DataType == CellValues.SharedString)
            {
                return sharedStringPart.SharedStringTable.ElementAt(int.Parse(value)).InnerText;
            }
            else
            {
                return value;
            }
        }
    }
}
