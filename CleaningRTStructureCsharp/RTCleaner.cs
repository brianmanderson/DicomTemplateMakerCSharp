using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FellowOakDicom;

namespace CleaningRTStructureCsharp
{
    class RTCleaner
    {
        DicomFile RT_file;
        public RTCleaner(string dicom_file)
        {
            RT_file = DicomFile.Open(dicom_file, FileReadOption.ReadAll);
            update_image_sequence();
        }
        public void save(string file_path)
        {
            RT_file.Save(file_path);
        }
        public void update_image_sequence()
        {
            DicomSequence refFrameofRefSequence = RT_file.Dataset.GetDicomItem<DicomSequence>(DicomTag.ReferencedFrameOfReferenceSequence);
            foreach (DicomDataset refFrameofRef in refFrameofRefSequence.Items)
            {
                DicomSequence rtRefStudySequence = refFrameofRef.GetDicomItem<DicomSequence>(DicomTag.RTReferencedStudySequence);
                foreach (DicomDataset rtRefStudy in rtRefStudySequence)
                {
                    DicomSequence rTReferencedSeriesSequence = rtRefStudy.GetDicomItem<DicomSequence>(DicomTag.RTReferencedSeriesSequence);
                    foreach (DicomDataset rTReferencedSeries in rTReferencedSeriesSequence)
                    {
                        DicomSequence contourImageSequence = rTReferencedSeries.GetDicomItem<DicomSequence>(DicomTag.ContourImageSequence);
                        DicomDataset fill_segment_base = new DicomDataset(contourImageSequence.Items[0]);
                        int total = contourImageSequence.Items.Count;
                        for (int i = 0; i < total; i++)
                        {
                            contourImageSequence.Items.RemoveAt(0);
                        }
                    }
                }
            }
        }
    }
}
