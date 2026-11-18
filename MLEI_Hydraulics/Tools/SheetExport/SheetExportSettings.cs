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

        private const string FileName = "SheetExportSettings.xml";

        public static SheetExportSettings Load() => SettingsStore.Load<SheetExportSettings>(FileName);
        public void Save() => SettingsStore.Save(FileName, this);
    }
}
