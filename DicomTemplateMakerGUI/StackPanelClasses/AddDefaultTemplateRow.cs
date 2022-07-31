using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DicomTemplateMakerGUI.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace DicomTemplateMakerGUI.StackPanelClasses
{
    class AddDefaultTemplateRow : StackPanel
    {
        public string file_path;
        public CheckBox check_box;
        public Label file_name;
        public AddDefaultTemplateRow(string file_path)
        {
            Orientation = Orientation.Horizontal;
            this.file_path = file_path;
            file_name = new Label();
            file_name.Content = Path.GetFileName(file_path);
            file_name.Width = 200;
            Children.Add(file_name);

            Label label = new Label();
            label.Content = "Build template?";
            label.Width = 200;
            Children.Add(label);

            check_box = new CheckBox();
            Children.Add(check_box);
        }
    }
}
