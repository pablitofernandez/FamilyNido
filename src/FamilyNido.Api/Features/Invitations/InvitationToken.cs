using System.Security.Cryptography;
using System.Text;

namespace FamilyNido.Api.Features.Invitations;

/// <summary>
/// Helpers to generate and hash invitation tokens. The raw token only ever
/// lives on the wire (email + URL); the database stores its SHA-256 so a
/// leaked dump cannot replay pending invitations.
/// </summary>
internal static class InvitationToken
{
    /// <summary>Length in bytes of the random material used to build a token.</summary>
    private const int RawByteLength = 32;

    /// <summary>
    /// Generates a fresh, URL-safe token. 32 bytes of randomness encoded with
    /// base64url is well over the security threshold for a one-time use token.
    /// </summary>
    public static string GenerateRaw()
    {
        Span<byte> bytes = stackalloc byte[RawByteLength];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    /// <summary>Computes the SHA-256 of the raw token.</summary>
    public static byte[] Hash(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        return SHA256.HashData(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var s = Convert.ToBase64String(bytes);
        // Standard "URL-safe base64" without padding.
        return s.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
