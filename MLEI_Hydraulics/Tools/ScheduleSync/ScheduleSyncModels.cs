using Autodesk.Revit.DB;

namespace MLEI_Hydraulics.Tools.ScheduleSync
{
    /// <summary>An entry of the schedule picker.</summary>
    public sealed class ScheduleOption
    {
        public ElementId Id { get; }
        public string Name { get; }
        public string CategoryName { get; }

        public ScheduleOption(ElementId id, string name, string categoryName)
        {
            Id = id;
            Name = name;
            CategoryName = categoryName;
        }

        public override string ToString() => Name;
    }

    /// <summary>One visible field of the schedule, and what the tool can do with it.</summary>
    public sealed class ScheduleColumn : ViewModelBase
    {
        /// <summary>Added to the CSV header of columns that Revit will not accept changes for.</summary>
        public const string ReadOnlySuffix = " (read-only)";

        public string Heading { get; }
        public ElementId ParameterId { get; }
        public bool IsTypeParameter { get; }

        /// <summary>False for calculated fields (count, formula...), which cannot be exported.</summary>
        public bool IsSupported { get; }
        public bool IsEditable { get; }
        public string StatusText { get; }

        public string ExportHeader => IsEditable ? Heading : Heading + ReadOnlySuffix;

        public ScheduleColumn(string heading, ElementId parameterId, bool isTypeParameter,
                              bool isSupported, bool isEditable, string statusText)
        {
            Heading = heading;
            ParameterId = parameterId;
            IsTypeParameter = isTypeParameter;
            IsSupported = isSupported;
            IsEditable = isEditable;
            StatusText = statusText;
            _isIncluded = isSupported;
        }

        private bool _isIncluded;
        public bool IsIncluded
        {
            get => _isIncluded;
            set => Set(ref _isIncluded, value);
        }
    }

    public enum ScheduleChangeStatus
    {
        Ready,
        NotInSchedule,
        NoParameter,
        ReadOnly,
        BlankNotAllowed,
        Conflict,
        Applied,
        Failed
    }

    /// <summary>One value that differs between the CSV and the model.</summary>
    public sealed class ScheduleChange : ViewModelBase
    {
        public string ElementIdText { get; }
        public string ElementLabel { get; }
        public string Field { get; }
        public string OldValue { get; }
        public string NewValue { get; }

        /// <summary>The element that owns the parameter (the type for type parameters).</summary>
        public ElementId TargetId { get; }
        public ElementId ParameterId { get; }

        /// <summary>Set for type parameters so rows of the same type can be compared.</summary>
        public string TypeKey { get; set; }

        public ScheduleChange(string elementIdText, string elementLabel, string field,
                              string oldValue, string newValue, ElementId targetId, ElementId parameterId)
        {
            ElementIdText = elementIdText;
            ElementLabel = elementLabel;
            Field = field;
            OldValue = oldValue;
            NewValue = newValue;
            TargetId = targetId;
            ParameterId = parameterId;
            SetStatus(ScheduleChangeStatus.Ready);
        }

        public ScheduleChangeStatus Status { get; private set; }
        public string StatusText { get; private set; }

        public bool IsReady => Status == ScheduleChangeStatus.Ready;
        public bool IsApplied => Status == ScheduleChangeStatus.Applied;
        public bool IsProblem => Status != ScheduleChangeStatus.Ready && Status != ScheduleChangeStatus.Applied;

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        public void SetStatus(ScheduleChangeStatus status, string detail = null)
        {
            Status = status;
            StatusText = detail ?? DefaultText(status);

            if (status != ScheduleChangeStatus.Ready)
                IsSelected = false;

            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsReady));
            OnPropertyChanged(nameof(IsApplied));
            OnPropertyChanged(nameof(IsProblem));
        }

        private static string DefaultText(ScheduleChangeStatus status)
        {
            switch (status)
            {
                case ScheduleChangeStatus.Ready: return "Will change";
                case ScheduleChangeStatus.NotInSchedule: return "Not in this schedule";
                case ScheduleChangeStatus.NoParameter: return "Element has no such parameter";
                case ScheduleChangeStatus.ReadOnly: return "Read-only for this element";
                case ScheduleChangeStatus.BlankNotAllowed: return "Blank not allowed";
                case ScheduleChangeStatus.Conflict: return "Same type, different values";
                case ScheduleChangeStatus.Applied: return "Applied";
                default: return "Failed";
            }
        }
    }

    public sealed class ScheduleImportResult
    {
        public System.Collections.Generic.List<ScheduleChange> Changes { get; } = new System.Collections.Generic.List<ScheduleChange>();
        public string Warning { get; set; }
        public int RowCount { get; set; }
    }

    /// <summary>Remembers the last folder and the type-parameter option per Windows user.</summary>
    public class ScheduleSyncSettings
    {
        public string LastFolder { get; set; } = "";
        public bool AllowTypeEdit { get; set; } = false;

        private const string FileName = "ScheduleSyncSettings.xml";

        public static ScheduleSyncSettings Load() => SettingsStore.Load<ScheduleSyncSettings>(FileName);
        public void Save() => SettingsStore.Save(FileName, this);
    }
}
