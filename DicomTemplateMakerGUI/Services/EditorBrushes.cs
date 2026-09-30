using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>Colours and a helper for the editors' status lines. The reason is always given as text; colour only adds emphasis.</summary>
    internal static class EditorBrushes
    {
        public static readonly Brush Error = Frozen(Color.FromRgb(176, 0, 32));
        public static readonly Brush Warning = Frozen(Color.FromRgb(138, 90, 0));
        public static readonly Brush Info = Frozen(Color.FromRgb(90, 90, 90));
        public static readonly Brush MissingBackground = Frozen(Color.FromRgb(229, 51, 51));

        /// <summary>
        /// Shows <paramref name="problems"/>, then <paramref name="warning"/>, then <paramref name="info"/> in
        /// <paramref name="target"/>, coloured by the most serious; hides it when there is nothing to say.
        /// </summary>
        public static void ShowStatus(TextBlock target, IReadOnlyCollection<string> problems, string? warning, string? info = null)
        {
            var parts = problems.ToList();
            if (!string.IsNullOrEmpty(warning))
            {
                parts.Add("Warning: " + warning);
            }

            if (!string.IsNullOrEmpty(info))
            {
                parts.Add(info);
            }

            target.Text = string.Join("  ", parts);
            target.Foreground = problems.Count > 0 ? Error : !string.IsNullOrEmpty(warning) ? Warning : Info;
            target.Visibility = parts.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
