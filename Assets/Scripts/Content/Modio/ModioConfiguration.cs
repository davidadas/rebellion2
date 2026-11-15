using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using UnityEngine;

namespace Rebellion.Content.Modio
{
    /// <summary>
    /// Provides public catalog credentials from the development environment or a built resource.
    /// </summary>
    [DataContract]
    public sealed class ModioConfiguration
    {
        [DataMember(Name = "game_id")]
        public long GameID { get; set; }

        [DataMember(Name = "api_key")]
        public string APIKey { get; set; }

        [DataMember(Name = "api_base_url")]
        public string APIBaseURL { get; set; }

        public bool IsConfigured => GameID > 0 && !string.IsNullOrWhiteSpace(APIKey);

        public string BaseURL =>
            string.IsNullOrWhiteSpace(APIBaseURL)
                ? $"https://g-{GameID.ToString(CultureInfo.InvariantCulture)}.modapi.io/v1/"
                : APIBaseURL.TrimEnd('/') + "/";

        /// <summary>
        /// Loads environment overrides, falling back to the catalog configuration embedded at build time.
        /// </summary>
        /// <returns>The available catalog configuration, which may be unconfigured.</returns>
        public static ModioConfiguration Load()
        {
            ModioConfiguration environment = FromEnvironment();
            if (environment.IsConfigured)
                return environment;
            TextAsset resource = Resources.Load<TextAsset>("ModioSettings");
            if (resource == null)
                return environment;
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(resource.text));
            return ModioJson.Read<ModioConfiguration>(stream);
        }

        /// <summary>
        /// Reads catalog credentials without accepting a personal account access token.
        /// </summary>
        /// <returns>The current process's public catalog configuration.</returns>
        public static ModioConfiguration FromEnvironment()
        {
            long.TryParse(
                Environment.GetEnvironmentVariable("MODIO_GAME_ID"),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long gameID
            );
            return new ModioConfiguration
            {
                GameID = gameID,
                APIKey = Environment.GetEnvironmentVariable("MODIO_API_KEY"),
                APIBaseURL = Environment.GetEnvironmentVariable("MODIO_API_BASE_URL"),
            };
        }

        /// <summary>
        /// Writes the public catalog configuration for a player build.
        /// </summary>
        /// <param name="path">The generated resource path.</param>
        public void Write(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using FileStream stream = File.Create(path);
            ModioJson.Write(stream, this);
        }
    }
}
