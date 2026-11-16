using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Rebellion.Content.Modio
{
    /// <summary>
    /// Describes a page of publicly available community mods.
    /// </summary>
    [DataContract]
    public sealed class ModioCatalogPage
    {
        [DataMember(Name = "data")]
        public ModioMod[] Mods { get; set; } = Array.Empty<ModioMod>();

        [DataMember(Name = "result_offset")]
        public int Offset { get; set; }

        [DataMember(Name = "result_limit")]
        public int Limit { get; set; }

        [DataMember(Name = "result_total")]
        public int Total { get; set; }
    }

    /// <summary>
    /// Describes one community mod and its currently published archive.
    /// </summary>
    [DataContract]
    public sealed class ModioMod
    {
        [DataMember(Name = "id")]
        public long ID { get; set; }

        [DataMember(Name = "game_id")]
        public long GameID { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "summary")]
        public string Summary { get; set; }

        [DataMember(Name = "description_plaintext")]
        public string Description { get; set; }

        [DataMember(Name = "submitted_by")]
        public ModioAuthor Author { get; set; }

        [DataMember(Name = "logo")]
        public ModioLogo Logo { get; set; }

        [DataMember(Name = "profile_url")]
        public string WebsiteURL { get; set; }

        [DataMember(Name = "modfile")]
        public ModioFile File { get; set; }
    }

    /// <summary>
    /// Identifies the creator shown on a community mod's public profile.
    /// </summary>
    [DataContract]
    public sealed class ModioAuthor
    {
        [DataMember(Name = "username")]
        public string Name { get; set; }
    }

    /// <summary>
    /// Supplies a creator-uploaded thumbnail without requiring the original image.
    /// </summary>
    [DataContract]
    public sealed class ModioLogo
    {
        [DataMember(Name = "thumb_640x360")]
        public string ThumbnailURL { get; set; }
    }

    /// <summary>
    /// Identifies a published archive and the information needed to verify its download.
    /// </summary>
    [DataContract]
    public sealed class ModioFile
    {
        [DataMember(Name = "id")]
        public long ID { get; set; }

        [DataMember(Name = "mod_id")]
        public long ModID { get; set; }

        [DataMember(Name = "filesize")]
        public long Size { get; set; }

        [DataMember(Name = "version")]
        public string Version { get; set; }

        [DataMember(Name = "filehash")]
        public ModioFileHash Hash { get; set; }

        [DataMember(Name = "download")]
        public ModioDownload Download { get; set; }
    }

    /// <summary>
    /// Contains the archive digest supplied by mod.io.
    /// </summary>
    [DataContract]
    public sealed class ModioFileHash
    {
        [DataMember(Name = "md5")]
        public string MD5 { get; set; }
    }

    /// <summary>
    /// Contains the temporary archive download address supplied by mod.io.
    /// </summary>
    [DataContract]
    public sealed class ModioDownload
    {
        [DataMember(Name = "binary_url")]
        public string URL { get; set; }
    }

    /// <summary>
    /// Reads and writes public catalog profiles and managed-installation records.
    /// </summary>
    internal static class ModioJson
    {
        /// <summary>
        /// Reads one JSON contract from a stream.
        /// </summary>
        /// <typeparam name="T">The contract type.</typeparam>
        /// <param name="stream">The readable JSON stream.</param>
        /// <returns>The deserialized contract.</returns>
        internal static T Read<T>(Stream stream)
        {
            return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }

        /// <summary>
        /// Writes one JSON contract to a stream.
        /// </summary>
        /// <typeparam name="T">The contract type.</typeparam>
        /// <param name="stream">The writable destination.</param>
        /// <param name="value">The contract to write.</param>
        internal static void Write<T>(Stream stream, T value)
        {
            new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
        }
    }
}
