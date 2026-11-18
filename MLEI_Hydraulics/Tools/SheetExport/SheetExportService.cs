using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;

namespace MLEI_Hydraulics.Tools.SheetExport
{
    public sealed class FileNameOptions
    {
        public string Prefix { get; set; } = "";
        public string Suffix { get; set; } = "";
        public string Separator { get; set; } = " - ";
        public IList<NamePart> Parts { get; set; } = new List<NamePart>();
    }

    public sealed class SheetExportRequest
    {
        public SheetExportFormat Format { get; set; }
        public string Folder { get; set; }
        /// <summary>Name of a saved DWG export setup, or empty for Revit's default options.</summary>
        public string DwgSetup { get; set; } = "";
        public bool MergeViews { get; set; } = true;
        public bool Overwrite { get; set; }
    }

    public sealed class SheetExportResult
    {
        public int Exported { get; set; }
        public List<string> Skipped { get; } = new List<string>();
        public List<string> Failed { get; } = new List<string>();
    }

    /// <summary>All Revit API work for the sheet export tab. No UI code in here.</summary>
    public static class SheetExportService
    {
        /// <summary>
        /// Reads every non-placeholder sheet together with all its parameter values and those of its
        /// title block. Reading once here keeps the live file name preview fast (no Revit calls while typing).
        /// </summary>
        public static List<SheetExportItem> GetSheets(Document doc)
        {
            List<SheetExportItem> items = new List<SheetExportItem>();

            IEnumerable<ViewSheet> sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber, StringComparer.OrdinalIgnoreCase);

            // Project Information is the same for every sheet, so it is read once
            Dictionary<string, string> projectValues = ReadParameters(doc.ProjectInformation);

            foreach (ViewSheet sheet in sheets)
            {
                Dictionary<string, string> sheetValues = ReadParameters(sheet);

                // These two always exist under these names, whatever language Revit runs in
                sheetValues["Sheet Number"] = sheet.SheetNumber;
                sheetValues["Sheet Name"] = sheet.Name;

                items.Add(new SheetExportItem(sheet.Id, sheet.SheetNumber, sheet.Name,
                                              sheetValues, ReadTitleBlockValues(doc, sheet), projectValues));
            }

            return items;
        }

        /// <summary>The parameters that can be added to the file name (sheet ones first, then title block ones).</summary>
        public static List<AvailableField> GetAvailableFields(IEnumerable<SheetExportItem> items)
        {
            List<SheetExportItem> list = items.ToList();
            List<AvailableField> fields = new List<AvailableField>();

            fields.AddRange(list
                .SelectMany(i => i.SheetValues.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => new AvailableField(NamePartKind.SheetParameter, n)));

            fields.AddRange(list
                .SelectMany(i => i.TitleBlockValues.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => new AvailableField(NamePartKind.TitleBlockParameter, n)));

            fields.AddRange(list
                .SelectMany(i => i.ProjectValues.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => new AvailableField(NamePartKind.ProjectParameter, n)));

            return fields;
        }

