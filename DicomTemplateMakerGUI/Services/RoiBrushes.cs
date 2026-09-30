using System.Windows.Media;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>WPF brushes for an ROI's colours. Kept in the GUI so the ROI model has no WPF dependency.</summary>
    internal static class RoiBrushes
    {
        public static SolidColorBrush Fill(ROIClass roi)
        {
            return Frozen(Color.FromRgb(roi.R, roi.G, roi.B));
        }

        public static SolidColorBrush DvhLine(ROIClass roi)
        {
            return Frozen(Color.FromRgb(roi.R_DVH, roi.G_DVH, roi.B_DVH));
        }

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
