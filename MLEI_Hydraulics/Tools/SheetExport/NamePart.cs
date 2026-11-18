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

        public NamePart(NamePartKind kind, string value)
        {
            Kind = kind;
            Value = value ?? "";
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
