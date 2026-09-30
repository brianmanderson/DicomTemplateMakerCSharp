using CommunityToolkit.Mvvm.ComponentModel;
using DicomTemplateMakerGUI.Services;

namespace DicomTemplateMakerGUI.ViewModels
{
    /// <summary>One site (template) offered by a template source, with a "build it" tick.</summary>
    public sealed partial class TemplateRowViewModel : ObservableObject
    {
        private bool isSelected;

        public TemplateRowViewModel(string siteName, TemplateSourceItem source, int roiCount, bool canBuild, bool alreadyExists)
        {
            SiteName = siteName;
            Source = source;
            RoiCount = roiCount;
            CanBuild = canBuild;
            AlreadyExists = alreadyExists;
        }

        public string SiteName { get; }

        public TemplateSourceItem Source { get; }

        public int RoiCount { get; }

        public string RoiCountText => RoiCount == 1 ? "1 ROI" : RoiCount + " ROIs";

        /// <summary>False when the site name cannot be used as a folder name; such a row cannot be ticked.</summary>
        public bool CanBuild { get; }

        public string Problem => CanBuild ? string.Empty : "Cannot be built: the name is not a valid folder name.";

        /// <summary>A folder with this name is already in the template folder; building replaces its ROIs.</summary>
        [ObservableProperty]
        public partial bool AlreadyExists { get; set; }

        /// <summary>Ticked for building. Always false for a row that cannot be built.</summary>
        public bool IsSelected
        {
            get { return isSelected; }
            set { SetProperty(ref isSelected, value && CanBuild); }
        }
    }
}
