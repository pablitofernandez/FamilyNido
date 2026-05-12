using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FamilyNido.Api.Features.Integrations;

/// <summary>
/// Cryptographic helpers for the API-key plaintext / hash / prefix triplet.
/// All keys share a stable visual prefix so they are easy to grep for in
/// configuration files and logs.
/// </summary>
public static class IntegrationTokens
{
    /// <summary>Visual marker every plaintext token starts with.</summary>
    public const string PlaintextPrefix = "bxn_";

    /// <summary>
    /// Number of leading characters preserved in clear text alongside the hash.
    /// Long enough to disambiguate a few keys at a glance ("bxn_a1b2c3d4"),
    /// short enough that an attacker who steals it cannot brute-force the rest.
    /// </summary>
    public const int PrefixDisplayLength = 12;

    /// <summary>
    /// Generate a fresh random plaintext token. Returns the full secret —
    /// callers persist only the digest plus <see cref="PrefixOf(string)"/>.
    /// </summary>
    public static string Generate()
    {
        // 32 bytes = 256 bits of entropy; base64url is URL-safe and fits
        // headers without quoting headaches.
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        return PlaintextPrefix + Base64UrlEncoder.Encode(randomBytes);
    }

    /// <summary>
    /// SHA-256 hex digest of <paramref name="plaintext"/>. We do not need
    /// PBKDF2/Argon2 here because the source material already carries 256
    /// bits of randomness — slow hashing would only protect against weak
    /// secrets, which by construction we never produce.
    /// </summary>
    public static string Hash(string plaintext)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Public-safe visual prefix used to identify a key in the UI.</summary>
    public static string PrefixOf(string plaintext)
        => plaintext.Length <= PrefixDisplayLength ? plaintext : plaintext[..PrefixDisplayLength];
}
