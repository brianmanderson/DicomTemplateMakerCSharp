using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace TemplateSync.Credentials
{
    /// <summary>
    /// SHA-256 fingerprints of Airtable personal access tokens that were published in this repository's
    /// history or in its release downloads (prefixes pat4, patK, patQ, patT). Files holding one of these
    /// are retired during migration instead of being imported, and they cannot be added as a connection.
    /// Only the hashes are kept; the tokens themselves must be revoked by their owners.
    /// </summary>
    public static class LeakedTokens
    {
        private static readonly HashSet<string> Fingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "23646c2b80d85fea400fb9a395677cea274755644c5e621f3da059020bc2daff", // pat4…: old TG-263 base, git history and v1.0.0 assets
            "1885b387825ca6387faa67c86420e0bae72fd1b169846643d2d15c5750152292", // patK…: third-party base, v1.0.0 assets only
            "660eebadde592c63a62523b1fc22338eb705409071feb508fb33e0397104153e", // patQ…: current TG-263 base, git history and v1.0.3/v1.0.4 assets
            "db15e18f12af37dff0cd91608a125aab995d0fdfd5c0f20a932ba9b6dffd8885", // patT…: UCSD base, git history and v1.0.0-v1.0.2 assets
        };

        public static bool IsKnownLeaked(string? token)
        {
            return !string.IsNullOrWhiteSpace(token) && Fingerprints.Contains(Sha256Hex(token!.Trim()));
        }

        internal static string Sha256Hex(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    text.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                }

                return text.ToString();
            }
        }
    }
}
