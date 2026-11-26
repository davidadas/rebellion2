using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>
    /// Base state shared by physical components installed on a ship.
    /// </summary>
    [PersistableObject]
    public abstract class ShipComponent : BaseGameEntity
    {
        public int Health { get; set; }
        public bool Targetable { get; set; }
        public string ModelNodePath { get; set; }

        /// <summary>
        /// Copies state shared by every ship component.
        /// </summary>
        /// <param name="copy">The destination component.</param>
        protected void CopyComponentStateTo(ShipComponent copy)
        {
            copy.Health = Health;
            copy.Targetable = Targetable;
            copy.ModelNodePath = ModelNodePath;
            CopyStateTo(copy);
        }

        /// <summary>
        /// Creates an independent copy of the component.
        /// </summary>
        /// <returns>The copied component.</returns>
        public abstract ShipComponent CreateCopy();
    }

    /// <summary>
    /// Provides sublight movement and maneuvering.
    /// </summary>
    [PersistableObject]
    public sealed class Engine : ShipComponent
    {
        public int SublightSpeed { get; set; }
        public int Maneuverability { get; set; }

        /// <summary>
        /// Creates an independent copy of this engine.
        /// </summary>
        /// <returns>The copied engine.</returns>
        public override ShipComponent CreateCopy()
        {
            Engine copy = new Engine
            {
                SublightSpeed = SublightSpeed,
                Maneuverability = Maneuverability,
            };
            CopyComponentStateTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// Provides hyperspace travel.
    /// </summary>
    [PersistableObject]
    public sealed class Hyperdrive : ShipComponent
    {
        public int Rating { get; set; }

        /// <summary>
        /// Creates an independent copy of this hyperdrive.
        /// </summary>
        /// <returns>The copied hyperdrive.</returns>
        public override ShipComponent CreateCopy()
        {
            Hyperdrive copy = new Hyperdrive { Rating = Rating };
            CopyComponentStateTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// Produces and recharges a ship's shields.
    /// </summary>
    [PersistableObject]
    public sealed class ShieldGenerator : ShipComponent
    {
        public int Capacity { get; set; }
        public int RechargeRate { get; set; }

        /// <summary>
        /// Creates an independent copy of this shield generator.
        /// </summary>
        /// <returns>The copied shield generator.</returns>
        public override ShipComponent CreateCopy()
        {
            ShieldGenerator copy = new ShieldGenerator
            {
                Capacity = Capacity,
                RechargeRate = RechargeRate,
            };
            CopyComponentStateTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// Houses starfighters aboard a ship.
    /// </summary>
    [PersistableObject]
    public sealed class StarfighterBay : ShipComponent
    {
        public int Capacity { get; set; }

        /// <summary>
        /// Creates an independent copy of this starfighter bay.
        /// </summary>
        /// <returns>The copied starfighter bay.</returns>
        public override ShipComponent CreateCopy()
        {
            StarfighterBay copy = new StarfighterBay { Capacity = Capacity };
            CopyComponentStateTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// Projects a gravity well that prevents hyperspace travel.
    /// </summary>
    [PersistableObject]
    public sealed class GravityWellGenerator : ShipComponent
    {
        /// <summary>
        /// Creates an independent copy of this gravity-well generator.
        /// </summary>
        /// <returns>The copied gravity-well generator.</returns>
        public override ShipComponent CreateCopy()
        {
            GravityWellGenerator copy = new GravityWellGenerator();
            CopyComponentStateTo(copy);
            return copy;
        }
    }
}
