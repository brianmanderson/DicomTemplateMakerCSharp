using System.Globalization;
using System.Text;
using FellowOakDicom;
using FellowOakDicom.IO.Buffer;

namespace TemplateEvaluationTool
{
    /// <summary>
    /// Replaces each distinct UID by <c>UID#n</c>, numbered in order of first appearance, so equal UIDs stay
    /// visibly equal. UIDs under the DICOM root 1.2.840.10008 (SOP classes, transfer syntaxes) identify nothing
    /// and are kept, followed by their name.
    /// </summary>
    internal sealed class UidMasker
    {
        private const string DicomRoot = "1.2.840.10008.";
        private readonly Dictionary<string, string> masks = new Dictionary<string, string>(StringComparer.Ordinal);

        public static bool IsStandard(string uid) => uid.StartsWith(DicomRoot, StringComparison.Ordinal);

        public string Mask(string uid)
        {
            if (IsStandard(uid))
            {
                DicomUID known = DicomUID.Parse(uid);
                return known.Type == DicomUidType.Unknown ? uid : uid + " (" + known.Name + ")";
            }

            if (!masks.TryGetValue(uid, out string? mask))
            {
                mask = "UID#" + (masks.Count + 1).ToString(CultureInfo.InvariantCulture);
                masks.Add(uid, mask);
            }

            return mask;
        }
    }

    /// <summary>
    /// Writes every element of a DICOM file as one line, <c>(gggg,eeee) Keyword VR VM len value</c>: the file meta
    /// information first, then the data set, with sequence items indented. Lengths are the stored value lengths
    /// (padding included); a sequence has no stored length after parsing, so it shows <c>-</c> and its item count
    /// as VM. Masked values show <c>*</c> as length:
    /// <list type="bullet">
    /// <item>non-standard UIDs become <c>UID#n</c> (see <see cref="UidMasker"/>);</item>
    /// <item>non-empty DA, TM and DT values become <c>&lt;DATE&gt;</c>, <c>&lt;TIME&gt;</c>, <c>&lt;DATETIME&gt;</c>;</item>
    /// <item>non-empty values of <see cref="IsMaskedAttribute"/> become <c>&lt;MASKED&gt;</c>;</item>
    /// <item>File Meta Information Group Length becomes <c>&lt;DERIVED&gt;</c> (its 4-byte length stays): it sums the
    /// masked UID lengths.</item>
    /// </list>
    /// Empty values stay empty, so a Type 2 attribute that is present but empty remains visible.
    /// </summary>
    internal static class DicomInventory
    {
        public const string Masked = "<MASKED>";
        public const string Derived = "<DERIVED>";
        private const int MaxBinaryBytes = 32;

        /// <summary>
        /// The implementation identity, the patient attributes (every attribute of group 0010) and every person
        /// name copied from the images. ROI Interpreter is a constant of the program, not personal data from the
        /// images, so it stays visible (gap C4).
        /// </summary>
        public static bool IsMaskedAttribute(DicomTag tag, DicomVR vr)
        {
            if (tag == DicomTag.ImplementationClassUID || tag == DicomTag.ImplementationVersionName)
            {
                return true;
            }

            if (tag.Group == 0x0010)
            {
                return true;
            }

            return vr == DicomVR.PN && tag != DicomTag.ROIInterpreter;
        }

        public static string Write(DicomFile file)
        {
            ArgumentNullException.ThrowIfNull(file);
            var masker = new UidMasker();
            var text = new StringBuilder();
            WriteDataset(text, file.FileMetaInfo, 0, masker);
            WriteDataset(text, file.Dataset, 0, masker);
            return text.ToString();
        }

        private static void WriteDataset(StringBuilder text, DicomDataset dataset, int depth, UidMasker masker)
        {
            foreach (DicomItem item in dataset)
            {
                string indent = new string(' ', depth * 2);
                string head = indent + Format(item.Tag) + " " + Keyword(item.Tag) + " " + item.ValueRepresentation.Code;
                if (item is DicomSequence sequence)
                {
                    text.Append(head).Append(' ').Append(sequence.Items.Count.ToString(CultureInfo.InvariantCulture)).Append(" -").Append('\n');
                    for (int i = 0; i < sequence.Items.Count; i++)
                    {
                        text.Append(indent).Append("  (FFFE,E000) Item ").Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append('\n');
                        WriteDataset(text, sequence.Items[i], depth + 2, masker);
                    }
                }
                else if (item is DicomElement element)
                {
                    (string value, bool masked) = Value(element, masker);
                    string length = masked ? "*" : element.Buffer.Size.ToString(CultureInfo.InvariantCulture);
                    string line = head + " " + Multiplicity(element).ToString(CultureInfo.InvariantCulture) + " " + length + " " + value;
                    text.Append(line.TrimEnd()).Append('\n');
                }
                else
                {
                    // Encapsulated pixel data and other fragment sequences: not expected in an RT Structure Set.
                    text.Append(head).Append(" - - <fragments>").Append('\n');
                }
            }
        }

