using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;

namespace MLEI_Hydraulics.Tools.ScheduleSync
{
    /// <summary>
    /// Exports a schedule's elements to CSV and applies edited values back.
    /// Rows are matched by Element ID, never by row position, so sorting the CSV in Excel is safe.
    /// </summary>
    public static class ScheduleSyncService
    {
        public const string ElementIdHeader = "Element ID";

        #region Reading the project

        public static List<ScheduleOption> GetSchedules(Document doc)
        {
            List<ScheduleOption> list = new List<ScheduleOption>();

            foreach (ViewSchedule s in new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>())
            {
                if (s.IsTemplate || s.IsTitleblockRevisionSchedule || s.IsInternalKeynoteSchedule)
                    continue;

                list.Add(new ScheduleOption(s.Id, s.Name, GetCategoryName(doc, s)));
            }

            return list.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>The elements the schedule currently shows (its filters are respected), one entry per element.</summary>
        public static List<Element> GetElements(Document doc, ViewSchedule schedule)
        {
            return new FilteredElementCollector(doc, schedule.Id)
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();
        }

        /// <summary>Works out, for every visible field of the schedule, whether it can be exported and edited.</summary>
        public static List<ScheduleColumn> BuildColumns(
            Document doc, ViewSchedule schedule, IList<Element> elements, bool allowTypeEdit)
        {
            List<ScheduleColumn> columns = new List<ScheduleColumn>();
            HashSet<string> usedHeadings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ScheduleDefinition definition = schedule.Definition;

            for (int i = 0; i < definition.GetFieldCount(); i++)
            {
                ScheduleField field = definition.GetField(i);
                if (field.IsHidden)
                    continue;

                string heading = !string.IsNullOrWhiteSpace(field.ColumnHeading) ? field.ColumnHeading : field.GetName();
                heading = MakeUnique(heading.Trim(), usedHeadings);

                bool isInstance = field.FieldType == ScheduleFieldType.Instance;
                bool isType = field.FieldType == ScheduleFieldType.ElementType;

                if (!isInstance && !isType)
                {
                    columns.Add(new ScheduleColumn(heading, null, false, false, false, "Not exported: calculated field"));
                    continue;
                }

                bool editable = false;
                foreach (Element e in elements)
                {
                    Parameter p = GetParameter(TargetFor(doc, e, isType), field.ParameterId);
                    if (p != null && IsEditable(p))
                    {
                        editable = true;
                        break;
                    }
                }

                string status;
                if (isType && !allowTypeEdit)
                {
                    editable = false;
                    status = "Read-only (type parameter)";
                }
                else
                {
                    status = editable ? "Editable" : "Read-only";
                }

                columns.Add(new ScheduleColumn(heading, field.ParameterId, isType, true, editable, status));
            }

            return columns;
        }

        #endregion

        #region Export

        /// <summary>Writes the elements and the ticked columns to a CSV file.</summary>
        public static void Export(Document doc, IList<Element> elements, IList<ScheduleColumn> columns, string path)
        {
            List<ScheduleColumn> included = columns.Where(c => c.IsSupported && c.IsIncluded).ToList();

            List<IList<string>> rows = new List<IList<string>>();

            List<string> header = new List<string> { ElementIdHeader };
            header.AddRange(included.Select(c => c.ExportHeader));
            rows.Add(header);

            List<List<string>> body = new List<List<string>>();
            foreach (Element e in elements)
            {
                List<string> row = new List<string> { e.Id.Value.ToString(CultureInfo.InvariantCulture) };
                foreach (ScheduleColumn c in included)
                    row.Add(ReadText(GetParameter(TargetFor(doc, e, c.IsTypeParameter), c.ParameterId)));
                body.Add(row);
            }

            // Sort by the first exported column so the file is easy to read
            foreach (List<string> row in body
                .OrderBy(r => r.Count > 1 ? r[1] : "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => long.Parse(r[0], CultureInfo.InvariantCulture)))
            {
                rows.Add(row);
            }

            // The delimiter Excel expects on this PC (it is ; in many regions)
            string delimiter = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
            Csv.Write(path, rows, delimiter);
        }

        #endregion

        #region Import

        /// <summary>
        /// Compares the CSV with the model and returns only the values that differ.
        /// Nothing is changed in the model here.
        /// </summary>
        public static ScheduleImportResult ReadImport(
            Document doc, string path, IList<ScheduleColumn> columns, IList<Element> elements)
        {
            string text;
            using (System.IO.FileStream stream = new System.IO.FileStream(
                path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
            using (System.IO.StreamReader reader = new System.IO.StreamReader(stream, Encoding.UTF8, true))
            {
                text = reader.ReadToEnd();
            }

            ScheduleImportResult result = new ScheduleImportResult();
            List<string> warnings = new List<string>();

            if (text.IndexOf('\uFFFD') >= 0)
                warnings.Add("Some characters could not be read. Save the CSV as UTF-8.");

            List<List<string>> table = Csv.Parse(text)
                .Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c)))
                .ToList();

            if (table.Count < 2)
            {
                result.Warning = "The file has no data rows.";
                return result;
            }

            List<string> header = table[0];

            int idColumn = -1;
            for (int i = 0; i < header.Count; i++)
            {
                string h = Normalize(header[i]);
                if (h == "element id" || h == "elementid" || h == "id")
                {
                    idColumn = i;
                    break;
                }
            }

            if (idColumn < 0)
                throw new InvalidOperationException(
                    "The file has no \"Element ID\" column. Use a file exported from this tool.");

            // Which CSV column belongs to which schedule field (matched by heading)
            Dictionary<int, ScheduleColumn> map = new Dictionary<int, ScheduleColumn>();
            List<string> ignored = new List<string>();

            for (int i = 0; i < header.Count; i++)
            {
                if (i == idColumn || string.IsNullOrWhiteSpace(header[i]))
                    continue;

                string wanted = Normalize(header[i]);
                ScheduleColumn match = columns.FirstOrDefault(c => c.IsSupported && Normalize(c.Heading) == wanted);

                if (match != null) map[i] = match;
                else ignored.Add(header[i].Trim());
            }

            if (map.Count == 0)
            {
                result.Warning = "None of the columns match this schedule. Is the right schedule selected?";
                return result;
            }

            if (ignored.Count > 0)
                warnings.Add("Ignored columns: " + string.Join(", ", ignored));

            Dictionary<long, Element> byId = elements.ToDictionary(e => e.Id.Value);
            Dictionary<string, HashSet<string>> typeValues = new Dictionary<string, HashSet<string>>();

            for (int r = 1; r < table.Count; r++)
            {
                List<string> row = table[r];
                result.RowCount++;

                string idText = Cell(row, idColumn);

                if (!long.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long idValue) ||
                    !byId.TryGetValue(idValue, out Element element))
                {
                    ScheduleChange missing = new ScheduleChange(idText, "", "(whole row)", "", "", null, null);
                    missing.SetStatus(ScheduleChangeStatus.NotInSchedule);
                    result.Changes.Add(missing);
                    continue;
                }

                foreach (KeyValuePair<int, ScheduleColumn> pair in map)
                {
                    ScheduleColumn column = pair.Value;
                    if (!column.IsEditable)
                        continue; // edits in read-only columns are ignored

                    string newText = Cell(row, pair.Key);
                    Element target = TargetFor(doc, element, column.IsTypeParameter);
                    Parameter p = GetParameter(target, column.ParameterId);

                    string typeKey = null;
                    if (column.IsTypeParameter && target != null)
                    {
                        typeKey = target.Id.Value + "|" + column.Heading;
                        if (!typeValues.TryGetValue(typeKey, out HashSet<string> seen))
                            typeValues[typeKey] = seen = new HashSet<string>();
                        seen.Add(newText);
                    }

                    if (p == null)
                    {
                        if (newText.Length > 0)
                            AddProblem(result, idText, element, column, "", newText, ScheduleChangeStatus.NoParameter);
                        continue;
                    }

                    string oldText = ReadText(p).Trim();
                    if (string.Equals(oldText, newText, StringComparison.Ordinal))
                        continue;

                    ScheduleChange change = new ScheduleChange(
                        idText, element.Name, column.Heading, oldText, newText, target.Id, column.ParameterId)
                    {
                        TypeKey = typeKey
                    };

                    if (!IsEditable(p))
                        change.SetStatus(ScheduleChangeStatus.ReadOnly);
                    else if (newText.Length == 0 && p.StorageType != StorageType.String)
                        change.SetStatus(ScheduleChangeStatus.BlankNotAllowed);

                    result.Changes.Add(change);
                }
            }

            // Type parameters: rows of the same type must agree, and only one change per type is kept
            HashSet<string> emitted = new HashSet<string>();
            result.Changes.RemoveAll(c =>
            {
                if (c.TypeKey == null)
                    return false;

                if (typeValues[c.TypeKey].Count > 1)
                {
                    c.SetStatus(ScheduleChangeStatus.Conflict);
                    return false;
                }

                return c.IsReady && !emitted.Add(c.TypeKey); // drop repeats of the same type change
            });

            if (warnings.Count > 0)
                result.Warning = string.Join("  ", warnings);

            return result;
        }

        /// <summary>Applies the given changes in one transaction. Each change reports its own result.</summary>
        public static void Apply(Document doc, IList<ScheduleChange> changes)
        {
            using (Transaction transaction = new Transaction(doc, "Import schedule values"))
            {
                transaction.Start();

                foreach (ScheduleChange change in changes)
                {
                    try
                    {
                        Parameter p = GetParameter(doc.GetElement(change.TargetId), change.ParameterId);

                        if (SetValue(p, change.NewValue))
                            change.SetStatus(ScheduleChangeStatus.Applied);
                        else
                            change.SetStatus(ScheduleChangeStatus.Failed, "Revit did not accept this value");
                    }
                    catch (Exception ex)
                    {
                        change.SetStatus(ScheduleChangeStatus.Failed, "Failed: " + ex.Message);
                    }
                }

                transaction.Commit();
            }
        }

        private static void AddProblem(ScheduleImportResult result, string idText, Element element,
                                       ScheduleColumn column, string oldText, string newText, ScheduleChangeStatus status)
        {
            ScheduleChange change = new ScheduleChange(idText, element.Name, column.Heading, oldText, newText, null, null);
            change.SetStatus(status);
            result.Changes.Add(change);
        }

        #endregion

        #region Parameter helpers

        private static Element TargetFor(Document doc, Element e, bool isTypeParameter)
            => isTypeParameter ? doc.GetElement(e.GetTypeId()) : e;

        /// <summary>Finds the parameter a schedule field points at (built-in, shared or project parameter).</summary>
        private static Parameter GetParameter(Element element, ElementId parameterId)
        {
            if (element == null || parameterId == null || parameterId.Value == -1)
                return null;

            if (parameterId.Value < 0)
                return element.get_Parameter((BuiltInParameter)(int)parameterId.Value);

            Element parameterElement = element.Document.GetElement(parameterId);

            if (parameterElement is SharedParameterElement shared)
                return element.get_Parameter(shared.GuidValue);

            if (parameterElement is ParameterElement project)
                return element.LookupParameter(project.GetDefinition().Name);

            return null;
        }

        private static bool IsEditable(Parameter p)
            => !p.IsReadOnly && p.StorageType != StorageType.ElementId && p.StorageType != StorageType.None;

        /// <summary>The value as the user sees it in Revit (with the project's units).</summary>
        private static string ReadText(Parameter p)
        {
            if (p == null || !p.HasValue)
                return "";

            if (p.StorageType == StorageType.String)
                return p.AsString() ?? "";

            string formatted = p.AsValueString();
            if (!string.IsNullOrEmpty(formatted))
                return formatted;

            if (p.StorageType == StorageType.Integer)
                return p.AsInteger().ToString(CultureInfo.InvariantCulture);

            if (p.StorageType == StorageType.Double)
                return p.AsDouble().ToString(CultureInfo.CurrentCulture);

            return "";
        }

        private static bool SetValue(Parameter p, string text)
        {
            if (p == null || !IsEditable(p))
                return false;

            switch (p.StorageType)
            {
                case StorageType.String:
                    return p.Set(text);

                case StorageType.Double:
                    return p.SetValueString(text);

                case StorageType.Integer:
                    if (IsYesNo(p))
                    {
                        bool? yes = ParseYesNo(text);
                        return yes.HasValue && p.Set(yes.Value ? 1 : 0);
                    }

                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                        return p.Set(number);

                    return p.SetValueString(text);

                default:
                    return false;
            }
        }

        private static bool IsYesNo(Parameter p)
        {
            try
            {
                return p.Definition.GetDataType().Equals(SpecTypeId.Boolean.YesNo);
            }
            catch
            {
                return false;
            }
        }

        private static bool? ParseYesNo(string text)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "yes":
                case "y":
                case "true":
                case "1":
                    return true;
                case "no":
                case "n":
                case "false":
                case "0":
                    return false;
                default:
                    return null;
            }
        }

        #endregion

        #region Small helpers

        private static string GetCategoryName(Document doc, ViewSchedule schedule)
        {
            try
            {
                Category category = Category.GetCategory(doc, schedule.Definition.CategoryId);
                return category != null ? category.Name : "Multi-category";
            }
            catch
            {
                return "Multi-category";
            }
        }

        private static string MakeUnique(string heading, HashSet<string> used)
        {
            string candidate = heading;
            int n = 2;
            while (!used.Add(candidate))
                candidate = $"{heading} ({n++})";
            return candidate;
        }

        private static string Normalize(string header)
        {
            string h = (header ?? "").Trim();
            if (h.EndsWith(ScheduleColumn.ReadOnlySuffix, StringComparison.OrdinalIgnoreCase))
                h = h.Substring(0, h.Length - ScheduleColumn.ReadOnlySuffix.Length);
            return h.Trim().ToLowerInvariant();
        }

        private static string Cell(List<string> row, int index)
            => index < row.Count ? row[index].Trim() : "";

        #endregion
    }
}
