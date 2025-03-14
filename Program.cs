﻿using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Hoi4UnitHistoryGenerator.Attributes;
using Hoi4UnitHistoryGenerator.Model;
using Hoi4UnitHistoryGenerator.Excel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Hoi4UnitHistoryGenerator
{
    using TDir = Dictionary<string, Dictionary<string, string>>;

    partial class Entrypoint
    {
        [GeneratedRegex("^([A-Z]{3})\\.xlsx")]
        private static partial Regex RegTemplateFileName();

        static void Main()
        {
            TDir dictionaries = LoadDictionaries("dictionaries.xlsx");
            string[] files = Directory.GetFiles(".", "*.xlsx");
            foreach (var item in files)
            {
                var match = RegTemplateFileName().Match(Path.GetFileName(item));
                if (match.Success)
                {
                    string countryTag = match.Groups[1].Value;
                    try
                    {
                        Console.Out.WriteLine($"Handling xlsx file: {item}");
                        HandleCountryXlsx(countryTag, item, dictionaries);
                        Console.Out.WriteLine("Success.");
                    }
                    catch (Exception ex)
                    {
                        Console.Out.WriteLine($"Fail: {ex}");
                    }
                }
            }
        }

        private static TDir LoadDictionaries(string filename)
        {
            using var doc = SpreadsheetDocument.Open(filename, false);
            if (doc.WorkbookPart is null)
            {
                Console.Error.WriteLine($"{filename} - Illegal xlsx: workbook part is missing");
                return [];
            }

            SharedStringTablePart? sharedStringPart = doc.WorkbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
            var workbook = doc.WorkbookPart!.Workbook;
            if (workbook.Sheets is null)
            {
                Console.Error.WriteLine($"{filename} - Illegal xlsx: sheet part is missing");
                return [];
            }

            TDir results = [];
            foreach (var sheet in workbook.Sheets.Elements<Sheet>())
            {
                var id = sheet.Id;
                WorksheetPart worksheetPart = (doc.WorkbookPart.GetPartById(sheet.Id!) as WorksheetPart)!;
                SheetData sheetData = worksheetPart.Worksheet.Elements<SheetData>().FirstOrDefault()!;

                var name = sheet.Name?.Value;
                if (name is not null)
                {
                    results[name] = LoadDictionary(sheetData, sharedStringPart);
                }
            }
            return results;
        }

        private static Dictionary<string, string> LoadDictionary(SheetData sheet, SharedStringTablePart? sharedStringPart)
        {
            var rowIterator = ExcelUtils.RowIterator(sheet, sharedStringPart);
            if (!rowIterator.MoveNext())
            {
                return [];
            }

            List<KeyValuePair<int, string>> headers = rowIterator.Current;

            int iName = -1;
            int iId = -1;

            foreach (var (i, header) in headers)
            {
                if (header == "name")
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

            Dictionary<string, string> results = [];

            while (rowIterator.MoveNext())
            {
                var cellValues = rowIterator.Current;
                
                string name = "";
                string id = "";

                foreach (var (j, value) in cellValues)
                {
                    if (iName == j)
                    {
                        name = value;
                    }
                    if (iId == j)
                    {
                        id = value;
                    }
                }

                results[name] = id;
            }

            return results;
        }

        private static void HandleCountryXlsx(string countryTag, string filename, TDir dictionaries)
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

            List<DivisionTemplate> divisionTemplates = [];
            List<DivisionEntity> divisionEntities = [];
            List<EquipmentVariant> armorVariants = [];
            List<EquipmentVariant> planeVariants = [];
            List<EquipmentVariant> shipVariants = [];
            List<Fleet> fleets = [];
            List<AirBase> airBases = [];

            foreach (var sheet in workbook.Sheets.Elements<Sheet>())
            {
                var id = sheet.Id;
                WorksheetPart worksheetPart = (doc.WorkbookPart.GetPartById(sheet.Id!) as WorksheetPart)!;
                SheetData sheetData = worksheetPart.Worksheet.Elements<SheetData>().FirstOrDefault()!;

                var name = sheet.Name;
                if ("division_entities" == name)
                {
                    divisionEntities = LoadDivisionEntities(sheetData, sharedStringPart, dictionaries);
                }
                else if ("armor_variants" == name)
                {
                    armorVariants = LoadEquipmentVariants(sheetData, sharedStringPart, dictionaries);
                }
                else if ("plane_variants" == name)
                {
                    planeVariants = LoadEquipmentVariants(sheetData, sharedStringPart, dictionaries);
                }
                else if ("ship_variants" == name)
                {
                    shipVariants = LoadEquipmentVariants(sheetData, sharedStringPart, dictionaries);
                }
                else if ("fleets" == name)
                {
                    fleets = LoadFleets(sheetData, sharedStringPart, dictionaries);
                }
                else if ("air_wings" == name)
                {
                    airBases = LoadAirBases(sheetData, sharedStringPart, dictionaries);
                }
                else if ("division_templates" == name)
                {
                    divisionTemplates = LoadDivisionTemplates(sheetData, sharedStringPart, dictionaries);
                }
            }

            Directory.CreateDirectory("output/history/units");
            Directory.CreateDirectory("output/common/units/names_divisions");

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
                    divisionTemplate.Print(textWriter, countryTag);
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

            using (TextWriter textWriter = new StreamWriter(new FileStream($"output/history/units/{countryTag}_naval.txt", FileMode.Create, FileAccess.Write, FileShare.None)))
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

            using (TextWriter textWriter = new StreamWriter(new FileStream($"output/common/units/names_divisions/{countryTag}_generated.txt", FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                foreach (var divisionTemplate in divisionTemplates)
                {
                    if (divisionTemplate.DivisionNameGroup.Length > 0)
                    {
                        divisionTemplate.PrintGeneratedNameGroup(textWriter, countryTag);
                        textWriter.WriteLine(" = {");

                        textWriter.WriteLine($"\tname = \"{divisionTemplate.Name}\"");
                        textWriter.WriteLine($"\tfor_countries = {{ {countryTag} }}");
                        textWriter.WriteLine("\tcan_use = { always = yes }");
                        textWriter.WriteLine($"\tfallback_name = \"{divisionTemplate.DivisionNameGroup}\"");

                        textWriter.WriteLine("}");

                        textWriter.WriteLine();
                    }
                }
            }
        }

        private static List<DivisionTemplate> LoadDivisionTemplates(SheetData sheet, SharedStringTablePart? sharedStringPart, TDir directory)
        {
            var iterator = sheet.Elements<Row>().GetEnumerator();

            List<DivisionTemplate> results = [];
            DivisionTemplate? divisionTemplate;
            while ((divisionTemplate = LoadOneDivisionTemplate(iterator, sharedStringPart, directory)) is not null)
            {
                divisionTemplate.ID = results.Count;
                results.Add(divisionTemplate);
            }
            return results;
        }

        private static DivisionTemplate? LoadOneDivisionTemplate(IEnumerator<Row> iterator, SharedStringTablePart? sharedStringPart, TDir directory)
        {
            if (!iterator.MoveNext())
            {
                return null;
            }
            Row headerRow = iterator.Current;
            List<string?> rawHeaders = ExcelUtils.ReadRowToStringList(headerRow, sharedStringPart);
            if (rawHeaders.Count == 0)
            {
                return null;
            }
            
            List<List<string>> columns = [];

            while (iterator.MoveNext())
            {
                Row row = iterator.Current;
                var cellValues = ExcelUtils.ReadRow(row, sharedStringPart);
                if ("!" == cellValues.FirstOrDefault().Value)
                {
                    break;
                }

                foreach (var (j, value) in cellValues)
                {
                    if (j >= rawHeaders.Count)
                    {
                        continue;
                    }

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

                    column.Add(value);
                }
            }

            DivisionTemplate divisionTemplate = new();
            for (int i = 0; i < columns.Count; i++)
            {
                string? rawHeader = rawHeaders[i];
                if (rawHeader == null)
                {
                    continue;
                }

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
            var rowIterator = ExcelUtils.RowIterator(sheet, sharedStringPart);
            if (!rowIterator.MoveNext())
            {
                return [];
            }

            List<PropertyInfo?> properties = [];
            List<string?> propertyLocalisationKeys = [];
            {
                List<string?> headerNames = ExcelUtils.ConvertCellsToStringList(rowIterator.Current);
                foreach (var rawHeader in headerNames)
                {
                    if (rawHeader is null)
                    {
                        continue;
                    }

                    string header = directory.GetValueOrDefault("column_name", []).GetValueOrDefault(rawHeader, rawHeader);
                    var property = typeof(DivisionEntity).GetProperty(header);
                    if (property is null)
                    {
                        Console.WriteLine($"[Warn] unknown property: {nameof(DivisionEntity)}.{header}");
                    }
                    properties.Add(property);
                    propertyLocalisationKeys.Add(BuildCategoryKey(property));
                }
            }

            List<DivisionEntity> results = [];
            while (rowIterator.MoveNext())
            {
                DivisionEntity divisionEntity = new();
                var cellValues = rowIterator.Current;
                foreach (var (j, value) in cellValues)
                {
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    var property = properties[j];
                    if (property is not null)
                    {
                        string propertyValue = directory.GetValueOrDefault(propertyLocalisationKeys[j]!, []).GetValueOrDefault(value, value);
                        property.SetValue(divisionEntity, Convert.ChangeType(propertyValue, property.PropertyType));
                    }
                }

                results.Add(divisionEntity);
            }

            return results;
        }

        private static List<EquipmentVariant> LoadEquipmentVariants(SheetData sheet, SharedStringTablePart? sharedStringPart, TDir directory)
        {
            var rowIterator = ExcelUtils.RowIterator(sheet, sharedStringPart);
            if (!rowIterator.MoveNext())
            {
                return [];
            }

            List<PropertyInfo?> properties = [];
            List<string?> propertyLocalisationKeys = [];
            List<string> headers = [];
            int iVariantName = -1;
            {
                List<string?> headerNames = ExcelUtils.ConvertCellsToStringList(rowIterator.Current);
                for (int i = 0; i < headerNames.Count; i++)
                {
                    string? rawHeader = headerNames[i];
                    if (rawHeader is null)
                    {
                        continue;
                    }

                    string header = directory.GetValueOrDefault("column_name", []).GetValueOrDefault(rawHeader, rawHeader);
                    headers.Add(header);

                    if ("Name" == header)
                    {
                        iVariantName = i;
                    }

                    var propertyInfo = typeof(EquipmentVariant).GetProperty(header);
                    properties.Add(propertyInfo);
                    propertyLocalisationKeys.Add(BuildCategoryKey(propertyInfo));
                }
            }

            if (iVariantName == -1)
            {
                return [];
            }

            Dictionary<string, EquipmentVariant> resultMap = [];
            List<EquipmentVariant> resultList = [];
            while (rowIterator.MoveNext())
            {
                EquipmentVariant equipmentVariant;

                var cellValues = rowIterator.Current;
                string variantName = cellValues.Where(pair => pair.Key == iVariantName).FirstOrDefault().Value ?? "";
                if (variantName.Length == 0)
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
                    var sv = resultMap.GetValueOrDefault(variantName);
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
                foreach (var (j, columnValue) in cellValues)
                {
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
                            slotName = directory.GetValueOrDefault("equipment_slot", []).GetValueOrDefault(columnValue, columnValue);
                        }
                        else if ("Equipment" == column)
                        {
                            equipment = directory.GetValueOrDefault("equipment_model", []).GetValueOrDefault(columnValue, columnValue);
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
                        string propertyValue = directory.GetValueOrDefault(propertyLocalisationKeys[j]!, []).GetValueOrDefault(columnValue, columnValue);
                        propertyInfo.SetValue(equipmentVariant, Convert.ChangeType(propertyValue, propertyInfo.PropertyType));
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
            var rowIterator = ExcelUtils.RowIterator(sheet, sharedStringPart);
            if (!rowIterator.MoveNext())
            {
                return [];
            }

            List<PropertyInfo?> properties = [];
            List<string?> propertyLocalisationKeys = [];
            int iFleetName = -1;
            int iTaskForceName = -1;
            int iShipName = -1;
            {
                var headerNames = ExcelUtils.ConvertCellsToStringList(rowIterator.Current);
                for (int i = 0; i < headerNames.Count; i++)
                {
                    string? rawHeader = headerNames[i];
                    if (rawHeader is null)
                    {
                        continue;
                    }

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
                        var property = typeof(WarShip).GetProperty(propertyName);
                        properties.Add(property);
                        propertyLocalisationKeys.Add(BuildCategoryKey(property));
                    }
                    else if (header.StartsWith("TaskForce."))
                    {
                        string propertyName = header["TaskForce.".Length..];
                        var property = typeof(TaskForce).GetProperty(propertyName);
                        properties.Add(property);
                        propertyLocalisationKeys.Add(BuildCategoryKey(property));
                    }
                    else
                    {
                        var property = typeof(Fleet).GetProperty(header);
                        properties.Add(property);
                        propertyLocalisationKeys.Add(BuildCategoryKey(property));
                    }
                }

                if (iFleetName == -1 || iTaskForceName == -1 || iShipName == -1)
                {
                    return [];
                }
            }

            Dictionary<string, Fleet> fleetMap = [];
            List<Fleet> fleetList = [];

            Dictionary<Fleet, Dictionary<string, TaskForce>> taskForceMap = [];
            Dictionary<TaskForce, Dictionary<string, WarShip>> shipMap = [];

            int currentRowId = 1;
            while (rowIterator.MoveNext())
            {
                currentRowId++;
                var cellValues = rowIterator.Current;
                Fleet fleet;
                TaskForce taskForce;

                string fleetName = cellValues.Where(pair => pair.Key == iFleetName).FirstOrDefault().Value ?? "";
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
                        fleetMap[fleetName] = fleet;
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

                string taskForceName = cellValues.Where(pair => pair.Key == iTaskForceName).FirstOrDefault().Value ?? "";
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
                        taskForceMap1[taskForceName] = taskForce;
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

                string shipName = cellValues.Where(pair => pair.Key == iShipName).FirstOrDefault().Value ?? "";
                if (shipName.Length == 0)
                {
                    continue;
                }

                var ship = shipMap1.GetValueOrDefault(shipName);
                if (ship is not null)
                {
                    Console.WriteLine($"[Warn] duplicate ship name [shipName] row {currentRowId}");
                    continue;
                }

                WarShip warShip = new();
                taskForce.WarShips.Add(warShip);
                shipMap1[shipName] = warShip;

                foreach (var (j, columnValue) in cellValues)
                {
                    PropertyInfo? property = properties[j];
                    if (property is null)
                    {
                        continue;
                    }

                    if (columnValue.Length == 0)
                    {
                        continue;
                    }

                    object? obj;
                    string propertyValue = directory.GetValueOrDefault(propertyLocalisationKeys[j]!, []).GetValueOrDefault(columnValue, columnValue);
                    object convertedValue = Convert.ChangeType(propertyValue, property.PropertyType);
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

        private static List<AirBase> LoadAirBases(SheetData sheet, SharedStringTablePart? sharedStringPart, TDir directory)
        {
            var rowIterator = ExcelUtils.RowIterator(sheet, sharedStringPart);
            if (!rowIterator.MoveNext())
            {
                return [];
            }

            List<PropertyInfo?> properties = [];
            List<string?> propertyLocalisationKeys = [];
            {
                var headerNames = ExcelUtils.ConvertCellsToStringList(rowIterator.Current);
                for (int i = 0; i < headerNames.Count; i++)
                {
                    string? rawHeader = headerNames[i];
                    if (rawHeader is null)
                    {
                        continue;
                    }

                    string header = directory.GetValueOrDefault("column_name", []).GetValueOrDefault(rawHeader, rawHeader);

                    var property = typeof(AirWing).GetProperty(header);
                    properties.Add(property);
                    propertyLocalisationKeys.Add(BuildCategoryKey(property));
                }
            }

            List<AirWing> airWings = [];
            while (rowIterator.MoveNext())
            {
                AirWing airWing = new();
                var columnValues = rowIterator.Current;
                foreach (var (j, value) in columnValues)
                {
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    var property = properties[j];
                    if (property is null)
                    {
                        continue;
                    }

                    string propertyValue = directory.GetValueOrDefault(propertyLocalisationKeys[j]!, []).GetValueOrDefault(value, value);
                    property.SetValue(airWing, Convert.ChangeType(propertyValue, property.PropertyType));
                }

                if (airWing.Amount == 0)
                {
                    continue;
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

        private static string? BuildCategoryKey(PropertyInfo? propertyInfo)
        {
            if (propertyInfo is null)
            {
                return null;
            }
            var attribute = propertyInfo.GetCustomAttribute<LocalisationReferenceAttribute>();
            return attribute is null ? $"{propertyInfo.DeclaringType!.Name}.{propertyInfo.Name}" : attribute.LocalisationKey;
        }
    }
}
