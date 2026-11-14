using System;
using Rebellion.Game.Units;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Tracks a planet-destroying battle station through a space battle, however the battle is
    /// resolved. The station stands off beyond the fighting, where no weapon can reach it: it
    /// takes part only through its superlaser, which destroys one capital ship each time it
    /// finishes charging, and through the trench that fighters fly to destroy it.
    /// </summary>
    /// <typeparam name="TTarget">The kind of unit the superlaser is fired at.</typeparam>
    public class BattleStationState<TTarget>
        where TTarget : class
    {
        private double _strikeRounds;
        private double _jumpRounds;

        public CapitalShip Ship { get; }

        /// <summary>
        /// Gets whether a shield generator on the planet protects the station, so fighters cannot
        /// attack it.
        /// </summary>
        public bool IsShielded { get; }

        public bool IsAlive { get; private set; } = true;

        public bool HasWithdrawn { get; private set; }

        public bool IsWithdrawing { get; private set; }

        /// <summary>
        /// Gets how charged the superlaser is, from 0 to 1. It opens the battle fully charged.
        /// </summary>
        public double SuperlaserCharge { get; private set; } = 1;

        /// <summary>
        /// Gets the capital ship the superlaser is burning right now, or null between shots.
        /// </summary>
        public TTarget StrikeTarget { get; private set; }

        /// <summary>
        /// Gets whether the station is still taking part: neither destroyed nor gone to hyperspace.
        /// </summary>
        public bool IsInBattle => IsAlive && !HasWithdrawn;

        /// <summary>
        /// Gets whether the superlaser can be fired now.
        /// </summary>
        public bool IsSuperlaserReady =>
            IsInBattle && !IsWithdrawing && StrikeTarget == null && SuperlaserCharge >= 1;

        public string DisplayName => Ship.GetDisplayName();

        /// <summary>
        /// Gets whether fighters can attack the station: it is still in the battle and no
        /// planetary shield protects it.
        /// </summary>
        internal bool IsAttackable => IsInBattle && !IsShielded;

        internal bool CanWithdrawIndependently => Ship.Hyperdrive > 0;

        internal bool IsJumpReady => _jumpRounds <= 0;

        internal double Durability =>
            Math.Max(Ship.CurrentHullStrength, 0) + Math.Max(Ship.MaxShieldStrength, 0);

        /// <summary>
        /// Creates battle state for one station.
        /// </summary>
        /// <param name="ship">The station entering combat.</param>
        /// <param name="isShielded">Whether a planetary shield generator protects the station.</param>
        internal BattleStationState(CapitalShip ship, bool isShielded)
        {
            Ship = ship ?? throw new ArgumentNullException(nameof(ship));
            IsShielded = isShielded;
        }

        /// <summary>
        /// Fires the superlaser at a capital ship: the charge is spent now and the beam destroys
        /// the ship once it has burned for the given time.
        /// </summary>
        /// <param name="target">The capital ship to destroy.</param>
        /// <param name="rounds">The rounds the beam burns before the ship is destroyed.</param>
        internal void BeginStrike(TTarget target, double rounds)
        {
            StrikeTarget = target ?? throw new ArgumentNullException(nameof(target));
            _strikeRounds = rounds;
            SuperlaserCharge = 0;
        }

        /// <summary>
        /// Advances the beam, the recharge, and the jump countdown.
        /// </summary>
        /// <param name="rounds">The elapsed combat rounds.</param>
        /// <param name="rechargeRounds">The rounds a full recharge takes.</param>
        /// <returns>The ship the beam finished burning this step, or null.</returns>
        internal TTarget Advance(double rounds, double rechargeRounds)
        {
            if (!IsInBattle)
                return null;

            if (IsWithdrawing)
                _jumpRounds = Math.Max(_jumpRounds - rounds, 0);

            if (StrikeTarget != null)
            {
                _strikeRounds -= rounds;
                if (_strikeRounds > 0)
                    return null;

                TTarget struck = StrikeTarget;
                StrikeTarget = null;
                return struck;
            }

            if (SuperlaserCharge < 1)
            {
                SuperlaserCharge =
                    rechargeRounds > 0
                        ? Math.Min(SuperlaserCharge + rounds / rechargeRounds, 1)
                        : 1;
            }

            return null;
        }

        /// <summary>
        /// Starts the station's jump to hyperspace, which completes after a countdown.
        /// </summary>
        /// <param name="jumpRounds">The rounds the station needs before it can jump.</param>
        internal void StartWithdrawal(double jumpRounds)
        {
            if (!IsInBattle || IsWithdrawing)
                return;

            IsWithdrawing = true;
            _jumpRounds = Math.Max(jumpRounds, 0);
        }

        /// <summary>
        /// Marks the surviving station as withdrawn from the battle.
        /// </summary>
        internal void FinishWithdrawal()
        {
            if (!IsAlive)
                return;

            HasWithdrawn = true;
            IsWithdrawing = false;
            StrikeTarget = null;
        }

        /// <summary>
        /// Destroys the station.
        /// </summary>
        internal void Destroy()
        {
            IsAlive = false;
            IsWithdrawing = false;
            StrikeTarget = null;
        }
    }
}
