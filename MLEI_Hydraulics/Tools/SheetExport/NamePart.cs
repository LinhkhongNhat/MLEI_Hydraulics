namespace MLEI_Hydraulics.Tools.SheetExport
{
    public enum NamePartKind
    {
        SheetParameter,
        TitleBlockParameter,
        Text,
        ProjectParameter
    }

    /// <summary>One piece of the file name. The pieces are joined in list order.</summary>
    public sealed class NamePart : ViewModelBase
    {
        public NamePartKind Kind { get; }

        /// <summary>The parameter name, or the literal text for a Text part.</summary>
        public string Value { get; }

        /// <summary>A new part: no prefix, and a "-" suffix so parts added one after another come out separated.</summary>
        public NamePart(NamePartKind kind, string value) : this(kind, value, "", "-")
        {
        }

        public NamePart(NamePartKind kind, string value, string prefix, string suffix)
        {
            Kind = kind;
            Value = value ?? "";
            _prefix = prefix ?? "";
            _suffix = suffix ?? "";
        }

        private string _prefix;

        /// <summary>Text added before this part's value (only when the value is not empty).</summary>
        public string Prefix
        {
            get => _prefix;
            set => Set(ref _prefix, value ?? "");
        }

        private string _suffix;

        /// <summary>Text added after this part's value (only when the value is not empty). Use it for separators such as "-".</summary>
        public string Suffix
        {
            get => _suffix;
            set => Set(ref _suffix, value ?? "");
        }

        private bool _isMissing;

        /// <summary>True when no sheet in this project has the parameter (for example a saved choice from another project).</summary>
        public bool IsMissing
        {
            get => _isMissing;
            set
            {
                if (Set(ref _isMissing, value))
                    OnPropertyChanged(nameof(Display));
            }
        }

        public string Display => Describe(Kind, Value) + (_isMissing ? "   (not found)" : "");

        public static string Describe(NamePartKind kind, string value)
        {
            switch (kind)
            {
                case NamePartKind.SheetParameter: return "Sheet : " + value;
                case NamePartKind.TitleBlockParameter: return "Title block : " + value;
                case NamePartKind.ProjectParameter: return "Project : " + value;
                default: return "Text : \"" + value + "\"";
            }
        }
    }

    /// <summary>An entry of the "add a parameter" dropdown.</summary>
    public sealed class AvailableField
    {
        public NamePartKind Kind { get; }
        public string Name { get; }

        public AvailableField(NamePartKind kind, string name)
        {
            Kind = kind;
            Name = name;
        }

        public string Display => NamePart.Describe(Kind, Name);

        public override string ToString() => Display;
    }
}
