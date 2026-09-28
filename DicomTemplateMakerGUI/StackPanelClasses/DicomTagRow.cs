using System;
using System.Windows;
using System.Windows.Controls;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    /// <summary>One DICOM requirement in the paths editor. Removing it goes through the editor's working copy.</summary>
    internal sealed class DicomTagRow : StackPanel
    {
        /// <param name="remove">Called when Remove is pressed; the editor removes exactly this key and value.</param>
        public DicomTagRow(string key, string value, Action<DicomTagRow> remove)
        {
            Key = key;
            Value = value;
            Orientation = Orientation.Horizontal;
            Margin = new Thickness(0, 0, 0, 2);
            Children.Add(new Label { Content = key, Width = 160 });
            Children.Add(new TextBlock { Text = value, Width = 300, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, ToolTip = value });
            Button delete_button = new Button { Content = "Remove", Width = 80, Height = 24, ToolTip = "Removes this requirement (when you save)." };
            delete_button.Click += (sender, e) => remove(this);
            Children.Add(delete_button);
        }

        public string Key { get; }

        public string Value { get; }
    }
}
