using System.Security.Cryptography;
using System.Text;
using TemplateSync.Credentials;

namespace DicomTemplateMaker.Presentation.Tests.Fakes;

/// <summary>Reversible stand-in for DPAPI. With <see cref="CannotDecrypt"/> it behaves like another Windows account.</summary>
public sealed class FakeProtector : ITokenProtector
{
    public bool CannotDecrypt { get; init; }

    public int ProtectCalls { get; private set; }

    public string Protect(string plaintext)
    {
        ProtectCalls++;
        return "enc:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
    }

    public string Unprotect(string protectedValue)
    {
        if (CannotDecrypt)
        {
            throw new CryptographicException("Key not valid for use in specified state.");
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue.Substring(4)));
    }
}
