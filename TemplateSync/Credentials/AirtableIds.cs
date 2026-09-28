using System.Text.RegularExpressions;

namespace TemplateSync.Credentials
{
    /// <summary>Format checks for the values a user types when connecting a table.</summary>
    public static class AirtableIds
    {
        private static readonly Regex BaseIdPattern = new Regex("^app[A-Za-z0-9]{14}$", RegexOptions.CultureInvariant);
        private static readonly Regex TableIdPattern = new Regex("^tbl[A-Za-z0-9]{14}$", RegexOptions.CultureInvariant);

        public static bool IsBaseId(string? value) => value != null && BaseIdPattern.IsMatch(value.Trim());

        public static bool IsTableId(string? value) => value != null && TableIdPattern.IsMatch(value.Trim());

        /// <summary>Personal access tokens look like "pat" + 14 characters + "." + 64 hex characters.</summary>
        public static bool LooksLikePersonalAccessToken(string? value)
        {
            if (value == null)
            {
                return false;
            }

            string token = value.Trim();
            return token.StartsWith("pat", System.StringComparison.Ordinal) && token.IndexOf('.') > 3 && token.Length >= 40;
        }

        /// <summary>Returns a user-facing problem description, or null when the values look valid.</summary>
        public static string? Validate(string? name, string? baseId, string? table, string? token)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Enter a name for this table.";
            }

            if (name!.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                return "The name contains characters that are not allowed in file names.";
            }

            if (!IsBaseId(baseId))
            {
                return "The base id should look like app followed by 14 letters or digits (copy it from the base's URL).";
            }

            if (string.IsNullOrWhiteSpace(table))
            {
                return "Enter the table id (tbl followed by 14 letters or digits) or the table's exact name.";
            }

            if (!LooksLikePersonalAccessToken(token))
            {
                return "The token should be an Airtable personal access token starting with \"pat\". Legacy API keys (starting with \"key\") no longer work.";
            }

            if (LeakedTokens.IsKnownLeaked(token))
            {
                return "This token was published in an old release of this program and must not be used. Create your own token at airtable.com/create/tokens.";
            }

            return null;
        }
    }
}
