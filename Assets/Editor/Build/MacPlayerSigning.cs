#if UNITY_STANDALONE_OSX
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

/// <summary>
/// Reuses a configured certificate when signing local macOS players and their companion editor.
/// </summary>
public static class MacPlayerSigning
{
    private const string _identityPreference = "Rebellion.Build.MacSigningIdentity";
    private const string _identityArgument = "-macSigningIdentity";

    /// <summary>
    /// Remembers the certificate fingerprint supplied on the command line for subsequent builds.
    /// </summary>
    public static void Configure()
    {
        string identity = GetArgument();
        if (identity == null)
            throw new BuildFailedException(
                $"Supply {_identityArgument} and a certificate SHA-1 fingerprint."
            );

        ValidateIdentity(identity);
        EditorPrefs.SetString(_identityPreference, identity);
    }

    /// <summary>
    /// Signs generated native code, then applies the configured identity to the app and its helpers.
    /// </summary>
    /// <param name="bundlePath">The completed macOS app bundle.</param>
    internal static void SignAppBundle(string bundlePath)
    {
        string identity = GetArgument() ?? EditorPrefs.GetString(_identityPreference, "-");
        ValidateIdentity(identity);
        UnityEditor.OSXStandalone.MacOSCodeSigning.CodeSignAppBundle(bundlePath);
        if (identity == "-")
            return;

        SignWithIdentity(bundlePath, identity);
        RunCodeSign("--verify", "--deep", "--strict", bundlePath);
    }

    /// <summary>
    /// Signs embedded companion apps before sealing their enclosing bundle with the same identity.
    /// </summary>
    /// <param name="bundlePath">The app bundle to sign.</param>
    /// <param name="identity">The installed signing certificate's SHA-1 fingerprint.</param>
    private static void SignWithIdentity(string bundlePath, string identity)
    {
        string helpers = Path.Combine(bundlePath, "Contents", "Helpers");
        if (Directory.Exists(helpers))
            foreach (
                string helper in Directory
                    .GetDirectories(helpers, "*.app")
                    .OrderBy(path => path, StringComparer.Ordinal)
            )
                SignWithIdentity(helper, identity);

        RunCodeSign(
            "--force",
            "--sign",
            identity,
            "--timestamp=none",
            "--preserve-metadata=identifier,entitlements,flags,runtime",
            bundlePath
        );
    }

    /// <summary>
    /// Runs the system signing tool and fails the build if signing or verification fails.
    /// </summary>
    /// <param name="arguments">Individual arguments passed without shell interpretation.</param>
    private static void RunCodeSign(params string[] arguments)
    {
        ProcessStartInfo startInfo = new("/usr/bin/codesign")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process =
            Process.Start(startInfo)
            ?? throw new BuildFailedException("Could not start macOS code signing.");
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new BuildFailedException($"macOS code signing failed: {error.Trim()}");
    }

    /// <summary>
    /// Reads an optional per-build signing identity from the command line.
    /// </summary>
    /// <returns>The supplied identity, or null when no override was supplied.</returns>
    private static string GetArgument()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length; index++)
        {
            if (!string.Equals(arguments[index], _identityArgument, StringComparison.Ordinal))
                continue;
            if (index + 1 >= arguments.Length)
                throw new BuildFailedException($"Missing value for {_identityArgument}.");

            return arguments[index + 1];
        }
        return null;
    }

    /// <summary>
    /// Requires an unambiguous certificate fingerprint or an explicit request for ad-hoc signing.
    /// </summary>
    /// <param name="identity">The requested certificate fingerprint or a single hyphen.</param>
    private static void ValidateIdentity(string identity)
    {
        if (identity != "-" && (identity.Length != 40 || !identity.All(Uri.IsHexDigit)))
            throw new BuildFailedException(
                "macOS signing requires a certificate SHA-1 fingerprint or '-' for ad-hoc signing."
            );
    }
}
#endif
