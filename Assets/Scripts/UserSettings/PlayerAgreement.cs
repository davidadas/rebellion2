using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Identifies the agreement presented to a player and records acceptance separately from analytics.
/// </summary>
public sealed class PlayerAgreement
{
    public string Text { get; }
    public string PrivacyNotice { get; }
    public string Version { get; }

    /// <summary>
    /// Loads the bundled documents and identifies both normalized texts across desktop platforms.
    /// </summary>
    /// <param name="text">The complete player agreement.</param>
    /// <param name="privacyNotice">The accompanying privacy notice.</param>
    public PlayerAgreement(string text, string privacyNotice)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("The player agreement is missing.", nameof(text));
        if (string.IsNullOrWhiteSpace(privacyNotice))
            throw new ArgumentException("The privacy notice is missing.", nameof(privacyNotice));

        Text = text;
        PrivacyNotice = privacyNotice;
        using SHA256 hash = SHA256.Create();
        Version = BitConverter
            .ToString(
                hash.ComputeHash(
                    Encoding.UTF8.GetBytes(
                        text.Replace("\r\n", "\n") + "\0" + privacyNotice.Replace("\r\n", "\n")
                    )
                )
            )
            .Replace("-", string.Empty);
    }

    /// <summary>
    /// Checks whether the current local user explicitly accepted this agreement version.
    /// </summary>
    /// <param name="settings">The local user's settings.</param>
    /// <returns>Whether the stored acceptance matches this agreement.</returns>
    public bool IsAccepted(UserSettings settings)
    {
        return settings?.AcceptedAgreementVersion == Version;
    }
}
