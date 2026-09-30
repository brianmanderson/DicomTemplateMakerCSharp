namespace DicomTemplateMakerGUI.ViewModels
{
    public enum FieldSeverity
    {
        /// <summary>Nothing to say.</summary>
        None,

        /// <summary>A hint; does not block.</summary>
        Info,

        /// <summary>Something was checked and works.</summary>
        Success,

        /// <summary>Probably wrong, but the user may go ahead.</summary>
        Warning,

        /// <summary>Must be fixed before going ahead.</summary>
        Error,
    }

    /// <summary>The message shown under an input field, with how serious it is.</summary>
    public sealed record FieldStatus(FieldSeverity Severity, string Text)
    {
        public static readonly FieldStatus Empty = new FieldStatus(FieldSeverity.None, string.Empty);

        public bool IsError => Severity == FieldSeverity.Error;

        /// <summary>The text with a symbol for its severity, so the meaning does not rely on colour alone.</summary>
        public string DisplayText => Severity switch
        {
            FieldSeverity.Error => "\u2716 " + Text,
            FieldSeverity.Warning => "\u26A0 " + Text,
            FieldSeverity.Success => "\u2714 " + Text,
            _ => Text,
        };

        public static FieldStatus Info(string text) => new FieldStatus(FieldSeverity.Info, text);

        public static FieldStatus Success(string text) => new FieldStatus(FieldSeverity.Success, text);

        public static FieldStatus Warning(string text) => new FieldStatus(FieldSeverity.Warning, text);

        public static FieldStatus Error(string text) => new FieldStatus(FieldSeverity.Error, text);
    }
}
