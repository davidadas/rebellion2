using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Attributes;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

public abstract class Mission : ContainerNode
{
    public string ConfigKey { get; set; }
    public string TargetInstanceID { get; set; }
    public string OriginInstanceID { get; set; }

    [PersistableIgnore]
    public List<IMissionParticipant> MainParticipants { get; set; }

    [PersistableIgnore]
    public List<IMissionParticipant> DecoyParticipants { get; set; }

    public MissionParticipantSkill ParticipantSkill { get; set; }
    public bool HasInitiated;

    [PersistableIgnore]
    public ProbabilityTable SuccessProbabilityTable { get; set; }

    [PersistableIgnore]
    public ProbabilityTable DecoyProbabilityTable { get; set; }

    [PersistableIgnore]
    public ProbabilityTable FoilProbabilityTable { get; set; }

    [PersistableIgnore]
    public int BaseTicks;

    [PersistableIgnore]
    public int SpreadTicks;

    public int MaxProgress { get; set; }
    public int CurrentProgress { get; set; }

    public MissionParticipantSkill DecoyParticipantSkill = MissionParticipantSkill.Espionage;

    /// <summary>
    /// Whether this mission should be canceled when the target planet changes ownership.
    /// Defaults to true. Override to false for missions that survive ownership changes.
    /// </summary>
    public virtual bool CanceledOnOwnershipChange => true;

    /// <summary>
    /// Returns true if an external event has invalidated this mission and it should be
    /// canceled before its next tick. Checked by MissionSystem as a pre-tick guard.
    /// Base implementation cancels when any main participant is captured or killed.
    /// Override and call base for missions with additional cancellation conditions.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>True if the mission should be aborted.</returns>
    public virtual bool ShouldAbort(GameRoot game) =>
        MainParticipants.OfType<Officer>().Any(o => o.IsCaptured || o.IsKilled);

    /// <summary>
    /// Parameterless constructor for deserialization.
    /// </summary>
    protected Mission()
    {
        MainParticipants = new List<IMissionParticipant>();
        DecoyParticipants = new List<IMissionParticipant>();
    }

    /// <summary>
    /// Initializes a mission with all required parameters.
    /// </summary>
    protected Mission(
        string configKey,
        string ownerInstanceId,
        string targetInstanceId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        MissionParticipantSkill participantSkill,
        ProbabilityTable successProbabilityTable
    )
    {
        ConfigKey = configKey ?? throw new ArgumentNullException(nameof(configKey));
        DisplayName = configKey;
        AllowedOwnerInstanceIDs = new List<string> { ownerInstanceId };
        OwnerInstanceID = ownerInstanceId;
        TargetInstanceID = targetInstanceId;
        MainParticipants = mainParticipants ?? new List<IMissionParticipant>();
        DecoyParticipants = decoyParticipants ?? new List<IMissionParticipant>();
        ParticipantSkill = participantSkill;
        SuccessProbabilityTable =
            successProbabilityTable ?? new ProbabilityTable(new Dictionary<int, int> { { 0, 50 } });
        DecoyProbabilityTable = new ProbabilityTable(new Dictionary<int, int> { { 0, 0 } });
        FoilProbabilityTable = new ProbabilityTable(new Dictionary<int, int> { { 0, 0 } });
    }

    /// <summary>
    /// Applies probability tables and tick ranges from config, keyed by ConfigKey.
    /// </summary>
    /// <param name="tables">The mission probability and tick configuration to apply.</param>
    public virtual void Configure(GameConfig.MissionProbabilityTablesConfig tables)
    {
        DecoyProbabilityTable = new ProbabilityTable(tables.Decoy);
        FoilProbabilityTable = new ProbabilityTable(tables.Foil);

        Dictionary<int, int> successTable = tables.GetSuccessTable(ConfigKey);
        if (successTable != null)
            SuccessProbabilityTable = new ProbabilityTable(successTable);

        GameConfig.MissionTickConfig tickConfig = tables.TickRanges.GetTickConfig(ConfigKey);
        if (tickConfig != null)
        {
            BaseTicks = tickConfig.Base;
            SpreadTicks = tickConfig.Spread;
        }
    }

