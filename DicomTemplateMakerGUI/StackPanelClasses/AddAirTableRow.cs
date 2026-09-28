using System.Windows.Controls;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.StackPanelClasses
{
    /// <summary>One site (template) offered by a template source, with a "build it" check box.</summary>
    class AddAirTableRow : StackPanel
    {
        public string site_name;
        public Label site_label;
        public CheckBox check_box;
        public TemplateSourceItem airtable;

        public AddAirTableRow(string site_name, TemplateSourceItem airTable, int roiCount)
        {
            airtable = airTable;
            Orientation = Orientation.Horizontal;
            this.site_name = site_name;
            site_label = new Label();
            site_label.Content = site_name;
            site_label.Width = 200;
            Children.Add(site_label);

            check_box = new CheckBox();
            check_box.Content = "Build template?";
            check_box.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            Children.Add(check_box);

            Label count_label = new Label();
            count_label.Content = roiCount == 1 ? "1 ROI" : roiCount + " ROIs";
            count_label.Margin = new System.Windows.Thickness(20, 0, 0, 0);
            Children.Add(count_label);
        }
    }
}
