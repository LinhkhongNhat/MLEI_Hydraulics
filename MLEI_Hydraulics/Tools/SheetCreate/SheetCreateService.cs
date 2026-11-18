using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;

namespace MLEI_Hydraulics.Tools.SheetCreate
{
    public sealed class SheetListReadResult
    {
        public List<SheetCreateRow> Rows { get; } = new List<SheetCreateRow>();
        public string Warning { get; set; }
    }

    /// <summary>All Revit and file work for the sheet create tab. No UI code in here.</summary>
    public static class SheetCreateService
    {
        // Characters Revit does not accept in sheet numbers and names
        private const string InvalidChars = "\\:{}[]|;<>?`~";

        private static readonly string[] NumberHeaders = { "number", "no", "sheetnumber", "sheetno", "sheetnum", "num" };
        private static readonly string[] NameHeaders = { "name", "sheetname", "title", "description" };

        public static List<TitleBlockOption> GetTitleBlocks(Document doc)
        {
            List<TitleBlockOption> options = new List<TitleBlockOption> { TitleBlockOption.None };

            options.AddRange(
                new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsElementType()
                    .Cast<FamilySymbol>()
                    .OrderBy(s => s.FamilyName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(s => new TitleBlockOption(s.Id, $"{s.FamilyName} : {s.Name}")));

            return options;
        }

        public static HashSet<string> GetExistingSheetNumbers(Document doc)
        {
            return new HashSet<string>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Select(s => s.SheetNumber),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads the CSV and checks every line. Understands a header row ("Sheet number", "Sheet name")
        /// or, without a header, takes column 1 as the number and column 2 as the name.
        /// </summary>
        public static SheetListReadResult ReadSheetList(string path, ISet<string> existingNumbers)
        {
            string text;

            // FileShare.ReadWrite so it still opens while Excel has the file open
            using (System.IO.FileStream stream = new System.IO.FileStream(
                path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
            using (System.IO.StreamReader reader = new System.IO.StreamReader(stream, Encoding.UTF8, true))
            {
                text = reader.ReadToEnd();
            }

            SheetListReadResult result = new SheetListReadResult();

            if (text.IndexOf('\uFFFD') >= 0)
            {
                result.Warning = "Some characters could not be read. In Excel use Save As > CSV UTF-8 (Comma delimited).";
            }

            List<List<string>> table = Csv.Parse(text)
                .Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c)))
                .ToList();

            if (table.Count == 0)
                return result;

            int numberCol = 0, nameCol = 1, firstDataRow = 0;
            int headerNumber = FindColumn(table[0], NumberHeaders);
            int headerName = FindColumn(table[0], NameHeaders);
            if (headerNumber >= 0 && headerName >= 0 && headerNumber != headerName)
            {
                numberCol = headerNumber;
                nameCol = headerName;
                firstDataRow = 1;
            }

            HashSet<string> seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = firstDataRow; i < table.Count; i++)
            {
                string number = Cell(table[i], numberCol);
                string name = Cell(table[i], nameCol);
                SheetCreateRow row = new SheetCreateRow(number, name);

                if (number.Length == 0)
                    row.SetStatus(SheetRowStatus.MissingNumber);
                else if (name.Length == 0)
                    row.SetStatus(SheetRowStatus.MissingName);
                else if (HasInvalidChars(number) || HasInvalidChars(name))
                    row.SetStatus(SheetRowStatus.InvalidCharacters);
                else if (!seenInFile.Add(number))
                    row.SetStatus(SheetRowStatus.DuplicateInFile);
                else if (existingNumbers.Contains(number))
                    row.SetStatus(SheetRowStatus.AlreadyExists);

                result.Rows.Add(row);
            }

            return result;
        }

        /// <summary>
        /// Creates the given rows as sheets in one transaction. A sheet that fails is rolled back
        /// on its own, so one bad line never blocks the rest.
        /// </summary>
        public static SheetCreateResult Create(Document doc, IList<SheetCreateRow> rows, TitleBlockOption titleBlock)
        {
            SheetCreateResult result = new SheetCreateResult();

            using (Transaction transaction = new Transaction(doc, "Create sheets"))
            {
                transaction.Start();

                // A title block type that was never used can be inactive
                if (!titleBlock.IsNone && doc.GetElement(titleBlock.Id) is FamilySymbol symbol && !symbol.IsActive)
                    symbol.Activate();

                ElementId titleBlockId = titleBlock.IsNone ? ElementId.InvalidElementId : titleBlock.Id;

                foreach (SheetCreateRow row in rows)
                {
                    using (SubTransaction sub = new SubTransaction(doc))
                    {
                        sub.Start();
                        try
                        {
                            ViewSheet sheet = ViewSheet.Create(doc, titleBlockId);
                            sheet.SheetNumber = row.Number;
                            sheet.Name = row.Name;
                            sub.Commit();

                            row.SetStatus(SheetRowStatus.Created);
                            result.Created++;
                        }
                        catch (Exception ex)
                        {
                            sub.RollBack();
                            row.SetStatus(SheetRowStatus.Failed, "Failed: " + ex.Message);
                            result.Failed++;
                        }
                    }
                }

                transaction.Commit();
            }

            return result;
        }

        /// <summary>Writes an example CSV (with a BOM so Excel shows accents correctly).</summary>
        public static void WriteTemplate(string path)
        {
            string content =
                "Sheet number,Sheet name\r\n" +
                "A-101,Ground floor plan\r\n" +
                "A-102,First floor plan\r\n";

            System.IO.File.WriteAllText(path, content, new UTF8Encoding(true));
        }

        #region helpers

        private static string Cell(List<string> cells, int index)
            => index < cells.Count ? cells[index].Trim() : "";

        private static bool HasInvalidChars(string value)
            => value.IndexOfAny(InvalidChars.ToCharArray()) >= 0;

        /// <summary>Index of the first header cell whose letters match one of the names, or -1.</summary>
        private static int FindColumn(List<string> headerRow, string[] names)
        {
            for (int i = 0; i < headerRow.Count; i++)
            {
                string normalized = new string(headerRow[i].ToLowerInvariant().Where(char.IsLetter).ToArray());
                if (names.Contains(normalized))
                    return i;
            }

            return -1;
        }

        #endregion
    }
}
