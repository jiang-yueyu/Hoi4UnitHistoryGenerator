using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Collections;

namespace Hoi4UnitHistoryGenerator.Excel
{
    struct ExcelIterator(
        IEnumerator<Row> rowIterator,
        SharedStringTablePart? sharedStringTablePart
        ) : IEnumerator<List<string>>
    {
        private bool _IsHeaderRowReturned;
        private int _HeaderColumnCount;

        public List<string> Current {
            get {
                var current = ExcelUtils.ReadRow(rowIterator.Current, sharedStringTablePart, _HeaderColumnCount);
                if (!_IsHeaderRowReturned)
                {
                    _IsHeaderRowReturned = true;
                    _HeaderColumnCount = current.Count;
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
