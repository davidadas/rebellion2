using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Scripting;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;
using Rebellion.Util.Mathematics;
using Rebellion.Util.Random;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Groups the image assets that define an officer's current visual identity.
    /// </summary>
    [PersistableObject]
    public sealed class OfficerImageSet
    {
        public string DisplayImagePath { get; set; }
        public string SmallDisplayImagePath { get; set; }
        public string MessageImagePath { get; set; }
        public string EncyclopediaImagePath { get; set; }

        /// <summary>
        /// Creates an independent copy of this image set.
        /// </summary>
        /// <returns>The created copy.</returns>
        public OfficerImageSet CreateCopy() =>
            new OfficerImageSet
            {
                DisplayImagePath = DisplayImagePath,
                SmallDisplayImagePath = SmallDisplayImagePath,
                MessageImagePath = MessageImagePath,
                EncyclopediaImagePath = EncyclopediaImagePath,
            };

        /// <summary>
        /// Applies authored image paths over the current image set.
        /// </summary>
        /// <param name="authored">The authored.</param>
        public void MergeFrom(OfficerImageSet authored)
        {
            if (authored == null)
                return;
            DisplayImagePath = authored.DisplayImagePath ?? DisplayImagePath;
            SmallDisplayImagePath = authored.SmallDisplayImagePath ?? SmallDisplayImagePath;
            MessageImagePath = authored.MessageImagePath ?? MessageImagePath;
            EncyclopediaImagePath = authored.EncyclopediaImagePath ?? EncyclopediaImagePath;
        }
    }

    /// <summary>
    /// Groups the recordings an officer can use for simulation and message events.
    /// </summary>
    [PersistableObject]
    public sealed class OfficerVoiceSet
    {
        private static readonly OfficerVoiceLineType[] _voiceLineTypes = (OfficerVoiceLineType[])
            Enum.GetValues(typeof(OfficerVoiceLineType));

        [PersistableMember(Name = "Order")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> OrderPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "PersonnelArrived")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> PersonnelArrivedPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "MissionSuccess")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionSuccessPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "MissionFailure")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionFailurePaths { get; set; } = new List<string>();

        [PersistableMember(Name = "MissionAbort")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionAbortPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "Released")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> ReleasedPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "Recovered")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> RecoveredPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "EnemyDetected")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> EnemyDetectedPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "ForceGrowth")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> ForceGrowthPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "ForceUserDiscovered")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> ForceUserDiscoveredPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "TraitorDiscovered")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> TraitorDiscoveredPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "RescueAttempt")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> RescueAttemptPaths { get; set; } = new List<string>();

        /// <summary>
        /// Returns the configured asset paths for one voice-line category.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The requested paths.</returns>
        public IReadOnlyList<string> GetPaths(OfficerVoiceLineType type)
        {
            IReadOnlyList<string> paths = GetMutablePaths(type);
            return paths ?? Array.Empty<string>();
        }

        /// <summary>
        /// Creates an independent copy of this voice set.
        /// </summary>
        /// <returns>The created copy.</returns>
        public OfficerVoiceSet CreateCopy()
        {
            OfficerVoiceSet copy = new OfficerVoiceSet();
            foreach (OfficerVoiceLineType type in _voiceLineTypes)
            {
                List<string> paths = GetMutablePaths(type);
                List<string> destination = copy.GetMutablePaths(type);
                if (paths != null && destination != null)
                    destination.AddRange(paths);
            }
            return copy;
        }

        /// <summary>
        /// Replaces each voice category for which the authored set supplies at least one path.
        /// </summary>
        /// <param name="authored">The authored.</param>
        public void MergeFrom(OfficerVoiceSet authored)
        {
            if (authored == null)
                return;

            foreach (OfficerVoiceLineType type in _voiceLineTypes)
                ReplaceWhenAuthored(authored.GetMutablePaths(type), GetMutablePaths(type));
        }

        /// <summary>
        /// Returns the mutable collection that canonically stores one voice-line category.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The requested mutable paths.</returns>
        private List<string> GetMutablePaths(OfficerVoiceLineType type) =>
            type switch
            {
                OfficerVoiceLineType.Order => OrderPaths,
                OfficerVoiceLineType.PersonnelArrived => PersonnelArrivedPaths,
                OfficerVoiceLineType.MissionSuccess => MissionSuccessPaths,
                OfficerVoiceLineType.MissionFailure => MissionFailurePaths,
                OfficerVoiceLineType.MissionAbort => MissionAbortPaths,
                OfficerVoiceLineType.Released => ReleasedPaths,
                OfficerVoiceLineType.Recovered => RecoveredPaths,
                OfficerVoiceLineType.EnemyDetected => EnemyDetectedPaths,
                OfficerVoiceLineType.ForceGrowth => ForceGrowthPaths,
                OfficerVoiceLineType.ForceUserDiscovered => ForceUserDiscoveredPaths,
                OfficerVoiceLineType.TraitorDiscovered => TraitorDiscoveredPaths,
                OfficerVoiceLineType.RescueAttempt => RescueAttemptPaths,
                _ => null,
            };

        /// <summary>
        /// Replaces a destination category only when authored paths are present.
        /// </summary>
        /// <param name="authored">The authored.</param>
        /// <param name="destination">The destination.</param>
        private static void ReplaceWhenAuthored(List<string> authored, List<string> destination)
        {
            if (authored == null || authored.Count == 0)
                return;

            destination.Clear();
            destination.AddRange(authored);
        }
    }

    /// <summary>
    /// Identifies the authored behavior controlling an officer's AI assignments.
    /// </summary>
    public enum OfficerAIBehaviorType
    {
        HoldPosition,
    }

    /// <summary>
    /// Configures an officer's AI behavior while every authored condition is satisfied.
    /// </summary>
    [PersistableObject]
    public sealed class OfficerAIBehavior
    {
        [PersistableAttribute]
        public OfficerAIBehaviorType Type { get; set; }

        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
    }

    /// <summary>
    /// Defines the command rank levels an officer can hold.
    /// </summary>
    public enum OfficerRank
    {
        None = 0,
        Commander = 1,
        Admiral = 2,
        General = 3,
    }

    /// <summary>
    /// Defines the display labels assigned to Force-rating thresholds.
    /// </summary>
    public enum ForceRankLabel
    {
        None,
        Novice,
        Trainee,
        ForceStudent,
        ForceKnight,
        ForceMaster,
    }

    public enum OfficerVoiceLineType
    {
        Order,
        PersonnelArrived,
        MissionSuccess,
        MissionFailure,
        MissionAbort,
        Released,
        Recovered,
        EnemyDetected,
        ForceGrowth,
        ForceUserDiscovered,
        TraitorDiscovered,
        RescueAttempt,
    }

    /// <summary>
    /// Represents an officer that can be used in missions.
    /// </summary>
    public class Officer : LeafNode, IMissionParticipant, IMovable, IEncyclopediaSource
    {
        private const int _ratingPercentScale = 100;

        [PersistableIgnore]
        private IReadOnlyDictionary<string, OfficerTraitDefinition> _traitDefinitions;

        [PersistableMember(Name = nameof(CanBetray))]
        private bool _canBetray;

        [PersistableMember(Name = nameof(Loyalty))]
        private int _loyalty;

        [PersistableMember(Name = nameof(ForceValue))]
        private int _forceValue;

        [PersistableMember(Name = nameof(ForceTrainingAdjustment))]
        private int _forceTrainingAdjustment;

        [PersistableMember(Name = nameof(InjuryPoints))]
        private int _injuryPoints;

        [PersistableMember(Name = "EnhancedDiplomacy")]
        private int? _enhancedDiplomacy;

        [PersistableMember(Name = "EnhancedEspionage")]
        private int? _enhancedEspionage;

        [PersistableMember(Name = "EnhancedCombat")]
        private int? _enhancedCombat;

        [PersistableMember(Name = "FastHealing")]
        private bool? _fastHealing;

        [PersistableIgnore]
        private GameConfig.JediConfig _forceRules;

        [PersistableIgnore]
        private GameRoot _forceGame;

        public string EncyclopediaImagePath { get; set; }
        public List<EncyclopediaEntryStat> EncyclopediaStats { get; set; }
        public string EncyclopediaDescription { get; set; }

        // Research Info.
        public int ShipResearch { get; set; }
        public int TroopResearch { get; set; }
        public int FacilityResearch { get; set; }

        // Officer Info.
        public bool IsMain { get; set; }

        /// <summary>
        /// Gets or sets the authored roster order used to choose a trainer and break equal training scores.
        /// </summary>
        public int RosterOrder { get; set; }

        public bool IsRecruitable { get; set; }
        public List<string> RecruitingFactionInstanceIDs { get; set; }

        public bool IsCaptured
        {
            get => _isCaptured;
            set
            {
                if (_isCaptured == value)
                    return;
                _isCaptured = value;
                RefreshForceDiscoveryState();
            }
        }

        private bool _isCaptured;
        public string CaptorInstanceID { get; set; }
        public bool CanEscape { get; set; }
        public int NextEscapeAttemptTick { get; set; }
        public bool IsKilled
        {
            get => _isKilled;
            set
            {
                if (_isKilled == value)
                    return;
                _isKilled = value;
                RefreshForceDiscoveryState();
            }
        }

        private bool _isKilled;

        /// <summary>
        /// Gets whether this officer's loyalty can change and permit betrayal.
        /// </summary>
        public bool CanBetray => _canBetray;

        /// <summary>
        /// Gets this officer's current loyalty.
        /// </summary>
        public int Loyalty => _loyalty;

        // Injury Info.
        [PersistableIgnore]
        public int InjuryPoints
        {
            get => _injuryPoints;
            set
            {
                if (_injuryPoints == value)
                    return;
                _injuryPoints = value;
                _enhancedCombat = CalculateForceRating(SkillRating.Combat);
            }
        }

        // Force Info.
        public int JediProbability { get; set; }
        public int JediLevel { get; set; }
        public int JediLevelVariance { get; set; }
        public bool IsJediTrainer
        {
            get => _isJediTrainer;
            set
            {
                if (_isJediTrainer == value)
                    return;
                _isJediTrainer = value;
                RefreshForceDiscoveryState();
            }
        }

        private bool _isJediTrainer;
        public bool GrowsForceOnMission { get; set; }

        /// <summary>
        /// Gets or sets whether capture-evasion growth requires Force eligibility.
        /// An unset value requires eligibility.
        /// </summary>
        public bool? ForceGrowthRequiresEligibility { get; set; }

        /// <summary>
        /// Gets or sets additional discovery requirements evaluated with the scanning officer
        /// available in the <c>$discoverer</c> binding.
        /// </summary>
        public List<GameConditional> ForceDiscoveryConditionals { get; set; } =
            new List<GameConditional>();

        [PersistableIgnore]
        public bool IsKnownJedi { get; set; }

        public bool IsForceSensitive { get; set; }
        public bool IsForceEligible { get; set; }

        /// <summary>
        /// Gets or sets the stored signed 16-bit Force experience value.
        /// </summary>
        [PersistableIgnore]
        public int ForceValue
        {
            get => unchecked((short)_forceValue);
            set
            {
                int stored = unchecked((short)value);
                if (ForceValue == stored)
                    return;
                InitializeForceState();
                _forceValue = stored;
                RefreshForceState();
            }
        }

        /// <summary>
        /// Gets or sets the stored signed 16-bit Force training adjustment.
        /// </summary>
        [PersistableIgnore]
        public int ForceTrainingAdjustment
        {
            get => unchecked((short)_forceTrainingAdjustment);
            set
            {
                int stored = unchecked((short)value);
                if (ForceTrainingAdjustment == stored)
                    return;
                InitializeForceState();
                _forceTrainingAdjustment = stored;
                RefreshForceState();
            }
        }

        [PersistableIgnore]
        public int ForceRank => unchecked((short)(ForceValue + ForceTrainingAdjustment));

        [PersistableIgnore]
        public bool IsDiscoveringForceUser => _isDiscoveringForceUser;

        [PersistableMember(Name = nameof(IsDiscoveringForceUser))]
        private bool _isDiscoveringForceUser;

        /// <summary>
        /// Gets or sets the next campaign tick when an enabled discovery scan may run.
        /// </summary>
        [PersistableIgnore]
        public int NextForceDiscoveryTick
        {
            get => _nextForceDiscoveryTick ?? 0;
            set => _nextForceDiscoveryTick = value;
        }

        [PersistableMember(Name = nameof(NextForceDiscoveryTick))]
        private int? _nextForceDiscoveryTick;

        /// <summary>
        /// Gets or sets the saved insertion order used when discovery scans share a due tick.
        /// </summary>
        public long ForceDiscoveryOrder { get; set; }

        // Rank Info.
        public OfficerRank[] AllowedRanks { get; set; }
        public OfficerRank CurrentRank { get; set; }

        // Owner Info.
        public string InitialParentTypeID { get; set; }
        public string InitialParentInstanceID { get; set; }

        // Variance Info.
        [PersistableIgnore]
        public int DiplomacyVariance { get; set; }

        [PersistableIgnore]
        public int EspionageVariance { get; set; }

        [PersistableIgnore]
        public int CombatVariance { get; set; }

        [PersistableIgnore]
        public int LeadershipVariance { get; set; }

        [PersistableIgnore]
        public int LoyaltyVariance { get; set; }

        [PersistableIgnore]
        public int FacilityResearchVariance { get; set; }

        [PersistableIgnore]
        public int TroopResearchVariance { get; set; }

        [PersistableIgnore]
        public int ShipResearchVariance { get; set; }

        // Movement Info.
        public MovementState Movement
        {
            get => _movement;
            set
            {
                if (ReferenceEquals(_movement, value))
                    return;
                _movement = value;
                OnAvailabilityChanged();
            }
        }

        private MovementState _movement;
        public bool IsRetired { get; set; }
        public string MissionReturnParentInstanceID { get; set; }
        public string MissionReturnLocationInstanceID { get; set; }
        public OfficerVoiceSet VoiceSet { get; set; }
        public OfficerImageSet ImageSet { get; set; }

        /// <summary>
        /// Gets or sets content-only AI configuration; saved officers use the active catalog instead.
        /// </summary>
        [PersistableIgnore]
        public OfficerAIBehavior AIBehavior { get; set; }

        [PersistableCollectionItem(Name = "TraitID")]
        public List<string> Traits { get; set; }

        [PersistableCollectionItem(Name = "Grant")]
        public List<OfficerTraitGrant> TraitGrants { get; set; }

        // Mission rating info.
        public Dictionary<SkillRating, int> Ratings { get; set; }
        public bool CanImproveMissionRating => true;

        /// <summary>
        /// Returns the officer's current command title and display name.
        /// </summary>
        /// <returns>The command-qualified name, or the authored name when unassigned.</returns>
        public override string GetDisplayName()
        {
            string name = base.GetDisplayName();
            if (CurrentRank == OfficerRank.None || string.IsNullOrWhiteSpace(name))
                return name;

            string title = CurrentRank switch
            {
                OfficerRank.Commander => "Commander",
                OfficerRank.Admiral => "Admiral",
                OfficerRank.General => "General",
                _ => string.Empty,
            };
            if (title.Length == 0)
                return name;

            string commandName = GetCommandName(name);
            return string.IsNullOrWhiteSpace(commandName) ? name : $"{title} {commandName}";
        }

        /// <summary>
        /// Converts an authored character name to the surname-style command name, such as Wedge
        /// Antilles becoming Antilles and Garm Bel Iblis becoming Bel Iblis.
        /// </summary>
        /// <param name="name">The authored officer name.</param>
        /// <returns>The name portion displayed after a command title.</returns>
        private static string GetCommandName(string name)
        {
            string commandName = name.Trim();
            string[] existingTitles = { "Commander ", "Admiral ", "General " };
            foreach (string existingTitle in existingTitles)
            {
                if (!commandName.StartsWith(existingTitle, StringComparison.OrdinalIgnoreCase))
                    continue;

                commandName = commandName.Substring(existingTitle.Length).TrimStart();
                break;
            }

            int firstSpace = commandName.IndexOf(' ');
            return firstSpace < 0 ? commandName : commandName.Substring(firstSpace + 1).TrimStart();
        }

        /// <summary>
        /// Applies authored image-set overrides to the officer's active image paths.
        /// </summary>
        public void ApplyImageSet()
        {
            DisplayImagePath = ImageSet.DisplayImagePath ?? DisplayImagePath;
            SmallDisplayImagePath = ImageSet.SmallDisplayImagePath ?? SmallDisplayImagePath;
            MessageImagePath = ImageSet.MessageImagePath ?? MessageImagePath;
            EncyclopediaImagePath = ImageSet.EncyclopediaImagePath ?? EncyclopediaImagePath;
        }

        /// <summary>
        /// Returns whether this officer can perform a mission type.
        /// </summary>
        /// <param name="missionTypeId">The mission type ID to inspect.</param>
        /// <returns>True if the officer can perform the mission type.</returns>
        public bool CanPerformMission(string missionTypeId) =>
            missionTypeId != ReconnaissanceMission.MissionTypeID;

        /// <summary>
        /// Creates an officer with the default state used for gameplay and deserialization.
        /// </summary>
        public Officer()
            : this(initializeDefaultState: true) { }

        /// <summary>
        /// Creates an officer with optional defaults before copied state is populated.
        /// </summary>
        /// <param name="initializeDefaultState">Whether to initialize fresh state instead of leaving it for the copy pipeline.</param>
        private Officer(bool initializeDefaultState)
        {
            if (!initializeDefaultState)
                return;

            _traitDefinitions = new Dictionary<string, OfficerTraitDefinition>(
                StringComparer.Ordinal
            );
            EncyclopediaStats = new List<EncyclopediaEntryStat>();
            RecruitingFactionInstanceIDs = new List<string>();
            VoiceSet = new OfficerVoiceSet();
            ImageSet = new OfficerImageSet();
            Traits = new List<string>();
            TraitGrants = new List<OfficerTraitGrant>();
            Ratings = new Dictionary<SkillRating, int>
            {
                { SkillRating.Diplomacy, 0 },
                { SkillRating.Espionage, 0 },
                { SkillRating.Combat, 0 },
                { SkillRating.Leadership, 0 },
            };
        }

        /// <summary>
        /// Applies a signed loyalty adjustment when this officer's loyalty can change.
        /// </summary>
        /// <param name="adjustment">The signed amount to apply.</param>
        /// <returns>True when the stored loyalty changed; otherwise false.</returns>
        public bool TryAdjustLoyalty(int adjustment)
        {
            if (!CanBetray || adjustment == 0)
                return false;

            int adjustedLoyalty = (int)Math.Clamp((long)_loyalty + adjustment, 0L, 100L);
            if (adjustedLoyalty == _loyalty)
                return false;

            _loyalty = adjustedLoyalty;
            return true;
        }

        /// <summary>
        /// Creates an officer for copying, retaining default state for derived factory callers.
        /// </summary>
        /// <returns>The created node copy.</returns>
        protected override BaseSceneNode CreateNodeCopy() =>
            GetType() == typeof(Officer)
                ? new Officer(initializeDefaultState: false)
                : new Officer();

        /// <summary>
        /// Copies officer state into an empty destination.
        /// </summary>
        /// <param name="destination">The destination.</param>
        protected override void CopyStateTo(BaseSceneNode destination)
        {
            base.CopyStateTo(destination);
            Officer copy = (Officer)destination;
            ((IEncyclopediaSource)this).CopyEncyclopediaStateTo(copy);
            copy.ShipResearch = ShipResearch;
            copy.TroopResearch = TroopResearch;
            copy.FacilityResearch = FacilityResearch;
            copy.IsMain = IsMain;
            copy.RosterOrder = RosterOrder;
            copy.IsRecruitable = IsRecruitable;
            copy.RecruitingFactionInstanceIDs = new List<string>(RecruitingFactionInstanceIDs);
            copy.IsCaptured = IsCaptured;
            copy.CaptorInstanceID = CaptorInstanceID;
            copy.CanEscape = CanEscape;
            copy.NextEscapeAttemptTick = NextEscapeAttemptTick;
            copy.IsKilled = IsKilled;
            copy._canBetray = _canBetray;
            copy._loyalty = _loyalty;
            copy._injuryPoints = _injuryPoints;
            copy.JediProbability = JediProbability;
            copy.JediLevel = JediLevel;
            copy.JediLevelVariance = JediLevelVariance;
            copy.IsJediTrainer = IsJediTrainer;
            copy.GrowsForceOnMission = GrowsForceOnMission;
            copy.ForceGrowthRequiresEligibility = ForceGrowthRequiresEligibility;
            copy.ForceDiscoveryConditionals = new List<GameConditional>(ForceDiscoveryConditionals);
            copy.IsKnownJedi = IsKnownJedi;
            copy.IsForceSensitive = IsForceSensitive;
            copy.IsForceEligible = IsForceEligible;
            copy._forceValue = _forceValue;
            copy._forceTrainingAdjustment = _forceTrainingAdjustment;
            copy._enhancedDiplomacy = _enhancedDiplomacy;
            copy._enhancedEspionage = _enhancedEspionage;
            copy._enhancedCombat = _enhancedCombat;
            copy._fastHealing = _fastHealing;
            copy._forceRules = _forceRules;
            copy._isDiscoveringForceUser = _isDiscoveringForceUser;
            copy._nextForceDiscoveryTick = _nextForceDiscoveryTick;
            copy.ForceDiscoveryOrder = ForceDiscoveryOrder;
            copy.AllowedRanks = AllowedRanks?.ToArray();
            copy.CurrentRank = CurrentRank;
            copy.InitialParentTypeID = InitialParentTypeID;
            copy.InitialParentInstanceID = InitialParentInstanceID;
            copy.DiplomacyVariance = DiplomacyVariance;
            copy.EspionageVariance = EspionageVariance;
            copy.CombatVariance = CombatVariance;
            copy.LeadershipVariance = LeadershipVariance;
            copy.LoyaltyVariance = LoyaltyVariance;
            copy.FacilityResearchVariance = FacilityResearchVariance;
            copy.TroopResearchVariance = TroopResearchVariance;
            copy.ShipResearchVariance = ShipResearchVariance;
            copy.Movement = Movement?.CreateCopy();
            copy.IsRetired = IsRetired;
            copy.MissionReturnParentInstanceID = MissionReturnParentInstanceID;
            copy.MissionReturnLocationInstanceID = MissionReturnLocationInstanceID;
            copy.VoiceSet = VoiceSet?.CreateCopy();
            copy.ImageSet = ImageSet?.CreateCopy();
            copy.Traits = new List<string>(Traits);
            copy.TraitGrants = TraitGrants.ConvertAll(grant => new OfficerTraitGrant
            {
                TraitID = grant.TraitID,
                SourceID = grant.SourceID,
            });
            copy._traitDefinitions = _traitDefinitions;
            copy.Ratings = new Dictionary<SkillRating, int>(Ratings);
        }

        /// <summary>
        /// Connects this officer to the validated trait definitions for the active content pack.
        /// </summary>
        /// <param name="traitDefinitions">The definitions indexed by trait identifier.</param>
        internal void BindTraitDefinitions(
            IReadOnlyDictionary<string, OfficerTraitDefinition> traitDefinitions
        )
        {
            _traitDefinitions =
                traitDefinitions ?? throw new ArgumentNullException(nameof(traitDefinitions));
        }

        /// <summary>
        /// Connects retained Force state to the rules injected into the owning game.
        /// Binding does not replay transitions or overwrite stored eligibility.
        /// </summary>
        /// <param name="rules">The current game's Force rules.</param>
        /// <param name="game">The authoritative game owning the officer and its discovery clock.</param>
        internal void BindForceRules(GameConfig.JediConfig rules, GameRoot game)
        {
            _forceRules = rules ?? throw new ArgumentNullException(nameof(rules));
            _forceGame = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Disconnects live discovery scheduling when this officer leaves the game registry.
        /// Retained ratings remain readable from the bound rules.
        /// </summary>
        internal void UnbindForceRuntime()
        {
            _forceGame = null;
        }

        /// <summary>
        /// Reconciles discovery after registration without replaying a zero-rank transition.
        /// </summary>
        internal void ReconcileForceDiscoveryState()
        {
            if (_forceGame != null && _isDiscoveringForceUser && !_nextForceDiscoveryTick.HasValue)
                NextForceDiscoveryTick = unchecked(_forceGame.CurrentTick + 1);
            if (ForceRank != 0)
                RefreshForceDiscoveryState();
        }

        /// <summary>
        /// Refreshes discovery after this officer changes physical containment or assignment.
        /// </summary>
        protected override void OnHierarchyChanged()
        {
            base.OnHierarchyChanged();
            RefreshForceDiscoveryState();
        }

        /// <summary>
        /// Refreshes discovery after an active or transit state changes in the containing subtree.
        /// </summary>
        protected override void OnAvailabilityChanged()
        {
            base.OnAvailabilityChanged();
            RefreshForceDiscoveryState();
        }

        /// <summary>
        /// Updates retained discovery eligibility and arms a newly enabled scanner for the next tick.
        /// Availability changes evaluate the current rank even when it is zero.
        /// </summary>
        private void RefreshForceDiscoveryState()
        {
            if (_forceGame == null)
                return;

            bool shouldDiscover =
                IsJediTrainer
                && ForceRank >= _forceRules.DiscoveringForceUserThreshold
                && !IsCaptured
                && !IsKilled
                && !IsOnMission()
                && ((IMovable)this).GetTransitMovement() == null
                && IsActive();
            if (_isDiscoveringForceUser == shouldDiscover)
                return;

            _isDiscoveringForceUser = shouldDiscover;
            NextForceDiscoveryTick = shouldDiscover ? unchecked(_forceGame.CurrentTick + 1) : 0;
            ForceDiscoveryOrder = shouldDiscover ? _forceGame.AllocateForceDiscoveryOrder() : 0;
            if (shouldDiscover)
                _forceGame.RecordForceDiscoveryStarted(this);
        }

        /// <summary>
        /// Grants a trait from one stable gameplay source without duplicating that assignment.
        /// </summary>
        /// <param name="traitID">The content-authored trait identifier.</param>
        /// <param name="sourceID">The source that owns the assignment.</param>
        /// <returns>True when a new grant was recorded; otherwise false.</returns>
        internal bool GrantTrait(string traitID, string sourceID)
        {
            RequireTraitDefinition(traitID);
            if (string.IsNullOrWhiteSpace(sourceID))
                throw new ArgumentException(
                    "A trait grant requires a source ID.",
                    nameof(sourceID)
                );
            if (
                TraitGrants.Any(grant =>
                    string.Equals(grant.TraitID, traitID, StringComparison.Ordinal)
                    && string.Equals(grant.SourceID, sourceID, StringComparison.Ordinal)
                )
            )
                return false;

            TraitGrants.Add(new OfficerTraitGrant { TraitID = traitID, SourceID = sourceID });
            return true;
        }

        /// <summary>
        /// Removes the assignment owned by one gameplay source.
        /// </summary>
        /// <param name="traitID">The content-authored trait identifier.</param>
        /// <param name="sourceID">The source that owns the assignment.</param>
        /// <returns>True when an existing grant was removed; otherwise false.</returns>
        internal bool RevokeTrait(string traitID, string sourceID)
        {
            RequireTraitDefinition(traitID);
            if (string.IsNullOrWhiteSpace(sourceID))
                throw new ArgumentException(
                    "A trait grant requires a source ID.",
                    nameof(sourceID)
                );

            return TraitGrants.RemoveAll(grant =>
                    string.Equals(grant.TraitID, traitID, StringComparison.Ordinal)
                    && string.Equals(grant.SourceID, sourceID, StringComparison.Ordinal)
                ) > 0;
        }

        /// <summary>
        /// Returns the travel-time percentage supplied by this officer's active traits.
        /// </summary>
        /// <returns>One hundred for normal travel, or the strongest active reduction.</returns>
        public int GetPersonnelTravelTimePercent()
        {
            return GetPersonnelTravelTimePercent(sharedOnly: false);
        }

        /// <summary>
        /// Returns the travel-time percentage supplied to an accompanying officer group.
        /// </summary>
        /// <returns>One hundred for normal travel, or the strongest shared reduction.</returns>
        internal int GetSharedPersonnelTravelTimePercent()
        {
            return GetPersonnelTravelTimePercent(sharedOnly: true);
        }

        /// <summary>
        /// Returns the strongest active personnel travel modifier matching the requested scope.
        /// </summary>
        /// <param name="sharedOnly">Whether to consider only modifiers authored to affect a group.</param>
        /// <returns>One hundred for normal travel, or the strongest matching reduction.</returns>
        private int GetPersonnelTravelTimePercent(bool sharedOnly)
        {
            int travelTimePercent = _ratingPercentScale;
            foreach (OfficerTraitDefinition definition in _traitDefinitions.Values)
            {
                if (
                    HasActiveTrait(definition.ID)
                    && (!sharedOnly || definition.SharesPersonnelTravelTimeModifier)
                )
                    travelTimePercent = Math.Min(
                        travelTimePercent,
                        definition.PersonnelTravelTimePercent
                    );
            }

            return travelTimePercent;
        }

        /// <summary>
        /// Returns the officer's stored value for the specified rating.
        /// </summary>
        /// <param name="rating">The rating to query.</param>
        /// <returns>The stored rating value.</returns>
        public int GetBaseRating(SkillRating rating)
        {
            return rating switch
            {
                SkillRating.ShipResearch => ShipResearch,
                SkillRating.TroopResearch => TroopResearch,
                SkillRating.FacilityResearch => FacilityResearch,
                SkillRating.None => 0,
                _ => Ratings.TryGetValue(rating, out int value) ? value : 0,
            };
        }

        /// <summary>
        /// Sets the officer's stored value for the specified rating.
        /// </summary>
        /// <param name="rating">The rating to update.</param>
        /// <param name="value">The new rating value.</param>
        /// <returns>The stored rating value.</returns>
        public int SetBaseRating(SkillRating rating, int value)
        {
            switch (rating)
            {
                case SkillRating.ShipResearch:
                    ShipResearch = value;
                    return value;
                case SkillRating.TroopResearch:
                    TroopResearch = value;
                    return value;
                case SkillRating.FacilityResearch:
                    FacilityResearch = value;
                    return value;
                case SkillRating.None:
                    return 0;
                default:
                    if (GetBaseRating(rating) == value)
                        return value;
                    Ratings[rating] = value;
                    RefreshForceRating(rating);
                    return value;
            }
        }

        /// <summary>
        /// Returns the officer's current value for the specified rating.
        /// </summary>
        /// <param name="rating">The rating to query.</param>
        /// <returns>The rating value after officer-specific modifiers.</returns>
        public int GetEffectiveRating(SkillRating rating)
        {
            switch (rating)
            {
                case SkillRating.Diplomacy:
                    return GetForceRating(ref _enhancedDiplomacy, rating);
                case SkillRating.Espionage:
                    return GetForceRating(ref _enhancedEspionage, rating);
                case SkillRating.Combat:
                    return GetForceRating(ref _enhancedCombat, rating);
            }
            int officerRating = GetBaseRating(rating);

            if (rating == SkillRating.Leadership)
            {
                int bonusPercent = 0;
                foreach (OfficerTraitDefinition definition in _traitDefinitions.Values)
                {
                    if (HasActiveTrait(definition.ID))
                        bonusPercent = checked(bonusPercent + definition.LeadershipBonusPercent);
                }
                officerRating = checked(
                    officerRating + IntegerMath.ScaleByPercent(officerRating, bonusPercent)
                );
            }
            return officerRating;
        }

        /// <summary>
        /// Returns whether the officer currently has a trait through either authorship or a grant.
        /// </summary>
        /// <param name="traitID">The trait identifier to find.</param>
        /// <returns>True when the trait is active; otherwise false.</returns>
        private bool HasActiveTrait(string traitID)
        {
            List<string> traits = Traits;
            if (traits != null)
            {
                for (int index = 0; index < traits.Count; index++)
                {
                    if (string.Equals(traits[index], traitID, StringComparison.Ordinal))
                        return true;
                }
            }

            List<OfficerTraitGrant> grants = TraitGrants;
            if (grants != null)
            {
                for (int index = 0; index < grants.Count; index++)
                {
                    OfficerTraitGrant grant = grants[index];
                    if (
                        grant != null
                        && string.Equals(grant.TraitID, traitID, StringComparison.Ordinal)
                    )
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves a validated trait definition or rejects an invalid runtime assignment.
        /// </summary>
        /// <param name="traitID">The trait identifier to resolve.</param>
        /// <returns>The resolved trait definition.</returns>
        private OfficerTraitDefinition RequireTraitDefinition(string traitID)
        {
            if (
                string.IsNullOrWhiteSpace(traitID)
                || !_traitDefinitions.TryGetValue(traitID, out OfficerTraitDefinition definition)
            )
            {
                throw new InvalidOperationException($"Unknown officer trait '{traitID}'.");
            }

            return definition;
        }

        /// <summary>
        /// Increments the officer's stored value for the specified rating.
        /// </summary>
        /// <param name="rating">The rating to increment.</param>
        /// <param name="amount">The amount to add.</param>
        public void IncrementBaseRating(SkillRating rating, int amount = 1)
        {
            SetBaseRating(rating, GetBaseRating(rating) + amount);
        }

        /// <summary>
        /// Returns the base research value for the given discipline.
        /// </summary>
        /// <param name="discipline">The research discipline to query.</param>
        /// <returns>The officer's base research value for that discipline.</returns>
        public int GetBaseRating(ResearchDiscipline discipline)
        {
            return GetBaseRating(GetRatingForResearchDiscipline(discipline));
        }

        /// <summary>
        /// Adds the specified amount to the base research value for the given discipline.
        /// </summary>
        /// <param name="discipline">The research discipline to update.</param>
        /// <param name="amount">The amount to add.</param>
        public void IncrementBaseRating(ResearchDiscipline discipline, int amount = 1)
        {
            IncrementBaseRating(GetRatingForResearchDiscipline(discipline), amount);
        }

        /// <summary>
        /// Returns whether the officer is currently assigned to a mission.
        /// </summary>
        /// <returns>True if the officer's parent is a <see cref="Mission"/>; otherwise, false.</returns>
        public bool IsOnMission()
        {
            return GetParent() is Mission;
        }

        /// <summary>
        /// Attempts to place this officer in the custody of another faction.
        /// </summary>
        /// <param name="captorInstanceId">The instance ID of the capturing faction.</param>
        /// <param name="game">The game whose clock and captive rules govern the transition.</param>
        /// <param name="random">The simulation random stream used to arm the escape timer.</param>
        /// <param name="canEscape">Whether the officer can attempt to escape captivity.</param>
        /// <returns>True when the officer was captured; otherwise false.</returns>
        internal bool TryCapture(
            string captorInstanceId,
            GameRoot game,
            IRandomNumberProvider random,
            bool canEscape = true
        )
        {
            if (((IMovable)this).GetTransitMovement() != null)
            {
                GameLogger.Warning(
                    $"Capture rejected: {GetDisplayName()} is in transit and cannot be captured."
                );
                return false;
            }

            bool captureStateChanged = !IsCaptured;
            IsCaptured = true;
            CaptorInstanceID = captorInstanceId;
            if (captureStateChanged && !CanEscape)
            {
                CanEscape = true;
                ScheduleEscapeAttempt(game, random, game.CurrentTick);
            }
            bool escapeStateChanged = CanEscape != canEscape;
            CanEscape = canEscape;
            if (escapeStateChanged)
            {
                if (canEscape)
                    ScheduleEscapeAttempt(game, random, game.CurrentTick);
                else
                    NextEscapeAttemptTick = 0;
            }
            return true;
        }

        /// <summary>
        /// Arms this officer's next escape attempt before subsequent operations consume randomness.
        /// Fixed intervals do not advance the simulation random stream.
        /// </summary>
        /// <param name="game">The game whose clock and captive rules determine the due tick.</param>
        /// <param name="random">The simulation random stream.</param>
        /// <param name="previousTick">The capture tick or the preceding attempt's due tick.</param>
        internal void ScheduleEscapeAttempt(
            GameRoot game,
            IRandomNumberProvider random,
            int previousTick
        )
        {
            GameConfig.TickRangeConfig interval = game.Config.Captive.EscapeAttemptInterval;
            int minimum = Math.Max(0, interval.Minimum);
            int maximum = Math.Max(minimum, interval.Maximum);
            int delay = maximum > minimum ? random.NextInt(minimum, maximum + 1) : minimum;
            NextEscapeAttemptTick = IsCaptured && CanEscape ? checked(previousTick + delay) : 0;
        }

        /// <summary>
        /// Clears custody and disables the escape timer when this officer is released.
        /// </summary>
        internal void ReleaseFromCaptivity()
        {
            IsCaptured = false;
            CaptorInstanceID = null;
            CanEscape = false;
            NextEscapeAttemptTick = 0;
        }

        /// <summary>
        /// Returns whether the injured officer is resting at a location controlled by their faction.
        /// </summary>
        /// <returns>True when the officer can recover injury points; otherwise, false.</returns>
        public bool CanHeal()
        {
            if (
                InjuryPoints <= 0
                || IsCaptured
                || IsKilled
                || IsOnMission()
                || ((IMovable)this).GetTransitMovement() != null
            )
                return false;

            string ownerInstanceID = GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerInstanceID))
                return false;

            CapitalShip ship = GetParentOfType<CapitalShip>();
            if (ship?.GetOwnerInstanceID() == ownerInstanceID)
                return true;

            Fleet fleet = GetParentOfType<Fleet>();
            if (fleet?.GetOwnerInstanceID() == ownerInstanceID)
                return true;

            return GetParentOfType<Planet>()?.GetOwnerInstanceID() == ownerInstanceID;
        }

        /// <summary>
        /// Returns the retained eligibility for the accelerated Force-user healing rate.
        /// </summary>
        /// <returns>The stored eligibility, initialized from a nonzero rank when absent.</returns>
        public bool HealsFast()
        {
            _fastHealing ??= CalculateFastHealing();
            return _fastHealing.Value;
        }

        /// <summary>
        /// Calculates healing eligibility using the bound rules without treating zero as a rank.
        /// </summary>
        /// <returns>True when a nonzero effective rank meets the configured threshold.</returns>
        private bool CalculateFastHealing()
        {
            GameConfig.JediConfig rules =
                _forceRules
                ?? throw new InvalidOperationException(
                    "Force rules are not configured for this officer."
                );
            return ForceRank != 0 && ForceRank >= rules.FastHealThreshold;
        }

        /// <summary>
        /// Returns whether the officer is a Jedi whose Force potential has not yet been revealed.
        /// </summary>
        /// <returns>True if this officer is an undiscovered Force user.</returns>
        public bool IsUndiscoveredForceUser()
        {
            return IsForceSensitive
                && !IsForceEligible
                && !IsCaptured
                && !IsKilled
                && !IsOnMission();
        }

        /// <summary>
        /// Initializes a revealed Force rating only while its stored experience is zero.
        /// </summary>
        /// <param name="provider">The random number provider used for the inclusive template variance.</param>
        internal void InitializeForceValue(IRandomNumberProvider provider)
        {
            if (ForceValue == 0 && IsForceSensitive && IsForceEligible)
            {
                int upperBound = unchecked(
                    JediLevelVariance < 0 ? 1 - JediLevelVariance : JediLevelVariance + 1
                );
                int variance = upperBound > 1 ? provider.NextInt(0, upperBound) : 0;
                ForceValue = JediLevel + (JediLevelVariance < 0 ? -variance : variance);
            }
        }

        /// <summary>
        /// Adds the specified amount to injury points, clamped to [0, maxInjury].
        /// </summary>
        /// <param name="amount">The amount of injury to apply.</param>
        /// <param name="maxInjury">Upper bound for injury points.</param>
        public void ApplyInjury(int amount, int maxInjury)
        {
            InjuryPoints = Math.Min(maxInjury, Math.Max(0, InjuryPoints + amount));
        }

        /// <summary>
        /// Subtracts the specified amount from injury points, floored at zero.
        /// </summary>
        /// <param name="amount">The amount to heal.</param>
        public void Heal(int amount)
        {
            InjuryPoints = Math.Max(0, InjuryPoints - amount);
        }

        /// <summary>
        /// Maps a research discipline to the officer rating that stores its base value.
        /// </summary>
        /// <param name="discipline">The research discipline to map.</param>
        /// <returns>The corresponding officer rating, or <see cref="SkillRating.None"/>.</returns>
        public static SkillRating GetRatingForResearchDiscipline(ResearchDiscipline discipline)
        {
            return discipline switch
            {
                ResearchDiscipline.ShipDesign => SkillRating.ShipResearch,
                ResearchDiscipline.FacilityDesign => SkillRating.FacilityResearch,
                ResearchDiscipline.TroopTraining => SkillRating.TroopResearch,
                _ => SkillRating.None,
            };
        }

        /// <summary>
        /// Returns a rating value after applying this officer's Force bonus.
        /// </summary>
        /// <param name="baseRating">The stored rating value.</param>
        /// <returns>The Force-adjusted rating value.</returns>
        private int ApplyForceRatingBonus(int baseRating)
        {
            return baseRating + (baseRating * ForceRank) / _ratingPercentScale;
        }

        /// <summary>
        /// Calculates the signed stored rating after Force and injury modifiers.
        /// </summary>
        /// <param name="rating">The diplomacy, espionage, or combat rating to calculate.</param>
        /// <returns>The signed sixteen-bit enhanced rating.</returns>
        private int CalculateForceRating(SkillRating rating)
        {
            int enhanced = ApplyForceRatingBonus(GetBaseRating(rating));
            if (rating == SkillRating.Combat)
                enhanced = Math.Max(0, enhanced - InjuryPoints);
            return unchecked((short)enhanced);
        }

        /// <summary>
        /// Restores a missing stored rating from the officer's current state before returning it.
        /// </summary>
        /// <param name="stored">The enhanced rating retained across state transitions.</param>
        /// <param name="rating">The rating whose modifiers apply.</param>
        /// <returns>The retained enhanced rating.</returns>
        private int GetForceRating(ref int? stored, SkillRating rating)
        {
            stored ??= CalculateForceRating(rating);
            return stored.Value;
        }

        /// <summary>
        /// Initializes missing derived Force state before a transition replaces its inputs.
        /// </summary>
        private void InitializeForceState()
        {
            GetForceRating(ref _enhancedDiplomacy, SkillRating.Diplomacy);
            GetForceRating(ref _enhancedEspionage, SkillRating.Espionage);
            GetForceRating(ref _enhancedCombat, SkillRating.Combat);
            if (_forceRules != null)
                _fastHealing ??= CalculateFastHealing();
        }

        /// <summary>
        /// Refreshes derived ratings, healing, and discovery after a nonzero rank change.
        /// At zero, base ratings and injury still refresh their corresponding skill values.
        /// </summary>
        private void RefreshForceState()
        {
            if (ForceRank == 0)
                return;
            RefreshForceRating(SkillRating.Diplomacy);
            RefreshForceRating(SkillRating.Espionage);
            RefreshForceRating(SkillRating.Combat);
            if (_forceRules != null)
                _fastHealing = CalculateFastHealing();
            RefreshForceDiscoveryState();
        }

        /// <summary>
        /// Refreshes only the enhanced rating owned by a changed base rating.
        /// </summary>
        /// <param name="rating">The rating whose base value changed.</param>
        private void RefreshForceRating(SkillRating rating)
        {
            switch (rating)
            {
                case SkillRating.Diplomacy:
                    _enhancedDiplomacy = CalculateForceRating(rating);
                    break;
                case SkillRating.Espionage:
                    _enhancedEspionage = CalculateForceRating(rating);
                    break;
                case SkillRating.Combat:
                    _enhancedCombat = CalculateForceRating(rating);
                    break;
            }
        }

        /// <summary>
        /// Returns whether the officer can be ordered to move.
        /// </summary>
        /// <returns>True if the officer has no active movement state and is not on an incomplete mission; otherwise, false.</returns>
        public bool IsMovable()
        {
            if (GetParent() is Mission mission)
                return mission.IsComplete() && Movement == null;
            return Movement == null;
        }

        /// <summary>
        /// Selects one available voice path for an officer event.
        /// </summary>
        /// <param name="voiceLineType">The officer event requesting a voice response.</param>
        /// <param name="provider">The random source used when multiple paths are available.</param>
        /// <returns>The selected path, or null when no matching voice exists.</returns>
        public string GetVoicePath(
            OfficerVoiceLineType voiceLineType,
            IRandomNumberProvider provider
        )
        {
            return SelectVoicePath(VoiceSet.GetPaths(voiceLineType), provider);
        }

        /// <summary>
        /// Reports whether an officer event has at least one available voice path.
        /// </summary>
        /// <param name="voiceLineType">The officer event to inspect.</param>
        /// <returns>True when a matching voice path exists.</returns>
        public bool HasVoicePath(OfficerVoiceLineType voiceLineType)
        {
            IReadOnlyList<string> paths = VoiceSet.GetPaths(voiceLineType);
            return paths?.Count > 0;
        }

        /// <summary>
        /// Selects one path from an available voice collection.
        /// </summary>
        /// <param name="paths">The available voice paths.</param>
        /// <param name="provider">The optional random source.</param>
        /// <returns>The selected path, or null when the collection is empty.</returns>
        private static string SelectVoicePath(
            IReadOnlyList<string> paths,
            IRandomNumberProvider provider
        )
        {
            if (paths == null || paths.Count == 0)
                return null;

            if (paths.Count == 1 || provider == null)
                return paths[0];

            return paths[provider.NextInt(0, paths.Count)];
        }
    }
}
