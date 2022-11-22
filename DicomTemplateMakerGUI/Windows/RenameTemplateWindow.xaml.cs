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
using System.IO;

namespace DicomTemplateMakerGUI.Windows
{
    /// <summary>
    /// Interaction logic for RenameTemplateWindow.xaml
    /// </summary>
    public partial class RenameTemplateWindow : Window
    {
        public string out_path;
        public bool rename;
        public RenameTemplateWindow(string previous_name, string out_path)
        {
            InitializeComponent();
            PreviousName_Textbox.Text = previous_name;
            this.out_path = out_path;
            rename = false;
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Rename_Button_Click(object sender, RoutedEventArgs e)
        {
            rename = true;
            Close();
        }

        private void NewName_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Rename_Button.IsEnabled = false;
            Status_Label.Content = "Status:";
            if (NewName_TextBox.Text.IndexOfAny(Path.GetInvalidFileNameChars())<0)
            {
                if (Directory.Exists(Path.Combine(out_path, NewName_TextBox.Text)))
                {
                    Status_Label.Content = "Template already exists";
                }
                else
                {
                    Status_Label.Content = "Status:";
                    Rename_Button.IsEnabled = true;
                }
            }
            else
            {
                Status_Label.Content = "Status: Invalid name";
            }
        }

    }
}
