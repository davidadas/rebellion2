using System;
using System.IO;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;
using UnityEngine;

namespace Rebellion.Core.Configuration
{
    /// <summary>
    /// Loads GameConfig from Resources/Configs/GameConfig.xml.
    /// Simple static data loader - NOT a complex state serializer.
    /// </summary>
    public static class ConfigLoader
    {
        private const string CONFIG_PATH = "Configs/GameConfig";

        /// <summary>
        /// Loads runtime simulation configuration from XML.
        /// Throws InvalidOperationException if file not found or deserialization fails.
        /// </summary>
        /// <returns>The loaded GameConfig.</returns>
        public static GameConfig LoadGameConfig()
        {
            TextAsset configAsset = Resources.Load<TextAsset>(CONFIG_PATH);
            if (configAsset == null)
            {
                throw new InvalidOperationException(
                    $"GameConfig.xml not found at Resources/{CONFIG_PATH}"
                );
            }

            try
            {
                var serializer = new GameSerializer(typeof(GameConfig));
                using (var reader = new StringReader(configAsset.text))
                {
                    GameConfig config = (GameConfig)serializer.Deserialize(reader);
                    config.Validate();
                    GameLogger.Log("GameConfig.xml loaded and validated successfully");
                    return config;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load GameConfig.xml: {ex.Message}");
            }
        }
    }
}
