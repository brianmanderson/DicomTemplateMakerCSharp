using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FellowOakDicom;

namespace DicomTemplateMakerGUI.Validation
{
    public enum RealismSeverity
    {
        Error,
        Warning,
    }

    /// <summary>One rule a treatment-planning system would enforce that an RT breaks.</summary>
    public sealed record RealismFinding(string RuleId, RealismSeverity Severity, string Message);

    /// <summary>
    /// "TPS realism" rules (overhaul plan P2.4): what treatment-planning systems require of an RT Structure Set beyond
    /// the DICOM standard, checked on the dataset as written. Report-only: it reads the dataset and never changes it.
    /// <list type="bullet">
    /// <item>RR1 the Structure Set ROI Sequence has at least one item (RayStation refuses an RT with none).</item>
    /// <item>RR2 the ROI numbers of the three ROI sequences are the same set, without duplicates.</item>
    /// <item>RR3 a template ROI (no contours) has no Contour Sequence, rather than an empty one.</item>
    /// <item>RR4 one frame of reference, one study and one series, whose Contour Image Sequence lists each image once.</item>
    /// <item>RR5 every RT ROI Interpreted Type is a PS3.3 C.8.8.8 defined term or empty.</item>
    /// <item>RR6 ROI names are unique ignoring case and trailing spaces; names over 16 characters are a warning.</item>
    /// <item>RR7 (<see cref="CheckSet"/>) the Structure Set Label is present, not empty and unique across the files.</item>
    /// <item>RR8 code values have the syntax of their scheme (SCT with a valid Verhoeff check digit, FMA integers) and fit Code Value.</item>
    /// <item>RR9 context attributes come as a unit, Varian context attributes only on Varian codes, no retired ROI Observation Label.</item>
    /// <item>RR10 the top-level Frame of Reference UID and Position Reference Indicator are present and the UID is the one every reference uses.</item>
    /// </list>
    /// </summary>
    public static class RtRealismChecker
    {
        /// <summary>The 25 defined terms of RT ROI Interpreted Type (3006,00A4), PS3.3 C.8.8.8.</summary>
        public static readonly IReadOnlyCollection<string> InterpretedTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "EXTERNAL", "PTV", "CTV", "GTV", "TREATED_VOLUME", "IRRAD_VOLUME", "OAR", "BOLUS", "AVOIDANCE", "ORGAN", "MARKER",
            "REGISTRATION", "ISOCENTER", "CONTRAST_AGENT", "CAVITY", "BRACHY_CHANNEL", "BRACHY_ACCESSORY", "BRACHY_SRC_APP",
            "BRACHY_CHNL_SHLD", "SUPPORT", "FIXATION", "DOSE_REGION", "CONTROL", "DOSE_MEASUREMENT", "DEVICE",
        };

