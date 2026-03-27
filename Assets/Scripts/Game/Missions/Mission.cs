using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Attributes;
using Rebellion.Util.Common;

/// <summary>
/// Represents a mission in the game.
/// </summary>
public abstract class Mission : ContainerNode
{
    // Mission Properties
    public string Name { get; set; }
    public string TargetInstanceID { get; set; }

    [PersistableIgnore]
    public List<IMissionParticipant> MainParticipants { get; set; }

    [PersistableIgnore]
    public List<IMissionParticipant> DecoyParticipants { get; set; }
    public MissionParticipantSkill ParticipantSkill { get; set; }
    public bool HasInitiated = false;

    // Success Probability Variables
    [PersistableIgnore]
    public ProbabilityTable SuccessProbabilityTable { get; set; }

    [PersistableIgnore]
    public double QuadraticCoefficient { get; set; }

    [PersistableIgnore]
    public double LinearCoefficient { get; set; }

    [PersistableIgnore]
    public double ConstantTerm { get; set; }

    [PersistableIgnore]
    public double MinSuccessProbability = 1;

    [PersistableIgnore]
    public double MaxSuccessProbability = 100;

    [PersistableIgnore]
    public int MinTicks = 1;

    [PersistableIgnore]
    public int MaxTicks = 10;

    public int MaxProgress { get; set; }
    public int CurrentProgress { get; set; }

    // Decoy Probability Variables
    // @TODO: Move these to a config file.
    public double DecoyQuadraticCoefficient = 0.0012;
    public double DecoyLinearCoefficient = 0.785;
    public double DecoyConstantTerm = 60;
    public MissionParticipantSkill DecoyParticipantSkill = MissionParticipantSkill.Espionage;

    // Foil Probability Variables
    // @TODO: Move these to a config file.
    public double FoilQuadraticCoefficient = -0.001999;
    public double FoilLinearCoefficient = 0.8879;
    public double FoilConstantTerm = 84.61;

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    protected Mission() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="name"></param>
    /// <param name="ownerInstanceId"></param>
    /// <param name="targetInstanceId"></param>
    /// <param name="mainParticipants"></param>
    /// <param name="decoyParticipants"></param>
    /// <param name="participantSkill"></param>
    /// <param name="successProbabilityTable"></param>
    /// <param name="quadraticCoefficient"></param>
    /// <param name="linearCoefficient"></param>
    /// <param name="constantTerm"></param>
    /// <param name="minSuccessProbability"></param>
    /// <param name="maxSuccessProbability"></param>
    /// <param name="minTicks"></param>
    /// <param name="maxTicks"></param>
    /// <exception cref="ArgumentNullException"></exception>
    protected Mission(
        string name,
        string ownerInstanceId,
        string targetInstanceId,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        MissionParticipantSkill participantSkill,
        ProbabilityTable successProbabilityTable,
        double quadraticCoefficient,
        double linearCoefficient,
        double constantTerm,
        double minSuccessProbability,
        double maxSuccessProbability,
        int minTicks,
        int maxTicks
    )
    {
        // Set mission fields.
        Name = name ?? throw new ArgumentNullException(nameof(name));
        OwnerInstanceID = ownerInstanceId;
        TargetInstanceID = targetInstanceId;
        MainParticipants = mainParticipants ?? new List<IMissionParticipant>();
        DecoyParticipants = decoyParticipants ?? new List<IMissionParticipant>();
        ParticipantSkill = participantSkill;

        // Set fields for success probability calculation.
        SuccessProbabilityTable = successProbabilityTable;
        QuadraticCoefficient = quadraticCoefficient;
        LinearCoefficient = linearCoefficient;
        ConstantTerm = constantTerm;
        MinSuccessProbability = minSuccessProbability;
        MaxSuccessProbability = maxSuccessProbability;

        // Set fields for mission duration.
        MinTicks = minTicks;
        MaxTicks = maxTicks;
    }

