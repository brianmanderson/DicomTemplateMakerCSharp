using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    class AddAirTableRow : StackPanel
    {
        public string site_name;
        public Label site_label;
        public CheckBox check_box;
        public AddAirTableRow(string site_name)
        {
            Orientation = Orientation.Horizontal;
            this.site_name = site_name;
            site_label = new Label();
            site_label.Content = site_name;
            site_label.Width = 200;
            Children.Add(site_label);

            Label label = new Label();
            label.Content = "Build template?";
            label.Width = 200;
            Children.Add(label);

            check_box = new CheckBox();
            Children.Add(check_box);
        }
    }
}
