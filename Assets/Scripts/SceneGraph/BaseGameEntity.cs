using System;
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
        [CloneIgnore]
        private string _instanceId;

        [CloneIgnore]
        public string InstanceID
        {
            get => _instanceId ??= Guid.NewGuid().ToString().Replace("-", "");
            set => _instanceId = value;
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
