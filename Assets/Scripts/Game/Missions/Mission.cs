using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Attributes;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

public abstract class Mission : ContainerNode
{
    public string Name { get; set; }
    public string TargetInstanceID { get; set; }

    [PersistableIgnore]
    public List<IMissionParticipant> MainParticipants { get; set; }

    [PersistableIgnore]
    public List<IMissionParticipant> DecoyParticipants { get; set; }

    public MissionParticipantSkill ParticipantSkill { get; set; }
    public bool HasInitiated = false;

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
    public virtual bool IsCanceled(GameRoot game) =>
        MainParticipants.OfType<Officer>().Any(o => o.IsCaptured || o.IsKilled);

    /// <summary>
    /// Parameterless constructor for deserialization.
    /// </summary>
    protected Mission() { }

    /// <summary>
    /// Initializes a mission with all required parameters.
    /// </summary>
    protected Mission(
        string name,
        string ownerInstanceId,
        string targetInstanceId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        MissionParticipantSkill participantSkill,
        ProbabilityTable successProbabilityTable,
        int baseTicks,
        int spreadTicks
    )
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DisplayName = Name;
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
        BaseTicks = baseTicks;
        SpreadTicks = spreadTicks;
    }

    /// <summary>
    /// Applies shared probability tables from config. Override to set mission-specific
    /// tables and tick ranges; always call base first.
    /// </summary>
    public virtual void Configure(GameConfig.MissionProbabilityTablesConfig tables)
    {
        DecoyProbabilityTable = new ProbabilityTable(tables.Decoy);
        FoilProbabilityTable = new ProbabilityTable(tables.Foil);
    }

    /// <summary>
    /// Randomizes MaxProgress as BaseTicks + random(0..SpreadTicks) inclusive.
    /// Matches original: base_delay + roll_dice(spread).
    /// </summary>
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
    public int[] GetTickRange() => new int[] { BaseTicks, SpreadTicks };

    /// <summary>
    /// Forces MaxProgress to a specific tick count, bypassing randomization. Used in tests.
    /// </summary>
    public void SetExecutionTick(int tick) => MaxProgress = tick;

    /// <summary>
    /// Returns true when CurrentProgress has reached or exceeded MaxProgress.
    /// </summary>
    public bool IsComplete() => CurrentProgress >= MaxProgress;

    /// <summary>
    /// Returns all main and decoy participants as a single list.
    /// </summary>
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
    protected virtual double GetAgentProbability(IMissionParticipant agent)
    {
        int score = (int)agent.GetMissionSkillValue(ParticipantSkill);
        return SuccessProbabilityTable.Lookup(score);
    }

    /// <summary>
    /// Calculates the probability that a decoy participant beats enemy detection.
    /// Score is the decoy's espionage skill offset by 35% of the best enemy defender's espionage.
    /// </summary>
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
    protected virtual double GetFoilProbability(double defenseScore)
    {
        if (GetParent() is Planet planet && planet.OwnerInstanceID == OwnerInstanceID)
            return 0;

        return FoilProbabilityTable.Lookup((int)defenseScore);
    }

    /// <summary>
    /// Returns the sum of defense ratings of all enemy regiments on the target planet.
    /// </summary>
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
    protected double CalculateTotalSuccess(double agentProbability, double foilProbability) =>
        (agentProbability / 100.0) * (1 - foilProbability / 100.0) * 100.0;

    /// <summary>
    /// Returns true if at least one main participant beats the combined success threshold.
    /// </summary>
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
            if (!IsTargetValid(game))
            {
                outcome = MissionOutcome.Failed;
                results.AddRange(OnFailed(game));
            }
            else
            {
                outcome = MissionOutcome.Success;
                results.AddRange(OnSuccess(game));
                ImproveMissionParticipantsSkill();
            }
        }
        else if (CheckMissionFoiled(provider, foilProbability))
        {
            outcome = MissionOutcome.Foiled;
            results.AddRange(OnFoiled(game));
        }
        else
        {
            outcome = MissionOutcome.Failed;
            results.AddRange(OnFailed(game));
        }

        List<IMissionParticipant> allParticipants = GetAllParticipants();
        string agents = string.Join(
            ", ",
            allParticipants.Select(p => ((ISceneNode)p).GetDisplayName())
        );
        string targetName = (GetParent() as Planet)?.GetDisplayName() ?? string.Empty;
        string targetStr = string.IsNullOrEmpty(targetName) ? "" : $" at {targetName}";
        GameLogger.Log($"{Name} mission by {agents}{targetStr}: {outcome}");

        results.Add(
            new MissionCompletedResult
            {
                MissionInstanceID = InstanceID,
                MissionName = Name,
                TargetName = targetName,
                ParticipantInstanceIDs = allParticipants.Select(p => p.GetInstanceID()).ToList(),
                ParticipantNames = allParticipants
                    .Select(p => ((ISceneNode)p).GetDisplayName())
                    .ToList(),
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
    /// <param name="missionName">Human-readable mission name used in the error message.</param>
    /// <exception cref="ArgumentNullException">target is null.</exception>
    /// <exception cref="InvalidOperationException">target is not a Planet.</exception>
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
    protected virtual bool IsTargetValid(GameRoot game) => true;

    /// <summary>
    /// Override to apply effects and return results when the mission succeeds.
    /// </summary>
    protected abstract List<GameResult> OnSuccess(GameRoot game);

    /// <summary>
    /// Override to apply effects when the mission is foiled by enemy forces.
    /// Default returns no results.
    /// </summary>
    protected virtual List<GameResult> OnFoiled(GameRoot game) => new List<GameResult>();

    /// <summary>
    /// Override to apply effects when the mission fails. Default returns no results.
    /// </summary>
    protected virtual List<GameResult> OnFailed(GameRoot game) => new List<GameResult>();

    /// <summary>
    /// Returns all mission participants as children of the mission.
    /// </summary>
    public override IEnumerable<ISceneNode> GetChildren()
    {
        if (HasInitiated)
            return MainParticipants.Cast<ISceneNode>().Concat(DecoyParticipants.Cast<ISceneNode>());

        return new List<ISceneNode>();
    }

    /// <summary>
    /// Missions cannot have children added after initialization.
    /// </summary>
    /// <param name="child">The candidate child node.</param>
    /// <returns>Always false.</returns>
    public override bool CanAcceptChild(ISceneNode child) => false;

    /// <summary>
    /// No-op — missions cannot have children added after initialization.
    /// </summary>
    public override void AddChild(ISceneNode child) { }

    /// <summary>
    /// Removes the child from participant lists (called by GameRoot.MoveNode/DetachNode).
    /// </summary>
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
    public abstract bool CanContinue(GameRoot game);
}
