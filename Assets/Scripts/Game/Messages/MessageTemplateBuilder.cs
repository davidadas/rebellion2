using System.Collections.Generic;
using Rebellion.Game.Factions;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Builds concrete faction messages from configured message templates.
    /// </summary>
    internal sealed class MessageTemplateBuilder
    {
        /// <summary>
        /// Builds a concrete message from a message definition and template values.
        /// </summary>
        /// <param name="definition">The message definition to build from.</param>
        /// <param name="faction">The faction receiving the message.</param>
        /// <param name="values">The template values to apply.</param>
        /// <param name="imageFaction">The faction used to resolve faction-specific image paths.</param>
        /// <param name="imageOverride">The image path to use instead of the definition image path.</param>
        /// <param name="overlayImagePath">The overlay image path to assign to the message.</param>
        /// <param name="officerVoicePath">The officer voice path to assign to the message.</param>
        /// <returns>The built message, or null when no definition was provided.</returns>
        public Message Build(
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

            string title = Interpolate(definition.TitleTemplate, values);
            string body = Interpolate(definition.BodyTemplate, values);

            return new Message(definition.MessageType, title, body)
            {
                DisplayName = title,
                DisplayImageKey = definition.ImageKey,
                DisplayImagePath =
                    imageOverride
                    ?? GetAssetPath(
                        definition.ImagePath,
                        definition.ImagePaths,
                        (imageFaction ?? faction)?.InstanceID
                    ),
                OverlayImagePath = overlayImagePath,
                MessageVoicePath = GetAssetPath(
                    definition.VoicePath,
                    definition.VoicePaths,
                    faction?.InstanceID
                ),
                OfficerVoicePath = officerVoicePath,
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
        private static string Interpolate(string template, Dictionary<string, string> values)
        {
            string result = template ?? string.Empty;
            if (values == null)
                return result;

            foreach (KeyValuePair<string, string> value in values)
                result = result.Replace("{" + value.Key + "}", value.Value ?? string.Empty);

            return result;
        }
    }
}
