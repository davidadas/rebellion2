using System;
using System.Collections.Generic;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Factions
{
    /// <summary>
    /// Preserves a faction's off-map entities in the game graph.
    /// </summary>
    [PersistableObject(Name = "VoidPool")]
    public sealed class VoidPool : ContainerNode
    {
        public List<Building> Buildings { get; set; } = new List<Building>();
        public List<CapitalShip> CapitalShips { get; set; } = new List<CapitalShip>();
        public List<Fleet> Fleets { get; set; } = new List<Fleet>();
        public List<Officer> Officers { get; set; } = new List<Officer>();
        public List<Regiment> Regiments { get; set; } = new List<Regiment>();
        public List<SpecialForces> SpecialForces { get; set; } = new List<SpecialForces>();
        public List<Starfighter> Starfighters { get; set; } = new List<Starfighter>();
        public List<Mission> Missions { get; set; } = new List<Mission>();

        public override bool CanAcceptChild(ISceneNode child) =>
            child is Building
            || child is CapitalShip
            || child is Fleet
            || child is Officer
            || child is Regiment
            || child is SpecialForces
            || child is Starfighter
            || child is Mission;

        public override void AddChild(ISceneNode child)
        {
            switch (child)
            {
                case Building value: Buildings.Add(value); break;
                case CapitalShip value: CapitalShips.Add(value); break;
                case Fleet value: Fleets.Add(value); break;
                case Officer value: Officers.Add(value); break;
                case Regiment value: Regiments.Add(value); break;
                case SpecialForces value: SpecialForces.Add(value); break;
                case Starfighter value: Starfighters.Add(value); break;
                case Mission value: Missions.Add(value); break;
                default: throw new InvalidOperationException($"{child?.GetType().Name} cannot enter a void pool.");
            }
        }

        public override void RemoveChild(ISceneNode child)
        {
            switch (child)
            {
                case Building value: Buildings.Remove(value); break;
                case CapitalShip value: CapitalShips.Remove(value); break;
                case Fleet value: Fleets.Remove(value); break;
                case Officer value: Officers.Remove(value); break;
                case Regiment value: Regiments.Remove(value); break;
                case SpecialForces value: SpecialForces.Remove(value); break;
                case Starfighter value: Starfighters.Remove(value); break;
                case Mission value: Missions.Remove(value); break;
            }
        }

        public override IEnumerable<ISceneNode> GetChildren()
        {
            foreach (ISceneNode node in Buildings) yield return node;
            foreach (ISceneNode node in CapitalShips) yield return node;
            foreach (ISceneNode node in Fleets) yield return node;
            foreach (ISceneNode node in Officers) yield return node;
            foreach (ISceneNode node in Regiments) yield return node;
            foreach (ISceneNode node in SpecialForces) yield return node;
            foreach (ISceneNode node in Starfighters) yield return node;
            foreach (ISceneNode node in Missions) yield return node;
        }
    }
}
