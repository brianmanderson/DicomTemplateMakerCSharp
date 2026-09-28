using System;
using System.Windows;
using System.Windows.Controls;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    /// <summary>One monitored folder in the paths editor, with a warning when it does not exist.</summary>
    internal sealed class PathsRow : StackPanel
    {
        private readonly TextBlock warning_text;

        /// <param name="remove">Called when Delete is pressed; the editor removes the row.</param>
        public PathsRow(string path, Action<PathsRow> remove)
        {
            Path = path;
            Orientation = Orientation.Vertical;
            Margin = new Thickness(0, 0, 0, 4);

            DockPanel line = new DockPanel { LastChildFill = true };
            Button delete_button = new Button { Content = "Remove", Width = 80, Height = 24, ToolTip = "Stops monitoring this folder (when you save). Nothing in the folder is deleted." };
            delete_button.Click += (sender, e) => remove(this);
            DockPanel.SetDock(delete_button, Dock.Right);
            line.Children.Add(delete_button);
            line.Children.Add(new TextBlock { Text = path, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 8, 0), ToolTip = path });
            Children.Add(line);

            warning_text = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Foreground = EditorBrushes.Warning };
            Children.Add(warning_text);
        }

        public string Path { get; }

        /// <summary>Shows <paramref name="warning"/> under the folder, or hides it when null.</summary>
        public void ShowWarning(string? warning)
        {
            warning_text.Text = warning == null ? string.Empty : "Warning: " + warning;
            warning_text.Visibility = warning == null ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