    public void Initiate(
        GameRoot game,
        MovementSystem movementManager,
        IRandomNumberProvider provider
    )
    {
        // Order each unit to move to the mission location.
        foreach (IMissionParticipant participant in GetAllParticipants())
        {
            if (participant.GetParent() != this)
            {
                movementManager.RequestMove(participant, this);
            }
        }

        // Set the mission progress to 0 and the max progress.
        // Then, set the max progress to a random value between the min and max ticks.
        CurrentProgress = 0;
        MaxProgress = provider.NextInt(MinTicks, MaxTicks);

        HasInitiated = true;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public int[] GetTickRange()
    {
        return new int[] { MinTicks, MaxTicks };
    }

    /// <summary>
    /// Increments the mission progress by 1.
    /// </summary>
    /// <remarks>This is called each tick to increment the mission progress.</remarks>
    public void IncrementProgress()
    {
        // Movement != null means unit is in transit
        bool unitsAreAllInTransits = GetAllParticipants().All(unit => unit.Movement != null);

        if (CurrentProgress < MaxProgress && !unitsAreAllInTransits)
        {
            CurrentProgress++;
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="length"></param>
    public void SetExecutionTick(int tick)
    {
        MaxProgress = tick;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public bool IsComplete()
    {
        return CurrentProgress >= MaxProgress;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public List<IMissionParticipant> GetAllParticipants()
    {
        return MainParticipants.Concat(DecoyParticipants).Cast<IMissionParticipant>().ToList();
    }

    /// <summary>
    /// Calculates the success probability based on the agent's skill score.
    /// </summary>
    /// <param name="score"></param>
    /// <param name="quadraticCoefficient"></param>
    /// <param name="linearCoefficient"></param>
    /// <param name="constantTerm"></param>
    /// <returns></returns>
    private double CalculateProbability(
        double score,
        double quadraticCoefficient,
        double linearCoefficient,
        double constantTerm
    )
    {
        return (quadraticCoefficient * Math.Pow(score, 2))
            + (linearCoefficient * score)
            + constantTerm;
    }

    /// <summary>
    /// Calculates the success probability based on the agent's skill score.
    /// Uses MSTB table lookup first (when SuccessProbabilityTable is set),
    /// falls back to quadratic formula otherwise.
    /// </summary>
    /// <param name="agent">The agent participating in the mission.</param>
    /// <returns>The calculated success probability.</returns>
    protected double GetAgentProbability(IMissionParticipant agent)
    {
        // Get the agent's skill score
        int agentScore = (int)agent.GetMissionSkillValue(ParticipantSkill);

        double agentProbability;

        // Priority 1: MSTB table lookup (piecewise-linear interpolation)
        if (SuccessProbabilityTable != null)
        {
            agentProbability = SuccessProbabilityTable.Lookup(agentScore);
        }
        // Priority 2: Quadratic fallback (rebellion2 Mission.cs coefficients)
        else
        {
            agentProbability = CalculateProbability(
                agentScore,
                QuadraticCoefficient,
                LinearCoefficient,
                ConstantTerm
            );
        }

        return Math.Max(MinSuccessProbability, Math.Min(agentProbability, MaxSuccessProbability));
    }

    /// <summary>
    /// Calculates the decoy probability based on the decoy's skill score.
    /// </summary>
    /// <param name="decoy">The decoy participating in the mission.</param>
    /// <returns>The calculated decoy probability.</returns>
    protected double GetDecoyProbability(IMissionParticipant decoy)
    {
        // Get the agent's skill score and calculate the success probability.
        double decoyScore = decoy.GetMissionSkillValue(DecoyParticipantSkill);

        return CalculateProbability(
            decoyScore,
            DecoyQuadraticCoefficient,
            DecoyLinearCoefficient,
            DecoyConstantTerm
        );
    }

    /// <summary>
    /// Calculates the foil probability based on the defense score.
    /// </summary>
    /// <param name="defenseScore"></param>
    /// <returns>The calculated foil probability.</returns>
    protected double GetFoilProbability(double defenseScore)
    {
        // Check if the planet is owned by the mission owner.
        if (GetParent() is Planet planet)
        {
            // If the planet is not owned by the mission owner, the foil probability is 0.
            if (planet.OwnerInstanceID == OwnerInstanceID)
            {
                return 0;
            }
        }

        return CalculateProbability(
            defenseScore,
            FoilQuadraticCoefficient,
            FoilLinearCoefficient,
            FoilConstantTerm
        );
    }

    /// <summary>
    /// Returns the defense score of the planet.
    /// This is the sum of the defense ratings of all regiments on the planet.
    /// </summary>
    /// <returns>The defense score of the planet.</returns>
    protected internal double GetDefenseScore()
    {
        Planet planet = GetParent() as Planet;
        double defenseScore = 0;

        // Sum the defense ratings of all regiments on the planet.
        foreach (ISceneNode child in planet.GetChildren())
        {
            if (child is Regiment regiment && regiment.OwnerInstanceID != OwnerInstanceID)
            {
                defenseScore += regiment.DefenseRating;
            }
        }

        return defenseScore;
    }

    /// <summary>
    /// Calculates the total success probability of the mission.
    /// </summary>
    /// <param name="agentProbability">The probability of the agent's success.</param>
    /// <param name="foilProbability">The probability of the mission being foiled.</param>
    /// <returns>The total success probability of the mission.</returns>
    protected double CalculateTotalSuccess(double agentProbability, double foilProbability)
    {
        agentProbability = agentProbability / 100.0;
        foilProbability = foilProbability / 100.0;

        // Calculate total success probability using the formula.
        double totalSuccess = agentProbability * (1 - foilProbability);

        // Convert back to percentage.
        return totalSuccess * 100.0;
    }

    /// <summary>
    /// Checks if the mission is successful.
    /// </summary>
    /// <param name="provider">Random number provider for probability checks.</param>
    /// <param name="foilProbability">The probability of the mission being foiled.</param>
    /// <returns>True if the mission is successful, false otherwise.</returns>
    protected bool CheckMissionSuccess(IRandomNumberProvider provider, double foilProbability)
    {
        foreach (IMissionParticipant participant in MainParticipants)
        {
            // Get the agent's skill score and calculate the success probability.
            double agentProbability = GetAgentProbability(participant);

            // Calculate the success probability.
            double successProbability = CalculateTotalSuccess(agentProbability, foilProbability);

            // Determine if the mission is successful.
            bool isSuccessful = provider.NextDouble() * 100 <= successProbability;

            // Only return true if the mission is successful.
            if (isSuccessful)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the decoy is successful.
    /// </summary>
    /// <param name="provider">Random number provider for probability checks.</param>
    /// <param name="foilProbability">The probability of the mission being foiled.</param>
    /// <returns>True if the decoy is successful, false otherwise.</returns>
    protected bool CheckDecoySuccessful(IRandomNumberProvider provider, double foilProbability)
    {
        foreach (IMissionParticipant decoy in DecoyParticipants)
        {
            double decoyProbability = GetDecoyProbability(decoy);

            // Determine if the decoy is successful.
            bool isSuccessful = provider.NextDouble() * 100 <= decoyProbability;

            // Only return true if the decoy is successful.
            if (isSuccessful)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the mission is foiled.
    /// </summary>
    /// <param name="provider">Random number provider for probability checks.</param>
    /// <param name="foilProbability">The probability of the mission being foiled.</param>
    /// <returns>True if the mission is foiled, false otherwise.</returns>
    protected bool CheckMissionFoiled(IRandomNumberProvider provider, double foilProbability)
    {
        return provider.NextDouble() * 100 <= foilProbability;
    }

    /// <summary>
    /// Improves the mission skill of all participants.
    /// </summary>
    protected void ImproveMissionParticipantsSkill()
    {
        foreach (IMissionParticipant participant in MainParticipants.Concat(DecoyParticipants))
        {
            // Check if the participant can improve their mission skill.
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
    /// Executes the mission, determining if it succeeds or fails.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="provider">Random number provider for probability checks.</param>
    public void Execute(GameRoot game, IRandomNumberProvider provider)
    {
        // Get the defense score and calculate the foil probability.
        double defenseScore = GetDefenseScore();
        double foilProbability = GetFoilProbability(defenseScore);

        if (
            CheckMissionSuccess(provider, foilProbability)
            || CheckDecoySuccessful(provider, foilProbability)
        )
        {
            OnSuccess(game, provider);
            ImproveMissionParticipantsSkill();
        }
        else if (CheckMissionFoiled(provider, foilProbability))
        {
            GameLogger.Log(Name + " has been foiled.");
            // @TODO: Handle mission being foiled.
        }
        else
        {
            GameLogger.Log(Name + " has failed.");
            // @TODO: Handle mission failure.
        }
    }

    /// <summary>
    /// Returns all mission participants as children of the mission.
    /// </summary>
    public override IEnumerable<ISceneNode> GetChildren()
    {
        if (HasInitiated)
        {
            return MainParticipants.Cast<ISceneNode>().Concat(DecoyParticipants.Cast<ISceneNode>());
        }

        // Return an empty list.
        return new List<ISceneNode>();
    }

    /// <summary>
    /// No-op (missions cannot have children added).
    /// </summary>
    public override void AddChild(ISceneNode child)
    {
        // No-op: Missions cannot have children added after initialization.
    }

    /// <summary>
    /// No-op (missions cannot have children removed).
    /// </summary>
    public override void RemoveChild(ISceneNode child)
    {
        // No-op: Missions cannot have children removed after initialization.
    }

    /// <summary>
    /// Method to handle mission success. To be implemented by derived classes.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="provider">Random number provider for success effects.</param>
    protected abstract void OnSuccess(GameRoot game, IRandomNumberProvider provider);

    /// <summary>
    ///
    /// </summary>
    /// <param name="game"></param>
    public abstract bool CanContinue(GameRoot game);
}
