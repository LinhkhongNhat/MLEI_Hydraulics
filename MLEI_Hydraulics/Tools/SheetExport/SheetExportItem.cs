using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace MLEI_Hydraulics.Tools.SheetExport
{
    /// <summary>One row of the sheet list: the sheet, whether it is ticked, and the file name it will get.</summary>
    public sealed class SheetExportItem : ViewModelBase
    {
        public ElementId Id { get; }
        public string SheetNumber { get; }
        public string SheetName { get; }

        /// <summary>Every parameter of the sheet as text, read once when the tab opens (name -> value).</summary>
        public IReadOnlyDictionary<string, string> SheetValues { get; }

        /// <summary>Every parameter of the sheet's title block (instance and type) as text. Empty if it has none.</summary>
        public IReadOnlyDictionary<string, string> TitleBlockValues { get; }

        /// <summary>Every Project Information parameter as text. The same for all sheets.</summary>
        public IReadOnlyDictionary<string, string> ProjectValues { get; }

        public SheetExportItem(
            ElementId id, string number, string name,
            IReadOnlyDictionary<string, string> sheetValues,
            IReadOnlyDictionary<string, string> titleBlockValues,
            IReadOnlyDictionary<string, string> projectValues)
        {
            Id = id;
            SheetNumber = number ?? "";
            SheetName = name ?? "";
            SheetValues = sheetValues;
            TitleBlockValues = titleBlockValues;
            ProjectValues = projectValues;
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        /// <summary>File name without extension, already cleaned of illegal characters.</summary>
        private string _fileName = "";
        public string FileName
        {
            get => _fileName;
            set => Set(ref _fileName, value);
        }

        /// <summary>File name with extension, shown in the list.</summary>
        private string _fileNameDisplay = "";
        public string FileNameDisplay
        {
            get => _fileNameDisplay;
            set => Set(ref _fileNameDisplay, value);
        }

        /// <summary>True when another ticked sheet would get the same file name.</summary>
        private bool _isDuplicate;
        public bool IsDuplicate
        {
            get => _isDuplicate;
            set => Set(ref _isDuplicate, value);
        }
    }
}
