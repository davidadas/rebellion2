using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Resolves loyalty-driven defections during mission resolution.
    /// </summary>
    public sealed class MissionDefectionSystem
    {
        private readonly GameRoot _game;

        public MissionDefectionSystem(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        public bool TryResolveDefection(
            Mission mission,
            IRandomNumberProvider provider,
            out List<GameResult> results
        )
        {
            if (mission == null)
                throw new ArgumentNullException(nameof(mission));
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            results = new List<GameResult>();
            Officer defector = mission
                .GetAllParticipants()
                .OfType<Officer>()
                .FirstOrDefault(officer => Defects(officer, provider));
            if (defector == null)
                return false;

            Officer discoverer = mission
                .GetAllParticipants()
                .OfType<Officer>()
                .Where(officer => officer != defector && CanDiscover(officer))
                .FirstOrDefault(officer => provider.NextInt(0, 100) < officer.ForceRank);
            if (discoverer != null)
            {
                defector.IsTraitor = true;
                results.Add(
                    new TraitorDiscoveredResult
                    {
                        Officer = defector,
                        DiscoveredBy = discoverer,
                        Context = mission.GetParent() as Planet,
                        Tick = _game.CurrentTick,
                    }
                );
            }

            return true;
        }

        private static bool Defects(Officer officer, IRandomNumberProvider provider)
        {
            if (
                officer
                is not {
                    CanBetray: true,
                    CurrentRank: OfficerRank.None,
                    IsCaptured: false,
                    IsKilled: false,
                }
            )
                return false;

            int probability = 100 - Math.Max(0, Math.Min(100, officer.Loyalty));
            return provider.NextInt(0, 100) < probability;
        }

        private static bool CanDiscover(Officer officer) =>
            officer is { IsCaptured: false, IsKilled: false } && officer.ForceRank > 0;
    }
}
