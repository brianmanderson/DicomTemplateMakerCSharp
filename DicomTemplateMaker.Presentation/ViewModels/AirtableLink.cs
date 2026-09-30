using System;
using System.Diagnostics.CodeAnalysis;
using TemplateSync.Credentials;

namespace DicomTemplateMakerGUI.ViewModels
{
    /// <summary>Reads the base and table ids out of an Airtable address such as https://airtable.com/app…/tbl…/viw….</summary>
    internal static class AirtableLink
    {
        /// <summary>True when the text is meant as an address rather than an id or a table name.</summary>
        public static bool LooksLikeLink([NotNullWhen(true)] string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string value = text.Trim();
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || value.IndexOf("airtable.com/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Finds the first path segment that is a base id and the first that is a table id. Either may be
        /// null (a base address has no table). Returns false when the text is not an address.
        /// </summary>
        public static bool TryParse(string? text, out string? baseId, out string? tableId)
        {
            baseId = null;
            tableId = null;
            if (!LooksLikeLink(text))
            {
                return false;
            }

            string value = text.Trim();
            if (!value.Contains("://", StringComparison.Ordinal))
            {
                value = "https://" + value;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            {
                return false;
            }

            foreach (string segment in uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (baseId == null && AirtableIds.IsBaseId(segment))
                {
                    baseId = segment;
                }
                else if (tableId == null && AirtableIds.IsTableId(segment))
                {
                    tableId = segment;
                }
            }

            return true;
        }
    }
}