    /// <summary>
    /// Randomizes MaxProgress as BaseTicks + random(0..SpreadTicks) inclusive.
    /// Matches original: base_delay + roll_dice(spread).
    /// </summary>
    /// <param name="provider">RNG provider for rolling the duration spread.</param>
    public void Initiate(IRandomNumberProvider provider)
    {
        CurrentProgress = 0;
        MaxProgress = BaseTicks + provider.NextInt(0, SpreadTicks + 1);
        HasInitiated = true;
    }

    /// <summary>
    /// Returns the configured tick values as [BaseTicks, SpreadTicks].
    /// Actual duration is BaseTicks + random(0, SpreadTicks) inclusive.
    /// </summary>
    /// <returns>Array of [BaseTicks, SpreadTicks].</returns>
    public int[] GetTickRange() => new int[] { BaseTicks, SpreadTicks };

    /// <summary>
    /// Forces MaxProgress to a specific tick count, bypassing randomization. Used in tests.
    /// </summary>
    /// <param name="tick">The exact tick count to assign as MaxProgress.</param>
    public void SetExecutionTick(int tick) => MaxProgress = tick;

    /// <summary>
    /// Returns true when CurrentProgress has reached or exceeded MaxProgress.
    /// </summary>
    /// <returns>True if the mission has completed.</returns>
    public bool IsComplete() => CurrentProgress >= MaxProgress;

    /// <summary>
    /// Returns all main and decoy participants as a single list.
    /// </summary>
    /// <returns>Combined list of main and decoy participants.</returns>
    public List<IMissionParticipant> GetAllParticipants() =>
        MainParticipants.Concat(DecoyParticipants).ToList();

    /// <summary>
    /// Increments progress by 1 unless all participants are in transit.
    /// </summary>
    public void IncrementProgress()
    {
        List<IMissionParticipant> all = GetAllParticipants();
        bool unitsAreAllInTransit = all.Count > 0 && all.All(u => u.Movement != null);
        if (CurrentProgress < MaxProgress && !unitsAreAllInTransit)
            CurrentProgress++;
    }

    /// <summary>
    /// Looks up the base success probability for an agent using their skill score and the
    /// success table. Override to use a different skill or scoring formula.
    /// </summary>
    /// <param name="agent">The participant whose skill is evaluated.</param>
    /// <returns>Success probability 0–100.</returns>
    protected virtual double GetAgentProbability(IMissionParticipant agent)
    {
        int score = (int)agent.GetMissionSkillValue(ParticipantSkill);
        return SuccessProbabilityTable.Lookup(score);
    }

    /// <summary>
    /// Calculates the probability that a decoy participant beats enemy detection.
    /// Score is the decoy's espionage skill offset by 35% of the best enemy defender's espionage.
    /// </summary>
    /// <param name="decoy">The decoy participant to evaluate.</param>
    /// <returns>Decoy success probability 0–100.</returns>
    protected double GetDecoyProbability(IMissionParticipant decoy)
    {
        int bestDefenderEspionage = 0;
        if (GetParent() is Planet planet)
        {
            foreach (Officer officer in planet.Officers)
            {
                if (officer.OwnerInstanceID != OwnerInstanceID && !officer.IsCaptured)
                {
                    int esp = officer.GetMissionSkillValue(MissionParticipantSkill.Espionage);
                    if (esp > bestDefenderEspionage)
                        bestDefenderEspionage = esp;
                }
            }
        }

        int decoyEspionage = decoy.GetMissionSkillValue(DecoyParticipantSkill);
        int score = decoyEspionage - (int)(bestDefenderEspionage * 0.35);
        return DecoyProbabilityTable.Lookup(score);
    }

    /// <summary>
    /// Returns the probability (0–100) that the mission is foiled by enemy forces.
    /// Returns 0 for missions targeting the owner's own planets. Override to suppress foiling
    /// entirely (return 0) or apply a custom formula.
    /// </summary>
    /// <param name="defenseScore">Sum of enemy regiment defense ratings on the target planet.</param>
    /// <returns>Foil probability 0–100.</returns>
    protected virtual double GetFoilProbability(double defenseScore)
    {
        if (GetParent() is Planet planet && planet.OwnerInstanceID == OwnerInstanceID)
            return 0;

        return FoilProbabilityTable.Lookup((int)defenseScore);
    }

