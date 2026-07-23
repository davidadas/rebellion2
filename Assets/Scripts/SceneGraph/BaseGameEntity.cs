using System;
using System.Collections.Generic;
using Rebellion.Game.Encyclopedia;
using Rebellion.Util.Extensions;
using Rebellion.Util.Serialization;

namespace Rebellion.SceneGraph
{
    /// <summary>
    /// Base implementation of the <see cref="IGameEntity"/> interface.
    /// </summary>
    [PersistableObject]
    public class BaseGameEntity : IGameEntity
    {
        private static readonly object _instanceIdLock = new object();
        private static Random _deterministicInstanceIdProvider;

        [CloneIgnore]
        private string _instanceId;

        [CloneIgnore]
        public string InstanceID
        {
            get => _instanceId ??= CreateInstanceId();
            set => _instanceId = value;
        }

        public static void SetInstanceIdSeed(int? seed)
        {
            lock (_instanceIdLock)
                _deterministicInstanceIdProvider = seed.HasValue ? new Random(seed.Value) : null;
        }

        private static string CreateInstanceId()
        {
            lock (_instanceIdLock)
            {
                if (_deterministicInstanceIdProvider == null)
                    return Guid.NewGuid().ToString("N");

                byte[] bytes = new byte[16];
                _deterministicInstanceIdProvider.NextBytes(bytes);
                return new Guid(bytes).ToString("N");
            }
        }

        internal string PeekInstanceID()
        {
            return _instanceId ?? string.Empty;
        }

        public string TypeID { get; set; }
        public string DisplayName { get; set; }
        public string DisplayImagePath { get; set; }
        public string SmallDisplayImagePath { get; set; }
        public string MessageImagePath { get; set; }
        public string InTransitImagePath { get; set; }
        public string InTransitSmallImagePath { get; set; }
        public string DamagedImagePath { get; set; }
        public string DamagedSmallImagePath { get; set; }
        public string CapturedOverlayImagePath { get; set; }
        public string InjuredImagePath { get; set; }
        public string Description { get; set; }
        public string EncyclopediaImagePath { get; set; }
        public List<EncyclopediaEntryStat> EncyclopediaStats { get; set; } =
            new List<EncyclopediaEntryStat>();
        public string EncyclopediaDescription { get; set; }

        /// <summary>
        /// Returns the instance ID of the entity.
        /// </summary>
        /// <returns>The instance ID of the entity.</returns>
        public string GetInstanceID()
        {
            return InstanceID;
        }

        /// <summary>
        /// Returns the TypeID of the entity.
        /// </summary>
        /// <returns>The TypeID of the entity.</returns>
        public string GetTypeID()
        {
            return TypeID;
        }

        /// <summary>
        /// Returns the DisplayName of the entity.
        /// </summary>
        /// <returns></returns>
        public string GetDisplayName()
        {
            return DisplayName;
        }

        /// <summary>
        /// Returns the DisplayImagePath of the entity.
        /// </summary>
        /// <returns>The DisplayImagePath of the entity.</returns>
        public string GetDisplayImagePath()
        {
            return DisplayImagePath;
        }
    }
}
