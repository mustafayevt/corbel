using System.Globalization;
using Corbel.Domain.Entities;
using Isopoh.Cryptography.Argon2;
using Microsoft.AspNetCore.Identity;

namespace Corbel.Infrastructure.Auth;

/// <summary>
/// Replaces Identity's default PBKDF2 hasher with Argon2id (OWASP-aligned parameters). Stored hashes are
/// PHC-encoded, so the cost parameters travel with each hash; <see cref="VerifyHashedPassword"/> returns
/// <see cref="PasswordVerificationResult.SuccessRehashNeeded"/> when an older hash's parameters differ from
/// the current configuration, letting Identity transparently upgrade it on the next successful sign-in.
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher<AppUser>
{
    private const int TimeCost = 2;            // iterations
    private const int MemoryCostKiB = 19_456;  // 19 MiB
    private const int Parallelism = 1;
    private const int HashLength = 32;

    public string HashPassword(AppUser user, string password)
        => Argon2.Hash(
            password: password,
            timeCost: TimeCost,
            memoryCost: MemoryCostKiB,
            parallelism: Parallelism,
            type: Argon2Type.HybridAddressing, // Argon2id
            hashLength: HashLength);

    public PasswordVerificationResult VerifyHashedPassword(AppUser user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
            return PasswordVerificationResult.Failed;

        if (!TryVerify(hashedPassword, providedPassword))
            return PasswordVerificationResult.Failed;

        return NeedsRehash(hashedPassword)
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }

    private static bool TryVerify(string hashedPassword, string providedPassword)
    {
        try
        {
            return Argon2.Verify(hashedPassword, providedPassword);
        }
#pragma warning disable CA1031 // A malformed stored hash must fail closed, never surface as a 500 during sign-in.
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }

    /// <summary>Parses the PHC parameters (<c>$argon2id$v=19$m=..,t=..,p=..$..</c>) and flags a rehash when they drift from the current cost.</summary>
    private static bool NeedsRehash(string encoded)
    {
        if (!encoded.StartsWith("$argon2id$", StringComparison.Ordinal))
            return true; // wrong algorithm/variant → upgrade

        var segments = encoded.Split('$');
        if (segments.Length < 5)
            return true;

        int? m = null, t = null, p = null;
        foreach (var pair in segments[3].Split(','))
        {
            var kv = pair.Split('=');
            if (kv.Length != 2 || !int.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                continue;

            switch (kv[0])
            {
                case "m": m = value; break;
                case "t": t = value; break;
                case "p": p = value; break;
                default: break;
            }
        }

        return m != MemoryCostKiB || t != TimeCost || p != Parallelism;
    }
}