    /// <summary>
    /// Returns the sum of defense ratings of all enemy regiments on the target planet.
    /// </summary>
    /// <returns>Total defense rating, or 0 if no valid planet target.</returns>
    protected internal double GetDefenseScore()
    {
        Planet planet = GetParent() as Planet;
        if (planet == null)
            return 0;

        double score = 0;
        foreach (ISceneNode child in planet.GetChildren())
        {
            if (child is Regiment regiment && regiment.OwnerInstanceID != OwnerInstanceID)
                score += regiment.DefenseRating;
        }
        return score;
    }

    /// <summary>
    /// Combines agent success probability and foil probability into a net success chance.
    /// </summary>
    /// <param name="agentProbability">Raw agent success probability 0–100.</param>
    /// <param name="foilProbability">Foil probability 0–100 that reduces the net chance.</param>
    /// <returns>Net success probability 0–100.</returns>
    protected double CalculateTotalSuccess(double agentProbability, double foilProbability) =>
        (agentProbability / 100.0) * (1 - foilProbability / 100.0) * 100.0;

    /// <summary>
    /// Returns true if at least one main participant beats the combined success threshold.
    /// </summary>
    /// <param name="provider">RNG provider for rolling against the success probability.</param>
    /// <param name="foilProbability">Foil probability 0–100 applied to each participant's roll.</param>
    /// <returns>True if at least one participant succeeds.</returns>
    protected bool CheckMissionSuccess(IRandomNumberProvider provider, double foilProbability)
    {
        foreach (IMissionParticipant participant in MainParticipants)
        {
            double successProbability = CalculateTotalSuccess(
                GetAgentProbability(participant),
                foilProbability
            );
            if (provider.NextDouble() * 100 <= successProbability)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if at least one decoy participant beats the detection threshold.
    /// A successful decoy zeroes out the foil probability for this execution.
    /// </summary>
    /// <param name="provider">RNG provider for rolling against each decoy's probability.</param>
    /// <returns>True if at least one decoy succeeds.</returns>
    protected bool CheckDecoySuccessful(IRandomNumberProvider provider)
    {
        foreach (IMissionParticipant decoy in DecoyParticipants)
        {
            if (provider.NextDouble() * 100 <= GetDecoyProbability(decoy))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if the foil roll falls within the foil probability, causing a Foiled outcome.
    /// </summary>
    /// <param name="provider">RNG provider for the foil roll.</param>
    /// <param name="foilProbability">Foil probability 0–100.</param>
    /// <returns>True if the mission is foiled.</returns>
    protected bool CheckMissionFoiled(IRandomNumberProvider provider, double foilProbability) =>
        provider.NextDouble() * 100 <= foilProbability;

    /// <summary>
    /// Increments the mission skill of every participant that has CanImproveMissionSkill set.
    /// Called automatically by Execute on a successful outcome.
    /// </summary>
    protected virtual void ImproveMissionParticipantsSkill()
    {
        foreach (IMissionParticipant participant in MainParticipants.Concat(DecoyParticipants))
        {
            if (participant.CanImproveMissionSkill)
            {
                participant.SetMissionSkillValue(
                    ParticipantSkill,
                    participant.GetMissionSkillValue(ParticipantSkill) + 1
                );
            }
        }
    }

    /// <summary>
    /// Executes the mission, determines the outcome, and returns all results.
    /// MissionCompletedResult is always the last item in the list.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="provider">RNG provider for all probability rolls.</param>
    /// <returns>All results produced by the outcome, with a MissionCompletedResult appended last.</returns>
    public List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
    {
        List<GameResult> results = new List<GameResult>();
        MissionOutcome outcome;

        double defenseScore = GetDefenseScore();
        double foilProbability = GetFoilProbability(defenseScore);

        if (CheckDecoySuccessful(provider))
            foilProbability = 0;

        if (CheckMissionSuccess(provider, foilProbability))
        {
            if (!IsMissionSatisfied(game))
            {
                outcome = MissionOutcome.Failed;
                results.AddRange(OnFailed(game, provider));
            }
            else
            {
                outcome = MissionOutcome.Success;
                results.AddRange(OnSuccess(game, provider));
                ImproveMissionParticipantsSkill();
            }
        }
        else if (CheckMissionFoiled(provider, foilProbability))
        {
            outcome = MissionOutcome.Foiled;
            results.AddRange(OnFoiled(game, provider));
        }
        else
        {
            outcome = MissionOutcome.Failed;
            results.AddRange(OnFailed(game, provider));
        }

        List<IMissionParticipant> allParticipants = GetAllParticipants();
        string targetName = (GetParent() as Planet)?.GetDisplayName() ?? string.Empty;

        results.Add(
            new MissionCompletedResult
            {
                Mission = this,
                MissionName = DisplayName,
                TargetName = targetName,
                Participants = allParticipants,
                Outcome = outcome,
                Tick = game.CurrentTick,
            }
        );

        return results;
    }

    /// <summary>
    /// Validates that <paramref name="target"/> is non-null and is a Planet, then returns it.
    /// Call at the top of each mission constructor before mission-specific validation.
    /// </summary>
    /// <param name="target">The scene node to validate as a Planet.</param>
    /// <param name="missionName">Human-readable mission name used in the error message.</param>
    /// <exception cref="ArgumentNullException">target is null.</exception>
    /// <exception cref="InvalidOperationException">target is not a Planet.</exception>
    /// <returns>The validated Planet instance.</returns>
    protected static Planet RequirePlanetTarget(ISceneNode target, string missionName)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (!(target is Planet planet))
            throw new InvalidOperationException(
                $"{missionName} target must be a planet. Got: {target.GetType().Name}"
            );
        return planet;
    }

    /// <summary>
    /// Override to validate target state at execution time. Returning false routes a
    /// successful dice roll to <see cref="MissionOutcome.Failed"/> without calling OnSuccess.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>True if the mission target conditions are still valid; false to force a Failed outcome.</returns>
    protected virtual bool IsMissionSatisfied(GameRoot game) => true;

    /// <summary>
    /// Override to apply effects and return results when the mission succeeds.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="provider">RNG provider for any randomized effects.</param>
    /// <returns>Results produced by the success outcome.</returns>
    protected abstract List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider);

    /// <summary>
    /// Override to apply effects when the mission is foiled by enemy forces.
    /// Default returns no results.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="provider">RNG provider for any randomized effects.</param>
    /// <returns>Results produced by the foiled outcome; empty by default.</returns>
    protected virtual List<GameResult> OnFoiled(GameRoot game, IRandomNumberProvider provider) =>
        new List<GameResult>();

    /// <summary>
    /// Override to apply effects when the mission fails. Default returns no results.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="provider">RNG provider for any randomized effects.</param>
    /// <returns>Results produced by the failed outcome; empty by default.</returns>
    protected virtual List<GameResult> OnFailed(GameRoot game, IRandomNumberProvider provider) =>
        new List<GameResult>();

    /// <summary>
    /// Returns all mission participants as children of the mission.
    /// </summary>
    /// <returns>All main and decoy participants as scene nodes.</returns>
    public override IEnumerable<ISceneNode> GetChildren()
    {
        if (HasInitiated)
            return MainParticipants.Cast<ISceneNode>().Concat(DecoyParticipants.Cast<ISceneNode>());

        return new List<ISceneNode>();
    }

    /// <summary>
    /// Only mission participants may be moved into a mission node.
    /// </summary>
    /// <param name="child">The node to test.</param>
    /// <returns>True if child is an IMissionParticipant.</returns>
    public override bool CanAcceptChild(ISceneNode child) => child is IMissionParticipant;

    /// <summary>
    /// No-op — participants are pre-populated in MainParticipants/DecoyParticipants at
    /// construction. Only SetParent is needed for scene-graph bookkeeping.
    /// </summary>
    /// <param name="child">The node to add (ignored).</param>
    public override void AddChild(ISceneNode child) { }

    /// <summary>
    /// Removes the child from participant lists (called by GameRoot.MoveNode/DetachNode).
    /// </summary>
    /// <param name="child">The node to remove from participant lists.</param>
    public override void RemoveChild(ISceneNode child)
    {
        if (child is IMissionParticipant participant)
        {
            MainParticipants.Remove(participant);
            DecoyParticipants.Remove(participant);
        }
    }

    /// <summary>
    /// Return true to repeat the mission at the same target after completion;
    /// return false to tear down and send participants home.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>True if the mission should continue; false to tear down and send participants home.</returns>
    public abstract bool CanContinue(GameRoot game);
}
