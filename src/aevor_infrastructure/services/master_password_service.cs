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

public sealed class MasterPasswordService : IMasterPasswordService
{

    private const int SaltBytes            = 32;
    private const int HashBytes            = 32;
    private const int Parallelism          = 8;
    private const int MemorySizeKiB        = 65536;
    private const int Iterations           = 4;

    private static readonly string HashFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aevor",
        "master.hash");

    private sealed class HashPayload
    {
        [JsonPropertyName("salt")] public string Salt { get; set; } = string.Empty;
        [JsonPropertyName("hash")] public string Hash { get; set; } = string.Empty;
    }

    private volatile bool _isSessionActive;
    private readonly ILogger<MasterPasswordService> _logger;

    public MasterPasswordService(ILogger<MasterPasswordService> logger)
    {
        _logger = logger;
    }

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

            return false;
        }
    }

    public async Task SetupPasswordAsync(string password)
    {

        var passwordChars = password.ToCharArray();
        try
        {

            var salt = RandomNumberGenerator.GetBytes(SaltBytes);

            var hash = await ComputeArgon2idAsync(passwordChars, salt).ConfigureAwait(false);

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

            var candidateHash = await ComputeArgon2idAsync(passwordChars, storedSalt).ConfigureAwait(false);

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

    private static Task<byte[]> ComputeArgon2idAsync(char[] password, byte[] salt)
    {
        return Task.Run(() =>
        {

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

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void ZeroChars(char[] chars)
    {
        for (int i = 0; i < chars.Length; i++)
            chars[i] = '\0';
    }
}
