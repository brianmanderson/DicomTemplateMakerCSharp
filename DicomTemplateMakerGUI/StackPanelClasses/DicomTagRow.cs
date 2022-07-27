using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    class DicomTagRow : StackPanel
    {
        private TemplateMaker template_maker;
        private string key;
        private string value;
        private string path;
        public DicomTagRow(TemplateMaker template_maker, string key, string value)
        {
            Orientation = Orientation.Horizontal;
            if (template_maker.DicomTags.ContainsKey(key))
            {
                template_maker.DicomTags[key].Add(value);
            }
            else
            {
                template_maker.DicomTags.Add(key, new List<string> { value });
            }
            this.template_maker = template_maker;
            this.key = key;
            this.value = value;
            //Orientation = Orientation.Horizontal;
            Label key_label = new Label();
            key_label.Content = key;
            key_label.Width = 150;
            Children.Add(key_label);

            Label value_label = new Label();
            value_label.Content = value;
            value_label.Width = 150;
            Children.Add(value_label);

            Button Delete_button = new Button();
            Delete_button.Content = "Delete";
            Delete_button.Width = 150;
            Delete_button.Click += Click_Delete;
            Children.Add(Delete_button);
        }
        private void Click_Delete(object sender, RoutedEventArgs e)
        {
            template_maker.Paths.Remove(path);
            template_maker.DicomTags[key].Remove(value);
            if (template_maker.DicomTags[key].Count == 0)
            {
                template_maker.DicomTags.Remove(key);
            }
            Children.Clear();
        }
    }
}
