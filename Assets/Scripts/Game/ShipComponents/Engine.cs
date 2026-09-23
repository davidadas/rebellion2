using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>Provides sublight movement and maneuvering.</summary>
    [PersistableObject]
    public sealed class Engine : ShipComponent
    {
        public int SublightSpeed { get; set; }
        public int Maneuverability { get; set; }

        public override ShipComponent CreateCopy()
        {
            Engine copy = new Engine
            {
                Health = Health,
                SublightSpeed = SublightSpeed,
                Maneuverability = Maneuverability,
            };
            CopyEntityStateTo(copy);
            return copy;
        }
    }
}
