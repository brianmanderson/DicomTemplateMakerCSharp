using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FellowOakDicom;
using ROIOntologyClass;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    /// <summary>
    /// Builds the empty RT Structure Set (ROI definitions, no contours) of a template for one image series.
    /// The template RT dataset is never modified: every RT starts from a clone of it and is returned as a
    /// new <see cref="DicomFile"/>, so its File Meta Information matches its own SOP Instance UID.
    /// </summary>
    internal sealed class RtStructureBuilder
    {
        /// <summary>Detached Study Management SOP Class, the class RTReferencedStudySequence items refer to.</summary>
        public const string DetachedStudyManagementSopClassUid = "1.2.840.10008.3.1.2.3.1";

        public const string ManufacturerName = "UCSD Residency";
        public const string ManufacturerModel = "Universal_RT_Creator";
        public const string RoiInterpreter = "Brian_Mark_Anderson";

        /// <summary>Type 2 patient and study attributes: copied from the image, or written empty.</summary>
        internal static readonly IReadOnlyList<DicomTag> Type2FromImage = new[]
        {
            DicomTag.StudyDate, DicomTag.StudyTime, DicomTag.AccessionNumber, DicomTag.ReferringPhysicianName,
            DicomTag.PatientName, DicomTag.PatientID, DicomTag.PatientBirthDate, DicomTag.PatientSex, DicomTag.StudyID,
        };

        /// <summary>Type 3 attributes: copied from the image, or removed.</summary>
        internal static readonly IReadOnlyList<DicomTag> OptionalFromImage = new[]
        {
            DicomTag.StudyDescription, DicomTag.PatientIdentityRemoved, DicomTag.DeidentificationMethod,
        };

        /// <summary>What the RT declares when its images declare no character set (their bytes are kept as they are).</summary>
        internal const string UndeclaredCharacterSet = "ISO_IR 100";

        /// <summary>
        /// U+02C6 MODIFIER LETTER CIRCUMFLEX, which Windows text tools put where a name has '^' (the TG-263 separator);
        /// the bundled CTV_High^LN_3cm+ and CTV_Mid^LN_3cm- had it, and template folders made from them still do. It is in
        /// no single-byte character set, and fo-dicom's Latin-1 encoder always wrote it as '^'; the RT still does, whatever
        /// the character set.
        /// </summary>
        private const char ModifierLetterCircumflex = '\u02C6';

        /// <summary>U+FFFD REPLACEMENT CHARACTER: what a byte that is not UTF-8 becomes when a legacy (Windows-1252) text file is read.</summary>
        private const char ReplacementCharacter = '\uFFFD';

        private readonly DicomDataset template;

        /// <param name="template">The template RT dataset. It is cloned for every RT and never modified.</param>
        public RtStructureBuilder(DicomDataset template)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
        }

        /// <summary>
        /// Builds the RT for <paramref name="slices"/> (one series, in slice order) with the ROIs of a template.
        /// ROIs that cannot be added are listed in the result and left out; no RT is returned when the images
        /// lack a required attribute or no ROI could be added.
        /// </summary>
        /// <param name="slices">The series' image headers, in slice order.</param>
        /// <param name="templateName">The template name; its first 16 characters become the Structure Set Label.</param>
        /// <param name="rois">The template's ROIs.</param>
        /// <param name="generationTime">Local generation time for the creation, series and structure set dates.</param>
        public RtBuildResult Build(IReadOnlyList<DicomDataset> slices, string templateName, IReadOnlyList<ROIClass> rois, DateTimeOffset generationTime)
        {
            ArgumentNullException.ThrowIfNull(slices);
            ArgumentNullException.ThrowIfNull(templateName);
            ArgumentNullException.ThrowIfNull(rois);
            if (slices.Count == 0)
            {
                return RtBuildResult.Failed("The series has no images.");
            }

            DicomDataset first = slices[0];
            string? studyUid = GetValue(first, DicomTag.StudyInstanceUID);
            string? frameOfReferenceUid = GetValue(first, DicomTag.FrameOfReferenceUID);
            if (string.IsNullOrWhiteSpace(studyUid))
            {
                return RtBuildResult.Failed("The images have no Study Instance UID (0020,000D), which the RT must reference; no RT was written.");
            }

            if (string.IsNullOrWhiteSpace(frameOfReferenceUid))
            {
                return RtBuildResult.Failed("The images have no Frame of Reference UID (0020,0052), which the RT must reference; no RT was written.");
            }

            // Files are grouped by Series Instance UID only. One RT names one patient, study and frame of reference,
            // so a "series" that mixes them (for example an original and a pseudonymised copy that kept the UIDs)
            // cannot be referenced correctly: no RT rather than one filed under the wrong patient.
            string? inconsistency = FindInconsistency(slices);
            if (inconsistency != null)
            {
                return RtBuildResult.Failed(inconsistency);
            }

            var imageReferences = new List<(string SopClassUid, string SopInstanceUid)>(slices.Count);
            var seenInstances = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < slices.Count; i++)
            {
                string? sopClass = GetValue(slices[i], DicomTag.SOPClassUID);
                string? sopInstance = GetValue(slices[i], DicomTag.SOPInstanceUID);
                if (string.IsNullOrWhiteSpace(sopClass) || string.IsNullOrWhiteSpace(sopInstance))
                {
                    return RtBuildResult.Failed($"Image {i + 1} of the series has no SOP Class UID or SOP Instance UID; no RT was written.");
                }

                // The same instance saved twice (a re-sent image under another file name) is referenced once.
                if (seenInstances.Add(sopInstance))
                {
                    imageReferences.Add((sopClass, sopInstance));
                }
            }

            if (rois.Count == 0)
            {
                return RtBuildResult.Failed("The template has no ROIs; no RT was written.");
            }

            // The top level is not re-validated: patient and study values are copied exactly as the images hold
            // them (the RT must match its images even where they are not strictly conformant, e.g. PatientSex
            // "Male"). What the runner composes itself is still validated: the label below and the ROI items.
            DicomDataset rt = template.Clone().NotValidated();

            // The RT declares the images' character set, so copied strings are written back unchanged. Images that
            // declare none usually hold ISO 8859-1 (Latin-1) bytes: their strings are read as Latin-1 and the RT
            // declares ISO_IR 100, which keeps every byte as the image has it (fo-dicom would read them as ASCII
            // and write '?' for each accented letter).
            string[]? declared = DeclaredCharacterSets(first);
            string[] characterSets = declared ?? new[] { UndeclaredCharacterSet };
            rt.AddOrUpdate(DicomTag.SpecificCharacterSet, characterSets);
            Encoding? singleEncoding = SingleEncoding(characterSets);

            foreach (DicomTag tag in Type2FromImage)
            {
                rt.AddOrUpdate(tag, GetImageText(first, tag, declared != null) ?? string.Empty);
            }

            foreach (DicomTag tag in OptionalFromImage)
            {
                string? value = GetImageText(first, tag, declared != null);
                if (value != null)
                {
                    rt.AddOrUpdate(tag, value);
                }
                else
                {
                    rt.Remove(tag);
                }
            }

            if (first.TryGetSequence(DicomTag.DeidentificationMethodCodeSequence, out DicomSequence? methodCodes) && methodCodes != null)
            {
                rt.AddOrUpdate(new DicomSequence(DicomTag.DeidentificationMethodCodeSequence, methodCodes.Items.Select(i => i.Clone()).ToArray()));
            }
            else
            {
                rt.Remove(DicomTag.DeidentificationMethodCodeSequence);
            }

            rt.AddOrUpdate(DicomTag.StudyInstanceUID, studyUid);
            rt.AddOrUpdate(DicomTag.FrameOfReferenceUID, frameOfReferenceUid);

            string date = generationTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            string time = generationTime.ToString("HHmmss", CultureInfo.InvariantCulture);
            rt.AddOrUpdate(DicomTag.InstanceCreationDate, date);
            rt.AddOrUpdate(DicomTag.InstanceCreationTime, time);
            rt.AddOrUpdate(DicomTag.StructureSetDate, date);
            rt.AddOrUpdate(DicomTag.StructureSetTime, time);
            rt.AddOrUpdate(DicomTag.SeriesDate, date);
            rt.AddOrUpdate(DicomTag.SeriesTime, time);

            rt.AddOrUpdate(DicomTag.SOPClassUID, DicomUID.RTStructureSetStorage);
            rt.AddOrUpdate(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            rt.AddOrUpdate(DicomTag.SeriesInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            string label = templateName.Length > 16 ? templateName.Substring(0, 16) : templateName;
            DicomValidation.ValidateSH(label);
            rt.AddOrUpdate(DicomTag.StructureSetLabel, label);
            rt.AddOrUpdate(DicomTag.Manufacturer, ManufacturerName);
            rt.AddOrUpdate(DicomTag.ManufacturerModelName, ManufacturerModel);

            rt.AddOrUpdate(BuildReferencedFrameOfReference(first, studyUid, frameOfReferenceUid, imageReferences));

            var structureSetRois = new DicomSequence(DicomTag.StructureSetROISequence);
            var roiContours = new DicomSequence(DicomTag.ROIContourSequence);
            var observations = new DicomSequence(DicomTag.RTROIObservationsSequence);
            var failures = new List<(string RoiName, string Reason)>();
            int added = 0;
            foreach (ROIClass roi in rois)
            {
                // All three items are built before any is appended, so a failing ROI leaves nothing behind.
                string? problem = Validate(roi);
                RoiItems? items = null;
                if (problem == null)
                {
                    // Validate has checked that the name and code meaning are there.
                    string name = RtRoiName(roi.ROIName);
                    string? codeMeaning = RtCodeMeaning(roi.Ontology_Class?.CodeMeaning, roi.ROIName);
                    problem = CharacterSetProblem(name, codeMeaning, singleEncoding, characterSets);
                }

                if (problem == null)
                {
                    try
                    {
                        items = BuildRoiItems(roi, added + 1, frameOfReferenceUid);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Any failure here (typically a value fo-dicom rejects, such as a name longer than 64
                        // characters) concerns this ROI only, and nothing of it has been added yet.
                        problem = ex.Message;
                    }
                }

                if (items == null)
                {
                    failures.Add((DisplayName(roi), problem ?? "The ROI could not be added."));
                    continue;
                }

                structureSetRois.Items.Add(items.StructureSetRoi);
                roiContours.Items.Add(items.RoiContour);
                observations.Items.Add(items.Observation);
                added++;
            }

            if (added == 0)
            {
                return RtBuildResult.Failed($"None of the template's {rois.Count} ROI(s) could be added; no RT was written.", failures);
            }

            rt.AddOrUpdate(structureSetRois);
            rt.AddOrUpdate(roiContours);
            rt.AddOrUpdate(observations);
            return RtBuildResult.Built(new DicomFile(rt), added, failures);
        }

        /// <summary>Returns why <paramref name="roi"/> cannot be written, or null when it can.</summary>
        internal static string? Validate(ROIClass roi)
        {
            if (string.IsNullOrWhiteSpace(roi.ROIName))
            {
                return "The ROI has no name.";
            }

            if (string.IsNullOrWhiteSpace(roi.ROI_Interpreted_type))
            {
                return "The ROI has no interpreted type.";
            }

            OntologyCodeClass? code = roi.Ontology_Class;
            if (code == null)
            {
                return "The ROI has no ontology code.";
            }

            string? missing = MissingCodeParts(code);
            return missing == null ? null : $"The ROI's ontology code has no {missing}; choose a complete code for it.";
        }

        /// <summary>
        /// What <paramref name="code"/> lacks to be written into an RT, e.g. "code value and no coding scheme"; null when
        /// it is complete. Code Value and Coding Scheme Designator are required in the RT ROI Identification Code
        /// Sequence (and Code Meaning always is); an empty one is not a code a planning system can check, so an ROI
        /// with such a code is left out of every RT. Importers use this to say so when the ROI is imported.
        /// </summary>
        internal static string? MissingCodeParts(OntologyCodeClass code)
        {
            ArgumentNullException.ThrowIfNull(code);
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(code.CodeValue))
            {
                missing.Add("code value");
            }

            if (string.IsNullOrWhiteSpace(code.Scheme))
            {
                missing.Add("coding scheme");
            }

            if (string.IsNullOrWhiteSpace(code.CodeMeaning))
            {
                missing.Add("code meaning");
            }

            return missing.Count == 0 ? null : string.Join(" and no ", missing);
        }

        /// <summary>The ROI name as the RT holds it: a modifier letter circumflex is written as '^' (see <see cref="ModifierLetterCircumflex"/>).</summary>
        internal static string RtRoiName(string name)
        {
            return name.Replace(ModifierLetterCircumflex, '^');
        }

        /// <summary>
        /// The code meaning as the RT holds it. Legacy template files written in Windows-1252 are read as UTF-8, so a
        /// non-ASCII letter (U+02C6, 'ü') of a code meaning that repeats the ROI name became U+FFFD; such a code meaning
        /// (the ROI name with U+FFFD in place of some of its non-ASCII letters, and otherwise equal) is written as the
        /// ROI name. U+02C6 is written as '^', as in <see cref="RtRoiName"/>.
        /// </summary>
        internal static string? RtCodeMeaning(string? codeMeaning, string roiName)
        {
            if (codeMeaning == null)
            {
                return null;
            }

            if (IsNameWithLostLetters(codeMeaning, roiName))
            {
                codeMeaning = roiName;
            }

            return codeMeaning.Replace(ModifierLetterCircumflex, '^');
        }

        private static bool IsNameWithLostLetters(string codeMeaning, string roiName)
        {
            if (codeMeaning.Length != roiName.Length || codeMeaning.IndexOf(ReplacementCharacter) < 0)
            {
                return false;
            }

            for (int i = 0; i < codeMeaning.Length; i++)
            {
                bool lost = codeMeaning[i] == ReplacementCharacter && roiName[i] >= 0x80 && roiName[i] != ReplacementCharacter;
                if (!lost && codeMeaning[i] != roiName[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Why the ROI's name or code meaning (as the RT holds them, see <see cref="RtRoiName"/> and
        /// <see cref="RtCodeMeaning"/>) cannot be written in the RT's character set (fo-dicom would silently replace
        /// the letters it cannot hold, e.g. "Hüftkopf" becomes "Huftkopf" in ISO_IR 144); null when it can or when the
        /// set is not a single one that can be checked.
        /// </summary>
        internal static string? CharacterSetProblem(string name, string? codeMeaning, Encoding? encoding, IReadOnlyList<string> characterSets)
        {
            if (encoding == null)
            {
                return null;
            }

            foreach ((string what, string? value) in new[] { ("name", (string?)name), ("ontology code meaning", codeMeaning) })
            {
                if (value != null && !CanEncode(encoding, value))
                {
                    return $"The ROI's {what} '{value}' cannot be written in the images' character set ({string.Join("\\", characterSets)}) without changing letters.";
                }
            }

            return null;
        }

        private static string DisplayName(ROIClass roi) => string.IsNullOrWhiteSpace(roi.ROIName) ? "(unnamed)" : roi.ROIName;

        private static DicomSequence BuildReferencedFrameOfReference(DicomDataset first, string studyUid, string frameOfReferenceUid, IReadOnlyList<(string SopClassUid, string SopInstanceUid)> images)
        {
            var contourImages = new DicomSequence(DicomTag.ContourImageSequence);
            foreach ((string sopClass, string sopInstance) in images)
            {
                contourImages.Items.Add(new DicomDataset
                {
                    { DicomTag.ReferencedSOPClassUID, sopClass },
                    { DicomTag.ReferencedSOPInstanceUID, sopInstance },
                });
            }

            var series = new DicomDataset
            {
                { DicomTag.SeriesInstanceUID, GetValue(first, DicomTag.SeriesInstanceUID) ?? string.Empty },
            };
            series.Add(contourImages);

            var study = new DicomDataset
            {
                { DicomTag.ReferencedSOPClassUID, DetachedStudyManagementSopClassUid },
                { DicomTag.ReferencedSOPInstanceUID, studyUid },
            };
            study.Add(new DicomSequence(DicomTag.RTReferencedSeriesSequence, series));

            var frame = new DicomDataset
            {
                { DicomTag.FrameOfReferenceUID, frameOfReferenceUid },
            };
            frame.Add(new DicomSequence(DicomTag.RTReferencedStudySequence, study));
            return new DicomSequence(DicomTag.ReferencedFrameOfReferenceSequence, frame);
        }

        /// <summary>The ROI's three items, with its name and code meaning as the RT holds them (<see cref="RtRoiName"/>, <see cref="RtCodeMeaning"/>).</summary>
        private static RoiItems BuildRoiItems(ROIClass roi, int roiNumber, string frameOfReferenceUid)
        {
            // Validate has checked these; the locals make that explicit for the compiler.
            string interpretedType = roi.ROI_Interpreted_type ?? throw new ArgumentException("The ROI has no interpreted type.");
            OntologyCodeClass ontology = roi.Ontology_Class ?? throw new ArgumentException("The ROI has no ontology code.");
            string name = RtRoiName(roi.ROIName);
            string meaning = RtCodeMeaning(ontology.CodeMeaning, roi.ROIName) ?? throw new ArgumentException("The ROI's ontology code has no code meaning.");

            var structureSetRoi = new DicomDataset();
            structureSetRoi.AddOrUpdate(DicomTag.ROINumber, roiNumber);
            structureSetRoi.AddOrUpdate(DicomTag.ROIName, name);
            structureSetRoi.AddOrUpdate(DicomTag.ROIGenerationAlgorithm, "SEMIAUTOMATIC");
            structureSetRoi.AddOrUpdate(DicomTag.ReferencedFrameOfReferenceUID, frameOfReferenceUid);

            var roiContour = new DicomDataset();
            roiContour.AddOrUpdate(DicomTag.ReferencedROINumber, roiNumber);
            roiContour.AddOrUpdate(DicomTag.ROIDisplayColor, roi.color_string);

            // Validate has checked the required values. The Type 3 context and mapping attributes are written only when
            // they have a value; Mapping Resource and Context Group Version are required only with a Context Identifier.
            var code = new DicomDataset();
            code.AddOrUpdate(DicomTag.CodeMeaning, meaning);
            code.AddOrUpdate(DicomTag.CodeValue, ontology.CodeValue);
            code.AddOrUpdate(DicomTag.CodingSchemeDesignator, ontology.Scheme);
            if (!string.IsNullOrWhiteSpace(ontology.ContextIdentifier))
            {
                code.AddOrUpdate(DicomTag.ContextIdentifier, ontology.ContextIdentifier);
                AddIfPresent(code, DicomTag.ContextGroupVersion, ontology.ContextGroupVersion);
                AddIfPresent(code, DicomTag.MappingResource, ontology.MappingResource);
            }

            AddIfPresent(code, DicomTag.ContextUID, ontology.ContextUID);
            AddIfPresent(code, DicomTag.MappingResourceName, ontology.MappingResourceName);
            AddIfPresent(code, DicomTag.MappingResourceUID, ontology.MappingResourceUID);

            var observation = new DicomDataset();
            observation.AddOrUpdate(DicomTag.ObservationNumber, roiNumber);
            observation.AddOrUpdate(DicomTag.ReferencedROINumber, roiNumber);
            observation.AddOrUpdate(DicomTag.RTROIInterpretedType, interpretedType.ToUpperInvariant());
            observation.AddOrUpdate(DicomTag.ROIInterpreter, RoiInterpreter);
            // (3006,0085) ROI Observation Label is retired in the current DICOM standard (hence fo-dicom's
            // name) but is still written, as before, for systems that read it.
            observation.AddOrUpdate(DicomTag.ROIObservationLabelRETIRED, name.Length > 16 ? name.Substring(0, 16) : name);
            observation.AddOrUpdate(new DicomSequence(DicomTag.RTROIIdentificationCodeSequence, code));
            return new RoiItems(structureSetRoi, roiContour, observation);
        }

        /// <summary>
        /// The value of a tag without DICOM padding (trailing space or NUL), as the runner has always copied
        /// it; null when the tag is absent.
        /// </summary>
        internal static string? GetValue(DicomDataset dataset, DicomTag tag)
        {
            if (!dataset.Contains(tag))
            {
                return null;
            }

            return (dataset.GetString(tag) ?? string.Empty).TrimEnd(' ', '\0');
        }

        /// <summary>
        /// <see cref="GetValue"/> for a patient or study string copied into the RT. When the image declares no character
        /// set, a text element is read as Latin-1: every byte maps to one character and back, so the RT (which then
        /// declares ISO_IR 100) holds exactly the image's bytes.
        /// </summary>
        private static string? GetImageText(DicomDataset dataset, DicomTag tag, bool characterSetDeclared)
        {
            if (!characterSetDeclared && dataset.Contains(tag) && dataset.GetDicomItem<DicomItem>(tag) is DicomStringElement element)
            {
                return Encoding.Latin1.GetString(element.Buffer.Data).TrimEnd(' ', '\0');
            }

            return GetValue(dataset, tag);
        }

        /// <summary>The non-blank values of the image's Specific Character Set; null when it declares none.</summary>
        private static string[]? DeclaredCharacterSets(DicomDataset dataset)
        {
            if (dataset.TryGetValues(DicomTag.SpecificCharacterSet, out string[]? values) && values != null && values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                return values.Select(v => v?.Trim() ?? string.Empty).ToArray();
            }

            return null;
        }

        /// <summary>The encoding of a single-valued character set; null for code extensions (several values), which are not checked.</summary>
        private static Encoding? SingleEncoding(IReadOnlyList<string> characterSets)
        {
            if (characterSets.Count != 1)
            {
                return null;
            }

            try
            {
                return DicomEncoding.GetEncoding(characterSets[0]);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is DicomDataException)
            {
                return null;
            }
        }

        private static bool CanEncode(Encoding encoding, string value)
        {
            return value.All(c => c < 0x80) || string.Equals(encoding.GetString(encoding.GetBytes(value)), value, StringComparison.Ordinal);
        }

        private static void AddIfPresent(DicomDataset dataset, DicomTag tag, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                dataset.AddOrUpdate(tag, value);
            }
        }

        /// <summary>
        /// Why the slices cannot share one RT (another patient, study or frame of reference than the first slice), or
        /// null when they can. A value that some slices have and others lack also counts as different.
        /// </summary>
        private static string? FindInconsistency(IReadOnlyList<DicomDataset> slices)
        {
            foreach ((DicomTag tag, string name) in new[]
            {
                (DicomTag.PatientID, "Patient ID"),
                (DicomTag.StudyInstanceUID, "Study Instance UID"),
                (DicomTag.FrameOfReferenceUID, "Frame of Reference UID"),
            })
            {
                string? expected = GetValue(slices[0], tag);
                for (int i = 1; i < slices.Count; i++)
                {
                    string? value = GetValue(slices[i], tag);
                    if (!string.Equals(value, expected, StringComparison.Ordinal))
                    {
                        return $"The images of this series do not all have the same {name} ('{expected}' in image 1, '{value}' in image {i + 1}), "
                            + "so one RT cannot reference them all; no RT was written. Remove the images that do not belong to this series.";
                    }
                }
            }

            return null;
        }

        private sealed record RoiItems(DicomDataset StructureSetRoi, DicomDataset RoiContour, DicomDataset Observation);
    }

    /// <summary>The RT built for one series, or why none was built, and the ROIs left out.</summary>
    internal sealed class RtBuildResult
    {
        private RtBuildResult(DicomFile? file, int roiCount, string? error, IReadOnlyList<(string RoiName, string Reason)> roiFailures)
        {
            File = file;
            RoiCount = roiCount;
            Error = error;
            RoiFailures = roiFailures;
        }

        /// <summary>The RT to save; null when <see cref="Error"/> is set.</summary>
        public DicomFile? File { get; }

        public int RoiCount { get; }

        public string? Error { get; }

        public IReadOnlyList<(string RoiName, string Reason)> RoiFailures { get; }

        public static RtBuildResult Built(DicomFile file, int roiCount, IReadOnlyList<(string RoiName, string Reason)> roiFailures) =>
            new RtBuildResult(file, roiCount, null, roiFailures);

        public static RtBuildResult Failed(string error, IReadOnlyList<(string RoiName, string Reason)>? roiFailures = null) =>
            new RtBuildResult(null, 0, error, roiFailures ?? Array.Empty<(string, string)>());
    }
}
