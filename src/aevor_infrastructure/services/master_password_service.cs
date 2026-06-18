using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aevor.Application.Interfaces;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Aevor.Infrastructure.Services;

/// <summary>
/// Argon2id-based master password service.
///
/// Security properties:
///   • 32-byte cryptographically random salt per setup
///   • Argon2id: DegreeOfParallelism=8, MemorySize=65536 KiB (64 MB), Iterations=4
///   • 32-byte hash output
///   • CryptographicOperations.FixedTimeEquals for constant-time comparison
///   • Password char[] zeroed immediately after hashing
///   • No plaintext written to any log entry
/// </summary>
public sealed class MasterPasswordService : IMasterPasswordService
{
    // ── Argon2id parameters ───────────────────────────────────────────────
    private const int SaltBytes            = 32;
    private const int HashBytes            = 32;
    private const int Parallelism          = 8;
    private const int MemorySizeKiB        = 65536; // 64 MB
    private const int Iterations           = 4;

    // ── Storage path ─────────────────────────────────────────────────────
    private static readonly string HashFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aevor",
        "master.hash");

    // ── JSON payload ─────────────────────────────────────────────────────
    private sealed class HashPayload
    {
        [JsonPropertyName("salt")] public string Salt { get; set; } = string.Empty;
        [JsonPropertyName("hash")] public string Hash { get; set; } = string.Empty;
    }

    // ── State ─────────────────────────────────────────────────────────────
    private volatile bool _isSessionActive;
    private readonly ILogger<MasterPasswordService> _logger;

    public MasterPasswordService(ILogger<MasterPasswordService> logger)
    {
        _logger = logger;
    }

    // ── IMasterPasswordService ────────────────────────────────────────────

    public bool IsSessionActive => _isSessionActive;

    public bool IsPasswordConfigured()
    {
        if (!File.Exists(HashFilePath))
            return false;

        try
        {
            var text = File.ReadAllText(HashFilePath);
            var payload = JsonSerializer.Deserialize<HashPayload>(text);
            return payload is { Salt.Length: > 0, Hash.Length: > 0 };
        }
        catch
        {
            // Corrupt or unreadable file — treat as not configured
            return false;
        }
    }

    public async Task SetupPasswordAsync(string password)
    {
        // Allocate a char[] copy so we can zero it after hashing
        var passwordChars = password.ToCharArray();
        try
        {
            // Generate fresh random salt
            var salt = RandomNumberGenerator.GetBytes(SaltBytes);

            // Hash
            var hash = await ComputeArgon2idAsync(passwordChars, salt).ConfigureAwait(false);

            // Persist
            EnsureDirectory();
            var payload = new HashPayload
            {
                Salt = Convert.ToBase64String(salt),
                Hash = Convert.ToBase64String(hash)
            };
            var json = JsonSerializer.Serialize(payload);
            await File.WriteAllTextAsync(HashFilePath, json).ConfigureAwait(false);

            _isSessionActive = true;
            _logger.LogInformation("Master password configured successfully.");
        }
        finally
        {
            // Zero the password characters — do not leave plaintext in heap
            ZeroChars(passwordChars);
        }
    }

    public async Task<bool> VerifyPasswordAsync(string password)
    {
        var passwordChars = password.ToCharArray();
        try
        {
            if (!File.Exists(HashFilePath))
            {
                _logger.LogWarning("VerifyPassword called but master.hash does not exist.");
                return false;
            }

            // Read stored payload
            var text = await File.ReadAllTextAsync(HashFilePath).ConfigureAwait(false);
            HashPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<HashPayload>(text);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "master.hash contains invalid JSON.");
                return false;
            }

            if (payload is null || string.IsNullOrEmpty(payload.Salt) || string.IsNullOrEmpty(payload.Hash))
            {
                _logger.LogError("master.hash payload is incomplete.");
                return false;
            }

            var storedSalt = Convert.FromBase64String(payload.Salt);
            var storedHash = Convert.FromBase64String(payload.Hash);

            // Re-hash the candidate password with the stored salt
            var candidateHash = await ComputeArgon2idAsync(passwordChars, storedSalt).ConfigureAwait(false);

            // Constant-time comparison — never use == on hashes
            bool match = CryptographicOperations.FixedTimeEquals(storedHash, candidateHash);

            if (match)
            {
                _isSessionActive = true;
                _logger.LogInformation("Master password verified successfully.");
            }
            else
            {
                _logger.LogWarning("Master password verification failed.");
            }

            return match;
        }
        finally
        {
            ZeroChars(passwordChars);
        }
    }

    public void ClearSession()
    {
        _isSessionActive = false;
        _logger.LogInformation("Master password session cleared.");
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static Task<byte[]> ComputeArgon2idAsync(char[] password, byte[] salt)
    {
        return Task.Run(() =>
        {
            // Encode password to UTF-8 bytes
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            try
            {
                using var argon2 = new Argon2id(passwordBytes)
                {
                    Salt                = salt,
                    DegreeOfParallelism = Parallelism,
                    MemorySize          = MemorySizeKiB,
                    Iterations          = Iterations
                };
                return argon2.GetBytes(HashBytes);
            }
            finally
            {
                // Zero the UTF-8 byte representation
                CryptographicOperations.ZeroMemory(passwordBytes);
            }
        });
    }

    private static void EnsureDirectory()
    {
        var dir = Path.GetDirectoryName(HashFilePath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>Overwrites every character in <paramref name="chars"/> with '\0'.</summary>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void ZeroChars(char[] chars)
    {
        for (int i = 0; i < chars.Length; i++)
            chars[i] = '\0';
    }
}
