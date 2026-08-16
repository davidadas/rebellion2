using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Resolves configured message templates into transient delivery requests.
    /// </summary>
    internal sealed class MessageTemplateBuilder
    {
        private static readonly Regex _tokenPattern = new Regex(
            "\\{(?<name>[^{}]+)\\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant
        );

        /// <summary>
        /// Resolves a message definition and template values into a delivery request.
        /// </summary>
        /// <param name="definition">The message definition to build from.</param>
        /// <param name="faction">The faction receiving the message.</param>
        /// <param name="values">The template values to apply.</param>
        /// <param name="imageFaction">The faction used to resolve faction-specific image paths.</param>
        /// <param name="imageOverride">The image path to use instead of the definition image path.</param>
        /// <param name="overlayImagePath">The overlay image path to assign to the message.</param>
        /// <param name="officerVoicePath">The officer voice path to assign to the message.</param>
        /// <returns>The resolved request, or null when no definition was provided.</returns>
        public MessageRequestedResult Build(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values,
            Faction imageFaction = null,
            string imageOverride = null,
            string overlayImagePath = null,
            string officerVoicePath = null
        )
        {
            if (definition == null)
                return null;

            MessageBackgroundImage background = definition.BackgroundImage;
            if (background != null)
            {
                int sourceCount =
                    (string.IsNullOrWhiteSpace(background.Key) ? 0 : 1)
                    + (string.IsNullOrWhiteSpace(background.Path) ? 0 : 1)
                    + (string.IsNullOrWhiteSpace(background.Binding) ? 0 : 1);
                if (sourceCount != 1 || !string.IsNullOrWhiteSpace(background.Binding))
                    throw new InvalidOperationException(
                        "A message definition background requires exactly one Key or Path."
                    );
            }

            string title = Interpolate(definition.Subject, values);
            string body = Interpolate(definition.Body, values);

            return new MessageRequestedResult
            {
                Recipient = faction,
                MessageType = definition.MessageType,
                ResultType = definition.ResultType,
                Subject = title,
                Body = body,
                BackgroundImageKey = definition.BackgroundImage?.Key,
                BackgroundImagePath =
                    imageOverride
                    ?? GetAssetPath(
                        definition.BackgroundImage?.Path,
                        definition.ImagePaths,
                        (imageFaction ?? faction)?.InstanceID
                    ),
                OverlayImagePath = overlayImagePath,
                BackgroundAudioPath = GetAssetPath(
                    definition.BackgroundAudioPath,
                    definition.BackgroundAudioPaths,
                    faction?.InstanceID
                ),
                OfficerVoicePath = officerVoicePath ?? definition.OfficerVoicePath,
            };
        }

        /// <summary>
        /// Gets the configured asset path for a key.
        /// </summary>
        /// <param name="defaultPath">The fallback asset path.</param>
        /// <param name="keyedPaths">The keyed asset paths.</param>
        /// <param name="key">The key to resolve.</param>
        /// <returns>The keyed asset path when present; otherwise the fallback asset path.</returns>
        private static string GetAssetPath(
            string defaultPath,
            Dictionary<string, string> keyedPaths,
            string key
        )
        {
            if (
                !string.IsNullOrEmpty(key)
                && keyedPaths != null
                && keyedPaths.TryGetValue(key, out string path)
                && !string.IsNullOrEmpty(path)
            )
                return path;

            return defaultPath;
        }

        /// <summary>
        /// Applies template values to configured message text.
        /// </summary>
        /// <param name="template">The text template.</param>
        /// <param name="values">The template values to apply.</param>
        /// <returns>The interpolated text.</returns>
        internal static string Interpolate(string template, Dictionary<string, string> values)
        {
            string source = template ?? string.Empty;
            return _tokenPattern.Replace(
                source,
                match =>
                {
                    string name = match.Groups["name"].Value;
                    if (values == null || !values.TryGetValue(name, out string value))
                        throw new InvalidOperationException(
                            $"Message template references unknown value '{name}'."
                        );
                    return value ?? string.Empty;
                }
            );
        }
    }
}
