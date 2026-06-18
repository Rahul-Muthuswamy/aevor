namespace Aevor.Application.Interfaces;

/// <summary>
/// Manages the Aevor master password: setup, verification, and session state.
/// All implementations must use constant-time hash comparison and must never
/// write plaintext passwords to any log, file, or long-lived memory location.
/// </summary>
public interface IMasterPasswordService
{
    /// <summary>
    /// Returns true when %AppData%\Aevor\master.hash exists and contains valid JSON.
    /// </summary>
    bool IsPasswordConfigured();

    /// <summary>
    /// Hashes <paramref name="password"/> with Argon2id and writes the result to
    /// %AppData%\Aevor\master.hash. Sets <see cref="IsSessionActive"/> = true.
    /// The caller must not retain a reference to <paramref name="password"/> after
    /// this call returns.
    /// </summary>
    Task SetupPasswordAsync(string password);

    /// <summary>
    /// Reads master.hash, re-hashes <paramref name="password"/> with the stored salt,
    /// and compares using <see cref="System.Security.Cryptography.CryptographicOperations.FixedTimeEquals"/>.
    /// Returns true and sets <see cref="IsSessionActive"/> = true on match.
    /// </summary>
    Task<bool> VerifyPasswordAsync(string password);

    /// <summary>Sets <see cref="IsSessionActive"/> = false.</summary>
    void ClearSession();

    /// <summary>
    /// True after a successful <see cref="SetupPasswordAsync"/> or
    /// <see cref="VerifyPasswordAsync"/>. False on app start and after
    /// <see cref="ClearSession"/>.
    /// </summary>
    bool IsSessionActive { get; }
}