        private static (string Value, bool Masked) Value(DicomElement element, UidMasker masker)
        {
            DicomVR vr = element.ValueRepresentation;
            if (element.Buffer.Size == 0)
            {
                return (string.Empty, false);
            }

            if (IsMaskedAttribute(element.Tag, vr))
            {
                return (Masked, true);
            }

            if (element.Tag == DicomTag.FileMetaInformationGroupLength)
            {
                // The sum of the file meta lengths, so it would reveal the lengths of the masked UIDs.
                return (Derived, false);
            }

            if (vr == DicomVR.DA || vr == DicomVR.TM || vr == DicomVR.DT)
            {
                string placeholder = vr == DicomVR.DA ? "<DATE>" : vr == DicomVR.TM ? "<TIME>" : "<DATETIME>";
                return (string.Join("\\", Enumerable.Repeat(placeholder, Math.Max(element.Count, 1))), true);
            }

            if (vr == DicomVR.UI)
            {
                string[] uids = element.Get<string[]>();
                bool masked = uids.Any(u => !UidMasker.IsStandard(u));
                return (string.Join("\\", uids.Select(masker.Mask)), masked);
            }

            if (element is DicomStringElement)
            {
                return (string.Join("\\", element.Get<string[]>()), false);
            }

            if (vr == DicomVR.AT)
            {
                return (string.Join("\\", element.Get<DicomTag[]>().Select(Format)), false);
            }

            if (IsBinary(vr))
            {
                return (Hex(element.Buffer), false);
            }

            return (Numbers(element), false);
        }

        private static string Numbers(DicomElement element)
        {
            DicomVR vr = element.ValueRepresentation;
            IEnumerable<IFormattable> values =
                vr == DicomVR.US ? element.Get<ushort[]>().Cast<IFormattable>()
                : vr == DicomVR.SS ? element.Get<short[]>().Cast<IFormattable>()
                : vr == DicomVR.UL ? element.Get<uint[]>().Cast<IFormattable>()
                : vr == DicomVR.SL ? element.Get<int[]>().Cast<IFormattable>()
                : vr == DicomVR.UV ? element.Get<ulong[]>().Cast<IFormattable>()
                : vr == DicomVR.SV ? element.Get<long[]>().Cast<IFormattable>()
                : vr == DicomVR.FL ? element.Get<float[]>().Cast<IFormattable>()
                : vr == DicomVR.FD ? element.Get<double[]>().Cast<IFormattable>()
                : throw new NotSupportedException("No inventory format for VR " + vr.Code + " at " + Format(element.Tag) + ".");
            return string.Join("\\", values.Select(v => v.ToString(null, CultureInfo.InvariantCulture)));
        }

        /// <summary>The value multiplicity. fo-dicom counts the bytes or words of the binary VRs, whose VM is 1 (PS3.5 6.4).</summary>
        private static int Multiplicity(DicomElement element)
            => IsBinary(element.ValueRepresentation) ? (element.Buffer.Size == 0 ? 0 : 1) : element.Count;

        private static bool IsBinary(DicomVR vr)
            => vr == DicomVR.OB || vr == DicomVR.OW || vr == DicomVR.OF || vr == DicomVR.OD || vr == DicomVR.OL || vr == DicomVR.OV || vr == DicomVR.UN;

        private static string Hex(IByteBuffer buffer)
        {
            byte[] data = buffer.Data;
            string hex = Convert.ToHexString(data, 0, Math.Min(data.Length, MaxBinaryBytes));
            return data.Length > MaxBinaryBytes ? hex + "..." : hex;
        }

        private static string Format(DicomTag tag)
            => "(" + tag.Group.ToString("X4", CultureInfo.InvariantCulture) + "," + tag.Element.ToString("X4", CultureInfo.InvariantCulture) + ")";

        private static string Keyword(DicomTag tag)
        {
            string keyword = tag.DictionaryEntry.Keyword;
            return string.IsNullOrEmpty(keyword) || tag.DictionaryEntry == DicomDictionary.UnknownTag ? "Unknown" : keyword;
        }
    }
}
