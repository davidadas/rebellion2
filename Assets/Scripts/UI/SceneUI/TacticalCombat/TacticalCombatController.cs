using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Content;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Tactical.Rendering;
using UnityEngine;
using Stopwatch = global::System.Diagnostics.Stopwatch;

namespace Rebellion.UI.SceneUI.TacticalCombat
{
    /// <summary>
    /// Translates tactical presentation input into encounter commands and projects live battle state.
    /// </summary>
    public sealed partial class TacticalCombatController : IDisposable
    {
        private readonly TacticalCombatSession _session;
        private readonly TacticalCombatView _view;
        private readonly Action<TacticalCombatSession> _complete;
        private readonly CancellationTokenSource _cancellation;
        private readonly CancellationToken _lifetime;
        private IReadOnlyList<TacticalCombatGroup> _selectedGroups => _session.Selection.Groups;
        private readonly TacticalCombatHudState _hud = new();
        private readonly bool[] _navigationSelected = new bool[4];
        private readonly bool[] _factionHighlighted = new bool[2];
        public event Action<TacticalCombatControl> MenuControlRequested;
        private ContentAssets _assets;
        private TacticalCombatCutsceneController _cutscenes;
        private TacticalCombatMoviePresentation _moviePresentation;
        private bool _ready;
        private bool _disposed;
        private bool _completing;
        private bool _stationControlsVisible;
        private bool _applicationMenuOpen;
        private bool _inspectionKeyHeld;
        private bool _escapeKeyHeld;
        private float _hudRefresh;
        private readonly TacticalCombatInspection _inspection;
        private readonly TacticalCombatRenderSettings _videoSettings;
        private readonly ITacticalCombatPresentationDriver _driver;

        /// <summary>
        /// Owns the authored view and its input subscriptions for one isolated battle.
        /// </summary>
        /// <param name="session">The deployed encounter.</param>
        /// <param name="view">The instantiated battle view owned by this controller.</param>
        /// <param name="complete">Commits the completed encounter.</param>
        /// <param name="cancellationToken">The campaign screen lifetime.</param>
        /// <param name="videoSettings">The active video settings, or null to use their authored defaults for an isolated encounter.</param>
        /// <param name="driver">The shared battle's presentation driver, or null for a local encounter.</param>
        public TacticalCombatController(
            TacticalCombatSession session,
            TacticalCombatView view,
            Action<TacticalCombatSession> complete,
            CancellationToken cancellationToken,
            UserVideoSettings videoSettings = null,
            ITacticalCombatPresentationDriver driver = null
        )
        {
            this._session = session ?? throw new ArgumentNullException(nameof(session));
            if (driver != null && !ReferenceEquals(driver.Session, session))
                throw new ArgumentException(
                    "The presentation driver belongs to another encounter.",
                    nameof(driver)
                );
            _driver = driver;
            _inspection = new TacticalCombatInspection(session);
            _videoSettings = new TacticalCombatRenderSettings(
                videoSettings ?? new UserVideoSettings()
            );
            this._view = view != null ? view : throw new ArgumentNullException(nameof(view));
            this._complete = complete ?? throw new ArgumentNullException(nameof(complete));
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _lifetime = _cancellation.Token;
            view.ControlRequested += HandleControl;
            session.Selection.ContextReplaced += HandleContextReplaced;
            session.Selection.StationControlsClosed += HandleStationControlsClosed;
            session.Selection.WithdrawalStarting += _hud.BeginWithdrawal;
            session.Selection.WithdrawalDispatched += _hud.CompleteWithdrawal;
            session.UnitWithdrawn += HandleStationDeparture;
            _stationControlsVisible =
                session.GetSide(session.PlayerSide).FactionIndex == 1
                && session.BattleStation != null;
            _hud.ReplaceContext(session.Selection.Kind);
            view.PointerPressed += HandlePointerDown;
            view.KeyboardRequested += HandleKeyboardEvent;
            view.Renderer.ArrivalPresentationChanged += HandleArrivalStrobe;
            session.ShotFired += HandleShot;
            session.Audio.Playback = PlayNotification;
            session.ImpactApplied += HandleImpact;
            session.DestructionEffectRequested += HandleDestructionEffect;
            session.SuperlaserFired += HandleSuperlaser;
            session.UnitDestroyed += HandleDestroyed;
            view.SetStatus("Preparing battle…");
            view.SetSelection(string.Empty);
            RefreshControls();
        }

