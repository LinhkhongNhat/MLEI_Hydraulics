using System.Collections.Generic;
using System.Text;

namespace MLEI_Hydraulics
{
    /// <summary>
    /// Small CSV reader. Handles quoted fields, "" escapes, line breaks inside quotes,
    /// and picks the delimiter automatically (Excel uses ; instead of , in many regions).
    /// </summary>
    public static class Csv
    {
        public static List<List<string>> Parse(string text)
        {
            text = (text ?? "").TrimStart('\uFEFF');
            char delimiter = DetectDelimiter(text);

            List<List<string>> rows = new List<List<string>>();
            List<string> row = new List<string>();
            StringBuilder field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == delimiter)
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else
                {
                    field.Append(c);
                }
            }

            // Last line without a trailing line break
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// Writes rows to a CSV file as UTF-8 with a BOM. Use the delimiter Excel expects on this PC
        /// (CultureInfo.CurrentCulture.TextInfo.ListSeparator) so a double-click opens it in columns.
        /// </summary>
        public static void Write(string path, IEnumerable<IList<string>> rows, string delimiter)
        {
            StringBuilder sb = new StringBuilder();

            foreach (IList<string> row in rows)
            {
                for (int i = 0; i < row.Count; i++)
                {
                    if (i > 0) sb.Append(delimiter);
                    sb.Append(Escape(row[i] ?? "", delimiter));
                }
                sb.Append("\r\n");
            }

            System.IO.File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static string Escape(string value, string delimiter)
        {
            bool needsQuotes = value.Contains(delimiter) || value.Contains("\"") || value.Contains("\r") || value.Contains("\n");
            return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        /// <summary>Looks at the first line and picks the most frequent of , ; and tab (outside quotes).</summary>
        private static char DetectDelimiter(string text)
        {
            int commas = 0, semicolons = 0, tabs = 0;
            bool inQuotes = false;

            foreach (char c in text)
            {
                if (c == '"') inQuotes = !inQuotes;
                else if (!inQuotes)
                {
                    if (c == '\r' || c == '\n') break;
                    if (c == ',') commas++;
                    else if (c == ';') semicolons++;
                    else if (c == '\t') tabs++;
                }
            }

            if (semicolons > commas && semicolons >= tabs) return ';';
            if (tabs > commas && tabs > semicolons) return '\t';
            return ',';
        }
    }
}
