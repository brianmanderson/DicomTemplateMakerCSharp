using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    class PathsRow : StackPanel
    {
        private TemplateMaker template_maker;
        private string path;
        public PathsRow(TemplateMaker template_maker, string path)
        {
            this.template_maker = template_maker;
            this.path = path;
            Orientation = Orientation.Horizontal;
            Label path_label = new Label();
            path_label.Width = 100;
            path_label.Content = path;
            Children.Add(path_label);

            Button Delete_button = new Button();
            Delete_button.Content = "Delete";
            Delete_button.Click += Click_Delete;
        }
        private void Click_Delete(object sender, RoutedEventArgs e)
        {
            template_maker.Paths.Remove(path);
            Children.Clear();
        }
    }
}
