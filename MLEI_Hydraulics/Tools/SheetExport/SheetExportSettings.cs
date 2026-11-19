using System.Collections.Generic;

namespace MLEI_Hydraulics.Tools.SheetExport
{
    public enum SheetExportFormat
    {
        Pdf,
        Dwg
    }

    /// <summary>One saved piece of the file name.</summary>
    public class NamePartSetting
    {
        public NamePartKind Kind { get; set; }
        public string Value { get; set; } = "";

        public NamePartSetting() { }

        public NamePartSetting(NamePartKind kind, string value)
        {
            Kind = kind;
            Value = value;
        }
    }

    /// <summary>One sheet in a saved set. The unique id survives renumbering; the number is the fallback.</summary>
    public class SheetRefSetting
    {
        public string UniqueId { get; set; } = "";
        public string Number { get; set; } = "";
    }

    /// <summary>A named group of sheets that can be ticked again with one click. Belongs to one project.</summary>
    public class SheetSetSetting
    {
        public string Name { get; set; } = "";
        public string ProjectKey { get; set; } = "";
        public List<SheetRefSetting> Sheets { get; set; } = new List<SheetRefSetting>();
    }

    /// <summary>The last choices made in the sheet export tab. Saved per Windows user.</summary>
    public class SheetExportSettings
    {
        public SheetExportFormat Format { get; set; } = SheetExportFormat.Pdf;
        public string OutputFolder { get; set; } = "";
        public string Prefix { get; set; } = "";
        public string Suffix { get; set; } = "";
        public string Separator { get; set; } = " - ";
        public bool MergeViews { get; set; } = true;
        public bool Overwrite { get; set; } = false;
        public string DwgSetup { get; set; } = "";

        /// <summary>The file name pieces in order. Empty means "use the default" (sheet number, sheet name).</summary>
        public List<NamePartSetting> Parts { get; set; } = new List<NamePartSetting>();

        /// <summary>Saved sheet sets for all projects (each set knows its project).</summary>
        public List<SheetSetSetting> Sets { get; set; } = new List<SheetSetSetting>();

        private const string FileName = "SheetExportSettings.xml";

        public static SheetExportSettings Load() => SettingsStore.Load<SheetExportSettings>(FileName);
        public void Save() => SettingsStore.Save(FileName, this);
    }
}
