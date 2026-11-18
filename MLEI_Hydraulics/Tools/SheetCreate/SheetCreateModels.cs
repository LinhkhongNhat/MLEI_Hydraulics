using Autodesk.Revit.DB;

namespace MLEI_Hydraulics.Tools.SheetCreate
{
    public enum SheetRowStatus
    {
        Ready,
        AlreadyExists,
        DuplicateInFile,
        MissingNumber,
        MissingName,
        InvalidCharacters,
        Created,
        Failed
    }

    /// <summary>One line of the imported sheet list, with the result of checking it.</summary>
    public sealed class SheetCreateRow : ViewModelBase
    {
        public string Number { get; }
        public string Name { get; }

        public SheetCreateRow(string number, string name)
        {
            Number = number ?? "";
            Name = name ?? "";
            SetStatus(SheetRowStatus.Ready);
        }

        public SheetRowStatus Status { get; private set; }
        public string StatusText { get; private set; }

        public bool IsReady => Status == SheetRowStatus.Ready;
        public bool IsCreated => Status == SheetRowStatus.Created;
        public bool IsProblem => Status != SheetRowStatus.Ready && Status != SheetRowStatus.Created;

        public void SetStatus(SheetRowStatus status, string detail = null)
        {
            Status = status;
            StatusText = detail ?? DefaultText(status);

            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsReady));
            OnPropertyChanged(nameof(IsCreated));
            OnPropertyChanged(nameof(IsProblem));
        }

        private static string DefaultText(SheetRowStatus status)
        {
            switch (status)
            {
                case SheetRowStatus.Ready: return "Ready";
                case SheetRowStatus.AlreadyExists: return "Already in project";
                case SheetRowStatus.DuplicateInFile: return "Duplicate in file";
                case SheetRowStatus.MissingNumber: return "Missing number";
                case SheetRowStatus.MissingName: return "Missing name";
                case SheetRowStatus.InvalidCharacters: return "Invalid characters";
                case SheetRowStatus.Created: return "Created";
                default: return "Failed";
            }
        }
    }

    /// <summary>An entry of the title block dropdown. "None" creates sheets without a title block.</summary>
    public sealed class TitleBlockOption
    {
        public static readonly TitleBlockOption None = new TitleBlockOption(null, "<No title block>");

        public ElementId Id { get; }
        public string Display { get; }
        public bool IsNone => Id == null;

        public TitleBlockOption(ElementId id, string display)
        {
            Id = id;
            Display = display;
        }

        public override string ToString() => Display;
    }

    public sealed class SheetCreateResult
    {
        public int Created { get; set; }
        public int Failed { get; set; }
    }

    /// <summary>Remembers the last title block and the last CSV folder per Windows user.</summary>
    public class SheetCreateSettings
    {
        public string TitleBlock { get; set; } = "";
        public string LastFolder { get; set; } = "";

        private const string FileName = "SheetCreateSettings.xml";

        public static SheetCreateSettings Load() => SettingsStore.Load<SheetCreateSettings>(FileName);
        public void Save() => SettingsStore.Save(FileName, this);
    }
}
