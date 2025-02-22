using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Hoi4UnitHistoryGenerator.Excel
{
    class ExcelUtils
    {
        public static ExcelIterator RowIterator(SheetData sheet, SharedStringTablePart? sharedStringPart)
        {
            return new ExcelIterator(sheet.Elements<Row>().GetEnumerator(), sharedStringPart);
        }

        public static List<string> ReadRow(Row rowData, SharedStringTablePart? sharedStringPart, int minCount)
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

        private static int ParseColumnIdFromReference(string cellReference)
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