        /// <summary>What each rule checks, for reports.</summary>
        public static readonly IReadOnlyDictionary<string, string> Rules = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["RR1"] = "Structure Set ROI Sequence has at least one item",
            ["RR2"] = "the three ROI sequences use the same ROI numbers, without duplicates",
            ["RR3"] = "template ROIs have no Contour Sequence rather than an empty one",
            ["RR4"] = "one frame of reference, study and series; Contour Image Sequence lists each image once",
            ["RR5"] = "RT ROI Interpreted Type is a PS3.3 C.8.8.8 defined term or empty",
            ["RR6"] = "ROI names unique ignoring case and trailing spaces (error); over 16 characters (warning)",
            ["RR7"] = "Structure Set Label present, not empty, unique across the files",
            ["RR8"] = "code value syntax per scheme (SCT Verhoeff and partition, FMA integer); Code Value at most 16 characters",
            ["RR9"] = "context attributes as a unit; Varian context only on Varian codes; no retired ROI Observation Label (warnings)",
            ["RR10"] = "Frame of Reference UID and Position Reference Indicator present; one UID throughout",
        };

        /// <summary>The longest ROI name TG-263 recommends (its practical limit for planning systems).</summary>
        public const int Tg263NameLimit = 16;

        /// <summary>The six context attributes of a Code Sequence item that only make sense together.</summary>
        private static readonly (DicomTag Tag, string Name)[] ContextAttributes =
        {
            (DicomTag.ContextIdentifier, "Context Identifier"),
            (DicomTag.MappingResource, "Mapping Resource"),
            (DicomTag.ContextGroupVersion, "Context Group Version"),
            (DicomTag.MappingResourceUID, "Mapping Resource UID"),
            (DicomTag.MappingResourceName, "Mapping Resource Name"),
            (DicomTag.ContextUID, "Context UID"),
        };

        /// <summary>The root of Varian's UIDs (Mapping Resource UID 1.2.246.352.7.1.1 and its context UIDs).</summary>
        private const string VarianUidRoot = "1.2.246.352.";

        private static readonly Regex Digits = new Regex("^[0-9]+$", RegexOptions.CultureInvariant);

        private static readonly int[,] VerhoeffD =
        {
            { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            { 1, 2, 3, 4, 0, 6, 7, 8, 9, 5 },
            { 2, 3, 4, 0, 1, 7, 8, 9, 5, 6 },
            { 3, 4, 0, 1, 2, 8, 9, 5, 6, 7 },
            { 4, 0, 1, 2, 3, 9, 5, 6, 7, 8 },
            { 5, 9, 8, 7, 6, 0, 4, 3, 2, 1 },
            { 6, 5, 9, 8, 7, 1, 0, 4, 3, 2 },
            { 7, 6, 5, 9, 8, 2, 1, 0, 4, 3 },
            { 8, 7, 6, 5, 9, 3, 2, 1, 0, 4 },
            { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 },
        };

        private static readonly int[,] VerhoeffP =
        {
            { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            { 1, 5, 7, 6, 2, 8, 3, 0, 9, 4 },
            { 5, 8, 0, 3, 7, 9, 6, 1, 4, 2 },
            { 8, 9, 1, 6, 0, 4, 3, 5, 2, 7 },
            { 9, 4, 5, 3, 1, 2, 6, 8, 7, 0 },
            { 4, 2, 8, 6, 5, 7, 3, 9, 0, 1 },
            { 2, 7, 9, 3, 8, 0, 6, 4, 1, 5 },
            { 7, 0, 4, 6, 9, 1, 3, 2, 5, 8 },
        };

        /// <summary>The findings of the single-file rules (all but RR7) for one RT Structure Set.</summary>
        public static IReadOnlyList<RealismFinding> Check(DicomDataset rt)
        {
            ArgumentNullException.ThrowIfNull(rt);
            var findings = new List<RealismFinding>();
            IReadOnlyList<DicomDataset>? structureSetRois = Items(rt, DicomTag.StructureSetROISequence);
            IReadOnlyList<DicomDataset>? roiContours = Items(rt, DicomTag.ROIContourSequence);
            IReadOnlyList<DicomDataset>? observations = Items(rt, DicomTag.RTROIObservationsSequence);
            Dictionary<int, string> names = RoiNames(structureSetRois);

            CheckRoisPresent(structureSetRois, findings);
            CheckRoiNumbers(structureSetRois, roiContours, observations, findings);
            CheckContourSequences(roiContours, names, findings);
            CheckReferencedImages(rt, roiContours, findings);
            CheckInterpretedTypes(observations, names, findings);
            CheckRoiNames(structureSetRois, findings);
            CheckCodes(observations, names, findings);
            CheckContextAttributes(observations, names, findings);
            CheckFrameOfReference(rt, structureSetRois, findings);
            return findings;
        }

        /// <summary>
        /// The findings of the cross-file rule RR7 for RTs checked together (for example one per site): each file's
        /// Structure Set Label must be present, not empty, and unique among them. The single-file rules are not run;
        /// use <see cref="Check"/> on each file for those.
        /// </summary>
        public static IReadOnlyList<(string Name, RealismFinding Finding)> CheckSet(IEnumerable<(string name, DicomDataset rt)> rts)
        {
            ArgumentNullException.ThrowIfNull(rts);
            var findings = new List<(string, RealismFinding)>();
            var labels = new List<(string Name, string Label)>();
            foreach ((string name, DicomDataset rt) in rts)
            {
                string? label = Text(rt, DicomTag.StructureSetLabel);
                if (label == null)
                {
                    findings.Add((name, Error("RR7", "Structure Set Label (3006,0002) is missing.")));
                }
                else if (label.Trim().Length == 0)
                {
                    findings.Add((name, Error("RR7", "Structure Set Label (3006,0002) is empty.")));
                }
                else
                {
                    labels.Add((name, label));
                }
            }

            foreach (IGrouping<string, (string Name, string Label)> group in labels.GroupBy(l => l.Label, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            {
                foreach ((string name, string label) in group)
                {
                    string others = string.Join(", ", group.Where(g => g.Name != name).Select(g => g.Name));
                    findings.Add((name, Error("RR7", $"Structure Set Label '{label}' is also the label of {others}; a planning system lists structure sets by label.")));
                }
            }

            return findings;
        }

        /// <summary>True when the digits of <paramref name="value"/> end in a valid Verhoeff check digit (the SNOMED CT identifier check).</summary>
        public static bool IsValidVerhoeff(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length == 0 || !Digits.IsMatch(value))
            {
                return false;
            }

            int check = 0;
            for (int i = 0; i < value.Length; i++)
            {
                int digit = value[value.Length - 1 - i] - '0';
                check = VerhoeffD[check, VerhoeffP[i % 8, digit]];
            }

            return check == 0;
        }

        private static void CheckRoisPresent(IReadOnlyList<DicomDataset>? structureSetRois, List<RealismFinding> findings)
        {
            if (structureSetRois == null)
            {
                findings.Add(Error("RR1", "Structure Set ROI Sequence (3006,0020) is missing; RayStation refuses an RT without ROIs."));
            }
            else if (structureSetRois.Count == 0)
            {
                findings.Add(Error("RR1", "Structure Set ROI Sequence (3006,0020) has no items; RayStation refuses an RT without ROIs."));
            }
        }

        private static void CheckRoiNumbers(IReadOnlyList<DicomDataset>? structureSetRois, IReadOnlyList<DicomDataset>? roiContours, IReadOnlyList<DicomDataset>? observations, List<RealismFinding> findings)
        {
            var sequences = new[]
            {
                ("Structure Set ROI Sequence", "ROI Number", structureSetRois, DicomTag.ROINumber),
                ("ROI Contour Sequence", "Referenced ROI Number", roiContours, DicomTag.ReferencedROINumber),
                ("RT ROI Observations Sequence", "Referenced ROI Number", observations, DicomTag.ReferencedROINumber),
            };
            var sets = new List<(string Sequence, HashSet<string> Numbers)>();
            foreach ((string sequence, string attribute, IReadOnlyList<DicomDataset>? items, DicomTag tag) in sequences)
            {
                var numbers = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < (items?.Count ?? 0); i++)
                {
                    string? number = Number(items![i], tag);
                    if (number == null)
                    {
                        findings.Add(Error("RR2", $"{sequence} item {i + 1} has no {attribute}."));
                    }
                    else if (!numbers.Add(number))
                    {
                        findings.Add(Error("RR2", $"{sequence} lists {attribute} {number} more than once."));
                    }
                }

                sets.Add((sequence, numbers));
            }

            HashSet<string> reference = sets[0].Numbers;
            foreach ((string sequence, HashSet<string> numbers) in sets.Skip(1))
            {
                List<string> missing = reference.Except(numbers).OrderBy(SortKey).ToList();
                List<string> extra = numbers.Except(reference).OrderBy(SortKey).ToList();
                if (missing.Count > 0)
                {
                    findings.Add(Error("RR2", $"{sequence} has no item for ROI number(s) {string.Join(", ", missing)} of the Structure Set ROI Sequence."));
                }

                if (extra.Count > 0)
                {
                    findings.Add(Error("RR2", $"{sequence} refers to ROI number(s) {string.Join(", ", extra)}, which the Structure Set ROI Sequence does not define."));
                }
            }
        }

        private static void CheckContourSequences(IReadOnlyList<DicomDataset>? roiContours, Dictionary<int, string> names, List<RealismFinding> findings)
        {
            foreach (DicomDataset item in roiContours ?? Array.Empty<DicomDataset>())
            {
                if (item.Contains(DicomTag.ContourSequence) && (Items(item, DicomTag.ContourSequence)?.Count ?? 0) == 0)
                {
                    findings.Add(Error("RR3", $"{RoiLabel(item, DicomTag.ReferencedROINumber, names)}: Contour Sequence (3006,0040) is present but empty; a template ROI must leave it out."));
                }
            }
        }

        private static void CheckReferencedImages(DicomDataset rt, IReadOnlyList<DicomDataset>? roiContours, List<RealismFinding> findings)
        {
            IReadOnlyList<DicomDataset>? frames = Items(rt, DicomTag.ReferencedFrameOfReferenceSequence);
            if (frames == null || frames.Count != 1)
            {
                findings.Add(Error("RR4", $"Referenced Frame of Reference Sequence (3006,0010) has {Count(frames)}; planning systems expect exactly one."));
                return;
            }

            IReadOnlyList<DicomDataset>? studies = Items(frames[0], DicomTag.RTReferencedStudySequence);
            if (studies == null || studies.Count != 1)
            {
                findings.Add(Error("RR4", $"RT Referenced Study Sequence (3006,0012) has {Count(studies)}; planning systems expect exactly one."));
                return;
            }

            IReadOnlyList<DicomDataset>? series = Items(studies[0], DicomTag.RTReferencedSeriesSequence);
            if (series == null || series.Count != 1)
            {
                findings.Add(Error("RR4", $"RT Referenced Series Sequence (3006,0014) has {Count(series)}; planning systems expect exactly one."));
                return;
            }

            IReadOnlyList<DicomDataset>? images = Items(series[0], DicomTag.ContourImageSequence);
            if (images == null || images.Count == 0)
            {
                findings.Add(Error("RR4", $"Contour Image Sequence (3006,0016) of the referenced series has {Count(images)}; it must list the series' images."));
                return;
            }

            var listed = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < images.Count; i++)
            {
                string? instance = Text(images[i], DicomTag.ReferencedSOPInstanceUID);
                if (string.IsNullOrEmpty(instance))
                {
                    findings.Add(Error("RR4", $"Contour Image Sequence item {i + 1} has no Referenced SOP Instance UID."));
                }
                else if (!listed.Add(instance))
                {
                    findings.Add(Error("RR4", $"Contour Image Sequence lists SOP Instance {instance} more than once."));
                }
            }

            // Images that contours refer to must be among the listed ones (a template RT has no contours).
            var unlisted = new SortedSet<string>(StringComparer.Ordinal);
            foreach (DicomDataset roi in roiContours ?? Array.Empty<DicomDataset>())
            {
                foreach (DicomDataset contour in Items(roi, DicomTag.ContourSequence) ?? Array.Empty<DicomDataset>())
                {
                    foreach (DicomDataset image in Items(contour, DicomTag.ContourImageSequence) ?? Array.Empty<DicomDataset>())
                    {
                        string? instance = Text(image, DicomTag.ReferencedSOPInstanceUID);
                        if (!string.IsNullOrEmpty(instance) && !listed.Contains(instance))
                        {
                            unlisted.Add(instance);
                        }
                    }
                }
            }

            foreach (string instance in unlisted)
            {
                findings.Add(Error("RR4", $"A contour refers to SOP Instance {instance}, which the referenced series' Contour Image Sequence does not list."));
            }
        }

        private static void CheckInterpretedTypes(IReadOnlyList<DicomDataset>? observations, Dictionary<int, string> names, List<RealismFinding> findings)
        {
            foreach (DicomDataset item in observations ?? Array.Empty<DicomDataset>())
            {
                string? type = Text(item, DicomTag.RTROIInterpretedType);
                if (type == null)
                {
                    findings.Add(Error("RR5", $"{RoiLabel(item, DicomTag.ReferencedROINumber, names)}: RT ROI Interpreted Type (3006,00A4) is missing; it is Type 2 (present, may be empty)."));
                }
                else if (type.Trim().Length > 0 && !InterpretedTypes.Contains(type.Trim()))
                {
                    findings.Add(Error("RR5", $"{RoiLabel(item, DicomTag.ReferencedROINumber, names)}: RT ROI Interpreted Type '{type.Trim()}' is not a PS3.3 C.8.8.8 defined term."));
                }
            }
        }

        private static void CheckRoiNames(IReadOnlyList<DicomDataset>? structureSetRois, List<RealismFinding> findings)
        {
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DicomDataset item in structureSetRois ?? Array.Empty<DicomDataset>())
            {
                string? name = Text(item, DicomTag.ROIName);
                if (name == null)
                {
                    continue;
                }

                if (seen.TryGetValue(name, out string? first))
                {
                    findings.Add(Error("RR6", $"ROI name '{name}' repeats '{first}' (names are compared ignoring case and trailing spaces)."));
                }
                else
                {
                    seen.Add(name, name);
                }

                if (name.Length > Tg263NameLimit)
                {
                    findings.Add(Warning("RR6", $"ROI name '{name}' has {name.Length} characters; TG-263 keeps names to {Tg263NameLimit}."));
                }
            }
        }

        private static void CheckCodes(IReadOnlyList<DicomDataset>? observations, Dictionary<int, string> names, List<RealismFinding> findings)
        {
            foreach (DicomDataset observation in observations ?? Array.Empty<DicomDataset>())
            {
                foreach (DicomDataset code in Items(observation, DicomTag.RTROIIdentificationCodeSequence) ?? Array.Empty<DicomDataset>())
                {
                    string roi = RoiNameLabel(observation, names);
                    string? codeValue = Text(code, DicomTag.CodeValue);
                    string? value = codeValue ?? Text(code, DicomTag.LongCodeValue);
                    string scheme = Text(code, DicomTag.CodingSchemeDesignator)?.Trim() ?? string.Empty;
                    if (codeValue != null && codeValue.Length > 16)
                    {
                        findings.Add(Error("RR8", $"{roi}: Code Value '{codeValue}' has {codeValue.Length} characters; a value over 16 belongs in Long Code Value (0008,0119)."));
                    }

                    if (value == null)
                    {
                        continue;
                    }

                    if (scheme == "SCT")
                    {
                        string? problem = SnomedProblem(value);
                        if (problem != null)
                        {
                            findings.Add(Error("RR8", $"{roi}: SCT code value '{value}' {problem}."));
                        }
                    }
                    else if (scheme == "FMA" && !Digits.IsMatch(value))
                    {
                        findings.Add(Error("RR8", $"{roi}: FMA code value '{value}' is not an integer."));
                    }
                }
            }
        }

        /// <summary>Why <paramref name="value"/> is not a SNOMED CT identifier, or null when it is one.</summary>
        private static string? SnomedProblem(string value)
        {
            if (value.Length < 6 || value.Length > 18 || !Digits.IsMatch(value))
            {
                return "is not 6 to 18 digits";
            }

            // The partition identifier: the two digits before the check digit (00-05 short format, 10-15 long format).
            string partition = value.Substring(value.Length - 3, 2);
            if (!(partition[0] is '0' or '1') || partition[1] > '5')
            {
                return $"has partition identifier {partition}, which is not 00-05 or 10-15";
            }

            return IsValidVerhoeff(value) ? null : "fails the Verhoeff check digit";
        }

        private static void CheckContextAttributes(IReadOnlyList<DicomDataset>? observations, Dictionary<int, string> names, List<RealismFinding> findings)
        {
            int labelled = 0;
            var varianOn = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (DicomDataset observation in observations ?? Array.Empty<DicomDataset>())
            {
                if (observation.Contains(DicomTag.ROIObservationLabelRETIRED))
                {
                    labelled++;
                }

                foreach (DicomDataset code in Items(observation, DicomTag.RTROIIdentificationCodeSequence) ?? Array.Empty<DicomDataset>())
                {
                    List<string> present = ContextAttributes.Where(a => code.Contains(a.Tag)).Select(a => a.Name).ToList();
                    if (present.Count > 0 && present.Count < ContextAttributes.Length)
                    {
                        string missing = string.Join(", ", ContextAttributes.Select(a => a.Name).Except(present));
                        findings.Add(Warning("RR9", $"{RoiNameLabel(observation, names)}: the code has {string.Join(", ", present)} but not {missing}; context attributes belong together."));
                    }

                    string scheme = Text(code, DicomTag.CodingSchemeDesignator)?.Trim() ?? string.Empty;
                    List<string> varian = VarianContext(code);
                    if (varian.Count > 0 && !scheme.StartsWith("99VMS", StringComparison.Ordinal))
                    {
                        if (!varianOn.TryGetValue(scheme, out SortedSet<string>? values))
                        {
                            values = new SortedSet<string>(StringComparer.Ordinal);
                            varianOn.Add(scheme, values);
                        }

                        values.UnionWith(varian);
                    }
                }
            }

            // One finding per file and scheme, without counts, so the same gap reads the same in every file.
            foreach ((string scheme, SortedSet<string> values) in varianOn)
            {
                string schemeText = scheme.Length == 0 ? "no coding scheme" : "scheme " + scheme;
                findings.Add(Warning("RR9", $"Codes of {schemeText} carry Varian context attributes ({string.Join(", ", values)}); they describe Varian's structure code dictionary, not that scheme."));
            }

            if (labelled > 0)
            {
                string where = labelled == observations!.Count ? "every RT ROI Observations item" : "some RT ROI Observations items";
                findings.Add(Warning("RR9", $"ROI Observation Label (3006,0085), retired from the standard, is present in {where}."));
            }
        }

        /// <summary>The context attributes of <paramref name="code"/> that name Varian's code dictionary, as "Attribute value".</summary>
        private static List<string> VarianContext(DicomDataset code)
        {
            var found = new List<string>();
            string? resource = Text(code, DicomTag.MappingResource)?.Trim();
            if (resource != null && resource.StartsWith("99VMS", StringComparison.Ordinal))
            {
                found.Add("Mapping Resource " + resource);
            }

            string? context = Text(code, DicomTag.ContextIdentifier)?.Trim();
            if (context != null && context.StartsWith("VMS", StringComparison.Ordinal))
            {
                found.Add("Context Identifier " + context);
            }

            foreach ((DicomTag tag, string name) in new[] { (DicomTag.MappingResourceUID, "Mapping Resource UID"), (DicomTag.ContextUID, "Context UID") })
            {
                string? uid = Text(code, tag)?.Trim();
                if (uid != null && uid.StartsWith(VarianUidRoot, StringComparison.Ordinal))
                {
                    found.Add(name + " " + uid);
                }
            }

            string? resourceName = Text(code, DicomTag.MappingResourceName)?.Trim();
            if (resourceName != null && resourceName.Contains("Varian", StringComparison.OrdinalIgnoreCase))
            {
                found.Add("Mapping Resource Name " + resourceName);
            }

            return found;
        }

        private static void CheckFrameOfReference(DicomDataset rt, IReadOnlyList<DicomDataset>? structureSetRois, List<RealismFinding> findings)
        {
            if (!rt.Contains(DicomTag.PositionReferenceIndicator))
            {
                findings.Add(Error("RR10", "Position Reference Indicator (0020,1040) is missing (it is Type 2: present, may be empty)."));
            }

            string? uid = Text(rt, DicomTag.FrameOfReferenceUID)?.Trim();
            if (string.IsNullOrEmpty(uid))
            {
                findings.Add(Error("RR10", "Frame of Reference UID (0020,0052) is missing or empty at the top level."));
                return;
            }

            IReadOnlyList<DicomDataset>? frames = Items(rt, DicomTag.ReferencedFrameOfReferenceSequence);
            for (int i = 0; i < (frames?.Count ?? 0); i++)
            {
                string? referenced = Text(frames![i], DicomTag.FrameOfReferenceUID)?.Trim();
                if (referenced != uid)
                {
                    findings.Add(Error("RR10", $"Referenced Frame of Reference Sequence item {i + 1} has Frame of Reference UID {referenced ?? "(none)"}, not the RT's {uid}."));
                }
            }

            foreach (DicomDataset item in structureSetRois ?? Array.Empty<DicomDataset>())
            {
                string? referenced = Text(item, DicomTag.ReferencedFrameOfReferenceUID)?.Trim();
                if (referenced != uid)
                {
                    findings.Add(Error("RR10", $"Structure Set ROI '{Text(item, DicomTag.ROIName) ?? "(unnamed)"}' has Referenced Frame of Reference UID {referenced ?? "(none)"}, not the RT's {uid}."));
                }
            }
        }

        private static RealismFinding Error(string rule, string message) => new RealismFinding(rule, RealismSeverity.Error, message);

        private static RealismFinding Warning(string rule, string message) => new RealismFinding(rule, RealismSeverity.Warning, message);

        /// <summary>The items of a sequence; null when the dataset has no such sequence.</summary>
        private static IReadOnlyList<DicomDataset>? Items(DicomDataset dataset, DicomTag tag)
            => dataset.TryGetSequence(tag, out DicomSequence? sequence) && sequence != null ? sequence.Items.ToList() : null;

        private static string Count(IReadOnlyList<DicomDataset>? items) => items == null ? "no items (it is missing)" : items.Count + " item(s)";

        /// <summary>A string value without DICOM padding (trailing spaces and NULs); null when the attribute is absent.</summary>
        private static string? Text(DicomDataset dataset, DicomTag tag)
        {
            if (!dataset.Contains(tag))
            {
                return null;
            }

            try
            {
                return (dataset.GetString(tag) ?? string.Empty).TrimEnd(' ', '\0');
            }
            catch (DicomDataException)
            {
                return string.Empty;
            }
        }

        /// <summary>An IS value as written (leading and trailing spaces removed); null when absent or empty.</summary>
        private static string? Number(DicomDataset item, DicomTag tag)
        {
            string? value = Text(item, tag)?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static (int, string) SortKey(string number)
            => (int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : int.MaxValue, number);

        private static Dictionary<int, string> RoiNames(IReadOnlyList<DicomDataset>? structureSetRois)
        {
            var names = new Dictionary<int, string>();
            foreach (DicomDataset item in structureSetRois ?? Array.Empty<DicomDataset>())
            {
                if (int.TryParse(Number(item, DicomTag.ROINumber), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                {
                    names.TryAdd(number, Text(item, DicomTag.ROIName) ?? string.Empty);
                }
            }

            return names;
        }

        /// <summary>"ROI 3 'Name'" for an item that refers to an ROI number.</summary>
        private static string RoiLabel(DicomDataset item, DicomTag numberTag, Dictionary<int, string> names)
        {
            string? number = Number(item, numberTag);
            if (number == null)
            {
                return "ROI (no number)";
            }

            return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && names.TryGetValue(n, out string? name)
                ? $"ROI {number} '{name}'"
                : $"ROI {number}";
        }

        /// <summary>"ROI 'Name'" for an observation item, without its number, so one message covers the ROI in every file.</summary>
        private static string RoiNameLabel(DicomDataset observation, Dictionary<int, string> names)
        {
            string? number = Number(observation, DicomTag.ReferencedROINumber);
            return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && names.TryGetValue(n, out string? name)
                ? $"ROI '{name}'"
                : $"ROI {number ?? "(no number)"}";
        }
    }
}