        /// <summary>The project's saved DWG export setups ("Export Setups" in Revit).</summary>
        public static List<string> GetDwgSetups(Document doc)
        {
            try
            {
                return DWGExportOptions.GetPredefinedSetupNames(doc)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Prefix + the parts joined with the separator + suffix, cleaned of characters Windows does not allow.
        /// Parts whose value is empty for this sheet are left out, so there are no doubled separators.
        /// </summary>
        public static string BuildFileName(SheetExportItem item, FileNameOptions options)
        {
            List<string> pieces = new List<string>();

            foreach (NamePart part in options.Parts)
            {
                string value;

                switch (part.Kind)
                {
                    case NamePartKind.Text:
                        value = part.Value;
                        break;
                    case NamePartKind.SheetParameter:
                        item.SheetValues.TryGetValue(part.Value, out value);
                        value = (value ?? "").Trim();
                        break;
                    case NamePartKind.ProjectParameter:
                        item.ProjectValues.TryGetValue(part.Value, out value);
                        value = (value ?? "").Trim();
                        break;
                    default:
                        item.TitleBlockValues.TryGetValue(part.Value, out value);
                        value = (value ?? "").Trim();
                        break;
                }

                if (!string.IsNullOrEmpty(value))
                    pieces.Add(value);
            }

            string raw = (options.Prefix ?? "") + string.Join(options.Separator ?? "", pieces) + (options.Suffix ?? "");

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);

            return sb.ToString().Trim().TrimEnd('.');
        }

        /// <summary>
        /// Exports the given sheets one by one so each file gets exactly its own name.
        /// progress(done, total, currentFileName) is called before each sheet.
        /// Must be called from Revit's main thread, with no transaction open.
        /// </summary>
        public static SheetExportResult Export(
            Document doc,
            IList<SheetExportItem> items,
            SheetExportRequest request,
            Action<int, int, string> progress)
        {
            SheetExportResult result = new SheetExportResult();
            string ext = request.Format == SheetExportFormat.Pdf ? ".pdf" : ".dwg";

            System.IO.Directory.CreateDirectory(request.Folder);

            for (int i = 0; i < items.Count; i++)
            {
                SheetExportItem item = items[i];
                string fileLabel = item.FileName + ext;
                string target = System.IO.Path.Combine(request.Folder, fileLabel);

                progress?.Invoke(i, items.Count, fileLabel);

                try
                {
                    if (System.IO.File.Exists(target))
                    {
                        if (!request.Overwrite)
                        {
                            result.Skipped.Add(fileLabel);
                            continue;
                        }

                        // Delete first so we can tell the user clearly if the file is open elsewhere
                        System.IO.File.Delete(target);
                    }

                    bool ok = request.Format == SheetExportFormat.Pdf
                        ? ExportPdf(doc, item, request.Folder)
                        : ExportDwg(doc, item, request);

                    if (ok)
                        result.Exported++;
                    else
                        result.Failed.Add($"{fileLabel}: Revit did not complete the export");
                }
                catch (Exception ex)
                {
                    result.Failed.Add($"{fileLabel}: {ex.Message}");
                }
            }

            progress?.Invoke(items.Count, items.Count, "");
            return result;
        }

        private static bool ExportPdf(Document doc, SheetExportItem item, string folder)
        {
            // Combine = true with a single sheet is how the API lets us choose the file name
            PDFExportOptions options = new PDFExportOptions
            {
                Combine = true,
                FileName = item.FileName
            };

            return doc.Export(folder, new List<ElementId> { item.Id }, options);
        }

        private static bool ExportDwg(Document doc, SheetExportItem item, SheetExportRequest request)
        {
            DWGExportOptions options = null;

            if (!string.IsNullOrEmpty(request.DwgSetup))
                options = DWGExportOptions.GetPredefinedOptions(doc, request.DwgSetup);

            if (options == null)
                options = new DWGExportOptions();

            if (request.MergeViews)
                options.MergedViews = true;

            return doc.Export(request.Folder, item.FileName, new List<ElementId> { item.Id }, options);
        }

        #region Reading parameters

        /// <summary>The sheet's title block: its own parameters, plus those of its type (the instance wins on a clash).</summary>
        private static Dictionary<string, string> ReadTitleBlockValues(Document doc, ViewSheet sheet)
        {
            Element titleBlock = new FilteredElementCollector(doc, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .FirstOrDefault();

            if (titleBlock == null)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string> values = ReadParameters(titleBlock);

            foreach (KeyValuePair<string, string> pair in ReadParameters(doc.GetElement(titleBlock.GetTypeId())))
            {
                if (!values.ContainsKey(pair.Key))
                    values[pair.Key] = pair.Value;
            }

            return values;
        }

        private static Dictionary<string, string> ReadParameters(Element element)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (element == null)
                return values;

            foreach (Parameter p in element.Parameters)
            {
                string name = p.Definition != null ? p.Definition.Name : null;
                if (string.IsNullOrEmpty(name) || values.ContainsKey(name))
                    continue;

                values[name] = ReadText(p);
            }

            return values;
        }

        /// <summary>The value as the user sees it in Revit.</summary>
        private static string ReadText(Parameter p)
        {
            try
            {
                if (!p.HasValue)
                    return "";

                if (p.StorageType == StorageType.String)
                    return p.AsString() ?? "";

                string formatted = p.AsValueString();
                if (!string.IsNullOrEmpty(formatted))
                    return formatted;

                if (p.StorageType == StorageType.Integer)
                    return p.AsInteger().ToString(CultureInfo.InvariantCulture);

                return "";
            }
            catch
            {
                return "";
            }
        }

        #endregion
    }
}
