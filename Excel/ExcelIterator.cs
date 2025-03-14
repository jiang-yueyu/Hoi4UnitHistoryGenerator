using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Collections;

namespace Hoi4UnitHistoryGenerator.Excel
{
    struct ExcelIterator(
        IEnumerator<Row> rowIterator,
        SharedStringTablePart? sharedStringTablePart
        ) : IEnumerator<List<KeyValuePair<int, string>>>
    {
        private bool _IsHeaderRowReturned;

        public List<KeyValuePair<int, string>> Current {
            get {
                var current = ExcelUtils.ReadRow(rowIterator.Current, sharedStringTablePart);
                if (!_IsHeaderRowReturned)
                {
                    _IsHeaderRowReturned = true;
                }
                return current;
            }
        }

        object IEnumerator.Current => Current;

        public readonly void Dispose()
        {
        }

        public readonly bool MoveNext()
        {
            return rowIterator.MoveNext();
        }

        public void Reset()
        {
            _IsHeaderRowReturned = false;
            rowIterator.Reset();
        }
    }
}
