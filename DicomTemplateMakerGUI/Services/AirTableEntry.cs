using ROIOntologyClass;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// Represents a single ROI entry in an AirTable database.
    /// Maps directly to AirTable field names for serialization.
    /// </summary>
    public class AirTableEntry
    {
        // Core ROI identification
        public string Structure { get; set; }
        public string CommonName { get; set; }
        public string Type { get; set; }

        // Color information
        public List<string> Colors_RGB { get; set; }
        public string RGB { get; set; }

        // Template associations
        public List<string> Template_Recommend { get; set; } = new List<string>();
        public List<string> Template_Consider { get; set; } = new List<string>();

        // Ontology coding
        public string SchemeCode { get; set; }
        public string Scheme { get; set; }
        public string ContextGroupVersion { get; set; }
        public string MappingResource { get; set; }
        public string ContextIdentifier { get; set; }
        public string MappingResourceName { get; set; }
        public string MappingResourceUID { get; set; }
        public string ContextUID { get; set; }

        // AirTable record ID
        public string Id { get; set; }

        // TG-263 naming variants
        public string TG_263 { get; set; }
        public string TG_263R { get; set; }
        public string TG_263Spanish { get; set; }
        public string TG_263SpanishR { get; set; }
        public string TG_263French { get; set; }
        public string TG_263FrenchR { get; set; }

        // DVH display properties
        public string DVH_Color { get; set; }
        public string DVH_Style { get; set; }
        public string DVH_Width { get; set; }
        public string DVH_Type_Index { get; set; }
        public string DVH_ContourStyle { get; set; }

        /// <summary>
        /// Default constructor with default ontology values.
        /// </summary>
        public AirTableEntry()
        {
            var defaultOntology = new OntologyCodeClass();
            Scheme = defaultOntology.Scheme;
            CommonName = null;
            ContextGroupVersion = defaultOntology.ContextGroupVersion;
            MappingResource = defaultOntology.MappingResource;
            ContextIdentifier = defaultOntology.ContextIdentifier;
            MappingResourceName = defaultOntology.MappingResourceName;
            MappingResourceUID = defaultOntology.MappingResourceUID;
            ContextUID = defaultOntology.ContextUID;
        }

        /// <summary>
        /// Creates an AirTableEntry from an ROIClass instance.
        /// </summary>
        public AirTableEntry(ROIClass roi)
        {
            if (roi == null) throw new ArgumentNullException(nameof(roi));

            var ontology = roi.Ontology_Class ?? new OntologyCodeClass();

            // Ontology information
            Scheme = ontology.Scheme;
            SchemeCode = ontology.CodeValue;
            CommonName = ontology.CodeMeaning;
            ContextGroupVersion = ontology.ContextGroupVersion;
            MappingResource = ontology.MappingResource;
            ContextIdentifier = ontology.ContextIdentifier;
            MappingResourceName = ontology.MappingResourceName;
            MappingResourceUID = ontology.MappingResourceUID;
            ContextUID = ontology.ContextUID;

            // ROI information
            Structure = roi.ROIName;
            Type = roi.ROI_Interpreted_type;
            Colors_RGB = new List<string> { $"Auto:{roi.R},{roi.G},{roi.B}" };
            RGB = $"{roi.R},{roi.G},{roi.B}";

            // DVH properties
            DVH_Color = roi.DVHLineColor;
            DVH_Style = roi.DVHLineStyle;
            DVH_Width = roi.DVHLineWidth;
            DVH_Type_Index = roi.TypeIndex;
            DVH_ContourStyle = roi.ContourStyle;
        }

        /// <summary>
        /// Converts this AirTableEntry to an ROIClass instance.
        /// </summary>
        public ROIClass ToROIClass(bool include = true)
        {
            string codeMeaning = !string.IsNullOrEmpty(CommonName) ? CommonName : Structure;

            var ontology = new OntologyCodeClass(
                name: codeMeaning,
                code_value: SchemeCode ?? "",
                scheme_designated: Scheme ?? "FMA",
                group_version: ContextGroupVersion,
                mapping_resource: MappingResource,
                context_identifier: ContextIdentifier,
                mapping_resource_name: MappingResourceName,
                mapping_resource_uid: MappingResourceUID,
                context_uid: ContextUID
            );

            // Parse colors
            byte r = 255, g = 0, b = 0; // Default red
            if (!string.IsNullOrEmpty(RGB))
            {
                var colorParts = RGB.Split(',');
                if (colorParts.Length >= 3)
                {
                    byte.TryParse(colorParts[0].Trim(), out r);
                    byte.TryParse(colorParts[1].Trim(), out g);
                    byte.TryParse(colorParts[2].Trim(), out b);
                }
            }
            else if (Colors_RGB != null && Colors_RGB.Count > 0)
            {
                // Parse "Auto:R,G,B" format
                var colorString = Colors_RGB[0];
                if (colorString.Contains(":"))
                {
                    var parts = colorString.Split(':')[1].Split(',');
                    if (parts.Length >= 3)
                    {
                        byte.TryParse(parts[0].Trim(), out r);
                        byte.TryParse(parts[1].Trim(), out g);
                        byte.TryParse(parts[2].Trim(), out b);
                    }
                }
            }

            var roi = new ROIClass(r, g, b, Structure, Type ?? "ORGAN", ontology);
            roi.Include = include;

            // Apply DVH properties if present
            if (!string.IsNullOrEmpty(DVH_Color))
            {
                roi.DVHLineColor = DVH_Color;
                roi.build_dvh_line_color();
            }
            if (!string.IsNullOrEmpty(DVH_Style))
            {
                roi.DVHLineStyle = DVH_Style;
            }
            if (!string.IsNullOrEmpty(DVH_Width))
            {
                roi.DVHLineWidth = DVH_Width;
            }
            if (!string.IsNullOrEmpty(DVH_Type_Index))
            {
                roi.TypeIndex = DVH_Type_Index;
            }
            if (!string.IsNullOrEmpty(DVH_ContourStyle))
            {
                roi.ContourStyle = DVH_ContourStyle;
            }

            return roi;
        }

        /// <summary>
        /// Creates an ROIWrapper from this entry (for language support).
        /// </summary>
        public ROIWrapper ToROIWrapper(bool include = true)
        {
            var roi = ToROIClass(include);
            return new ROIWrapper(
                roi,
                Structure,
                TG_263R,
                TG_263Spanish,
                TG_263SpanishR,
                TG_263French,
                TG_263FrenchR
            );
        }

        /// <summary>
        /// Gets the list of properties that differ between this entry and another.
        /// </summary>
        public List<string> GetDifferences(AirTableEntry other)
        {
            var differences = new List<string>();
            if (other == null) return differences;

            foreach (var prop in GetType().GetProperties())
            {
                var thisValue = prop.GetValue(this);
                var otherValue = prop.GetValue(other);

                if (thisValue is IList<string> thisList && otherValue is IList<string> otherList)
                {
                    if (!thisList.SequenceEqual(otherList))
                    {
                        differences.Add(prop.Name);
                    }
                }
                else if (!Equals(thisValue, otherValue))
                {
                    differences.Add(prop.Name);
                }
            }

            return differences;
        }

        /// <summary>
        /// Checks if this entry has any language variants (Spanish, French).
        /// </summary>
        public bool HasLanguageVariants =>
            !string.IsNullOrEmpty(TG_263Spanish) ||
            !string.IsNullOrEmpty(TG_263SpanishR) ||
            !string.IsNullOrEmpty(TG_263French) ||
            !string.IsNullOrEmpty(TG_263FrenchR);

        /// <summary>
        /// Checks if this entry has laterality variants.
        /// </summary>
        public bool HasLateralityVariants =>
            !string.IsNullOrEmpty(TG_263R) ||
            !string.IsNullOrEmpty(TG_263SpanishR) ||
            !string.IsNullOrEmpty(TG_263FrenchR);
    }
}