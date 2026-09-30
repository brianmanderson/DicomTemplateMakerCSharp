using System;
using System.Security.Cryptography;
using System.Text;
using TemplateSync.Credentials;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// Encrypts Airtable tokens with Windows DPAPI for the current Windows user, so the token file
    /// is unreadable to other accounts and useless if copied to another machine.
    /// </summary>
    internal sealed class DpapiTokenProtector : ITokenProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DicomTemplateMaker.AirtableToken.v1");

        public string Protect(string plaintext)
        {
            byte[] data = Encoding.UTF8.GetBytes(plaintext);
            return Convert.ToBase64String(ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser));
        }

        public string Unprotect(string protectedValue)
        {
            byte[] data = Convert.FromBase64String(protectedValue);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser));
        }
    }
}