        /// <summary>
        /// Loads required battle media before allowing any simulation progress.
        /// </summary>
        /// <param name="models">The application's shared model cache.</param>
        /// <param name="assets">The selected pack's media source.</param>
        /// <returns>The asynchronous preparation operation.</returns>
        public async Task PrepareAsync(ContentModelCache models, ContentAssets assets)
        {
            _assets = assets;
            if (_session.BattleStation != null)
            {
                if (
                    string.IsNullOrWhiteSpace(_session.Definition?.AttackRunSuccessMoviePath)
                    || string.IsNullOrWhiteSpace(_session.Definition?.AttackRunFailureMoviePath)
                )
                    throw new InvalidDataException(
                        "Tactical combat requires success and failure movie addresses for battle-station attacks."
                    );
                _ = assets.GetVideoUrl(_session.Definition.AttackRunSuccessMoviePath);
                _ = assets.GetVideoUrl(_session.Definition.AttackRunFailureMoviePath);
                _cutscenes = new TacticalCombatCutsceneController(
                    _session,
                    PlayAttackRunMovie,
                    SetMoviePresentation,
                    ReportMovieError,
                    PostMovieCompletion,
                    _driver == null ? null : _driver.AcknowledgeMovie
                );
            }
            await Task.WhenAll(
                _session
                    .Audio.Clips.Select(clip => clip.Path)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.Ordinal)
                    .Select(assets.LoadAudioAsync)
            );
            _lifetime.ThrowIfCancellationRequested();
            TacticalCombatFactionDefinition faction =
                _session.Definition.Factions.Find(value =>
                    value.FactionIndex == _session.GetSide(_session.PlayerSide).FactionIndex
                )
                ?? throw new InvalidDataException(
                    "Tactical combat HUD requires a definition for the player's faction."
                );
            _view.PrepareArtwork(_session.Definition, faction, assets);
            await _view.Renderer.PrepareAsync(
                _session,
                models,
                assets,
                _lifetime,
                _videoSettings,
                _driver?.ReplicaPlayback
            );
            _lifetime.ThrowIfCancellationRequested();
            if (
                _videoSettings.ShowStarfield
                && !string.IsNullOrWhiteSpace(_session.Definition?.BackgroundImagePath)
            )
                _view.SetBackgroundTexture(
                    assets.GetTexture(_session.Definition.BackgroundImagePath)
                );
            TacticalCombatArrival arrival = TacticalCombatArrival.CreateFor(
                _session,
                _view.Renderer
            );
            if (_driver != null)
                _driver.Prepare(arrival);
            else
            {
                Stopwatch clock = Stopwatch.StartNew();
                _session.ConfigureClockedAdvance(() => unchecked((uint)clock.ElapsedMilliseconds));
                _session.BeginArrival(arrival);
            }
            CaptureHeldKeys();
            _ready = true;
            RefreshHud();
            RefreshControls();
        }

        /// <summary>
        /// Advances the encounter and projects one screen frame while campaign ticking remains suspended.
        /// </summary>
        /// <param name="elapsedSeconds">The unscaled presentation interval.</param>
        public void Advance(float elapsedSeconds)
        {
            if (_disposed || !_ready)
                return;
            if (_driver != null)
                _driver.Advance(elapsedSeconds);
            else
                _session.AdvanceClocked(elapsedSeconds * 1000);
            if (_disposed)
                return;
            if (_session.IsComplete && !_completing && _driver?.CompletionReady != false)
            {
                _completing = true;
                _complete(_session);
                return;
            }
            if (_session.PendingAttackRunMovie != null)
                return;
            Vector2Int size = _view.PixelSize;
            _view.Renderer.Resize(size.x, size.y);
            _view.SetViewportTexture(_view.Renderer.Target);
            _view.Renderer.AdvanceEffects(elapsedSeconds);
            _view.Renderer.RenderUnits();
            RefreshNavigationSelection();
            _hudRefresh -= elapsedSeconds;
            if (_hudRefresh <= 0)
            {
                RefreshHud();
                RefreshControls();
                _hudRefresh = 0.1f;
            }
        }

        /// <summary>
        /// Cancels pending preparation, removes subscriptions, and destroys the owned authored screen.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _cutscenes?.Dispose();
            _cutscenes = null;
            _moviePresentation?.Dispose();
            _moviePresentation = null;
            _driver?.Dispose();
            _cancellation.Cancel();
            _cancellation.Dispose();
            _view.ControlRequested -= HandleControl;
            _session.Selection.ContextReplaced -= HandleContextReplaced;
            _session.Selection.StationControlsClosed -= HandleStationControlsClosed;
            _session.Selection.WithdrawalStarting -= _hud.BeginWithdrawal;
            _session.Selection.WithdrawalDispatched -= _hud.CompleteWithdrawal;
            _session.UnitWithdrawn -= HandleStationDeparture;
            _view.PointerPressed -= HandlePointerDown;
            _view.KeyboardRequested -= HandleKeyboardEvent;
            _view.Renderer.ArrivalPresentationChanged -= HandleArrivalStrobe;
            _session.ShotFired -= HandleShot;
            _session.Audio.Playback = null;
            _session.Selection.Dispose();
            _session.Audio.Dispose();
            _session.ImpactApplied -= HandleImpact;
            _session.DestructionEffectRequested -= HandleDestructionEffect;
            _session.SuperlaserFired -= HandleSuperlaser;
            _session.UnitDestroyed -= HandleDestroyed;
            if (_view != null)
                UnityEngine.Object.Destroy(_view.gameObject);
        }
    }
}
