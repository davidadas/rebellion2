using System.IO;
using System.Runtime.Serialization;

namespace Rebellion.Content.Modio
{
    /// <summary>
    /// Records the published archive installed by either the game or the launcher.
    /// </summary>
    [DataContract]
    public sealed class ModioInstallation
    {
        public const string FileName = ".modio.json";

        [DataMember(Name = "schema_version")]
        public int SchemaVersion { get; set; } = 1;

        [DataMember(Name = "game_id")]
        public long GameID { get; set; }

        [DataMember(Name = "mod_id")]
        public long ModID { get; set; }

        [DataMember(Name = "file_id")]
        public long FileID { get; set; }

        [DataMember(Name = "content_id")]
        public string ContentID { get; set; }

        [DataMember(Name = "content_version")]
        public string ContentVersion { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Reads the installation record stored beside a content definition.
        /// </summary>
        /// <param name="directory">The managed mod directory.</param>
        /// <returns>The installation record, or null when no record exists.</returns>
        public static ModioInstallation Read(string directory)
        {
            string path = Path.Combine(directory, FileName);
            if (!File.Exists(path))
                return null;
            using FileStream stream = File.OpenRead(path);
            return ModioJson.Read<ModioInstallation>(stream);
        }

        /// <summary>
        /// Writes the installation record into a staged, validated package.
        /// </summary>
        /// <param name="directory">The staged package directory.</param>
        public void Write(string directory)
        {
            using FileStream stream = File.Create(Path.Combine(directory, FileName));
            ModioJson.Write(stream, this);
        }
    }
}
