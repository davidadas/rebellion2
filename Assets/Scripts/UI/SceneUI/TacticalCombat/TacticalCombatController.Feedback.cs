using System;
using System.Threading.Tasks;
using Rebellion.Game.SpaceCombat;
using Rebellion.Tactical;
using UnityEngine;

namespace Rebellion.UI.SceneUI.TacticalCombat
{
    public sealed partial class TacticalCombatController : IDisposable
    {
        /// <summary>
        /// Starts an owned notification playback using its authored loop setting.
        /// </summary>
        /// <param name="clip">The selected notification variant.</param>
        /// <param name="loop">Whether the scheduler owns a repeating playback.</param>
        /// <returns>The playback's actual activity and cancellation boundary.</returns>
        private ITacticalCombatNotificationPlayback PlayNotification(
            TacticalCombatSoundClip clip,
            bool loop
        )
        {
            if (_disposed || AudioManager.Instance == null)
                return null;
            AudioPlaybackHandle playback = AudioManager.Instance.PlaySfxInstance(
                _assets.GetPreloadedAudio(clip.Path),
                loop
            );
            return playback == null ? null : new NotificationPlayback(playback);
        }

        /// <summary>
        /// Captures a fired weapon's visual trail.
        /// </summary>
        /// <param name="shot">The applied discharge.</param>
        private void HandleShot(TacticalCombatShot shot) =>
            _view.Renderer.ShowShot(shot, _session.GetSide(shot.Source.Side).FactionIndex);

        /// <summary>
        /// Projects the surface reached by an applied impact without repeating damage calculations.
        /// </summary>
        /// <param name="unit">The impacted encounter participant.</param>
        /// <param name="weapon">The impacting weapon.</param>
        /// <param name="impact">The affected surface or destruction.</param>
        private void HandleImpact(
            TacticalCombatUnit unit,
            TacticalWeaponType weapon,
            TacticalCombatImpactKind impact
        ) => _view.Renderer.ShowImpact(unit, weapon, impact);

        /// <summary>
        /// Displays the station's superlaser without changing its queued damage command.
        /// </summary>
        /// <param name="station">The firing station.</param>
        /// <param name="target">The enemy capital ship.</param>
        private void HandleSuperlaser(TacticalCombatUnit station, TacticalCombatUnit target) =>
            _view.Renderer.ShowSuperlaser(station, target, _session.BattleDepth);

        /// <summary>
        /// Begins the effect for a direct destruction command before its participant is deactivated.
        /// </summary>
        /// <param name="unit">The unit admitted for destruction.</param>
        private void HandleDestructionEffect(TacticalCombatUnit unit) =>
            _view.Renderer.ShowDestruction(unit);

        /// <summary>
        /// Closes station controls after their station is destroyed.
        /// </summary>
        /// <param name="unit">The destroyed encounter participant.</param>
        private void HandleDestroyed(TacticalCombatUnit unit)
        {
            if (unit.IsBattleStation)
                HandleStationControlsClosed();
        }

        /// <summary>
        /// Closes station controls when their owned station has departed.
        /// </summary>
        /// <param name="unit">The departed participant.</param>
        private void HandleStationDeparture(TacticalCombatUnit unit)
        {
            if (unit.IsBattleStation)
                HandleStationControlsClosed();
        }

        /// <summary>
        /// Releases the charge header without replacing an unrelated inspection context.
        /// </summary>
        private void HandleStationControlsClosed()
        {
            _stationControlsVisible = false;
            _hudRefresh = 0;
        }

        /// <summary>
        /// Begins the outcome movie with its own cancellable playback and restricted skip controls.
        /// </summary>
        /// <param name="path">The authored outcome video address.</param>
        /// <param name="completed">Acknowledges the battle's movie pause after playback.</param>
        /// <returns>Ownership that cancels only this movie.</returns>
        private IDisposable PlayAttackRunMovie(string path, Action completed)
        {
            return AppBootstrap
                .Instance.GetCutsceneManager()
                .PlayOwned(path, completed, CutsceneSkipInput.EscapeOrPrimaryPointer);
        }

        /// <summary>
        /// Transfers visibility and pointer focus between battle presentation and full-screen video.
        /// </summary>
        /// <param name="active">Whether the movie owns presentation.</param>
        private void SetMoviePresentation(bool active)
        {
            if (_moviePresentation == null)
            {
                AppInputController input = AppBootstrap.Instance.GetInputController();
                _moviePresentation = new TacticalCombatMoviePresentation(
                    SetBattlePresentation,
                    () => input.PushContext(InputContext.Cutscene),
                    input.PopContext
                );
            }
            _moviePresentation.SetActive(active);
        }

        /// <summary>
        /// Releases battle sounds before hiding and permits new sounds after the battle reactivates.
        /// A disposed controller or destroyed screen cannot resume battle presentation.
        /// </summary>
        /// <param name="active">Whether battle presentation is visible.</param>
        private void SetBattlePresentation(bool active)
        {
            if (_disposed || _view == null)
                return;
            if (!active)
                _session.Audio.SetPlaybackAvailable(false);
            _view.gameObject.SetActive(active);
            if (active)
            {
                CaptureHeldKeys();
                _session.Audio.SetPlaybackAvailable(true);
            }
        }

        /// <summary>
        /// Posts a failed movie's acknowledgement after the current battle command returns.
        /// </summary>
        /// <param name="completed">Releases the failed movie's simulation and presentation ownership.</param>
        private async void PostMovieCompletion(Action completed)
        {
            await Task.Yield();
            if (!_disposed)
                completed();
        }

        /// <summary>
        /// Reports a video that could not start before its deferred completion acknowledgement.
        /// </summary>
        /// <param name="exception">The playback failure.</param>
        private void ReportMovieError(Exception exception)
        {
            Debug.LogException(exception);
        }

        /// <summary>
        /// Adapts one application-owned playback to the notification scheduler's live status.
        /// </summary>
        private sealed class NotificationPlayback : ITacticalCombatNotificationPlayback
        {
            private readonly AudioPlaybackHandle playback;
            public bool IsPlaying =>
                playback.IsActive
                && (playback.Paused || AudioListener.pause || playback.Source?.isPlaying == true);

            /// <summary>
            /// Captures the independently controllable sound source.
            /// </summary>
            /// <param name="playback">The application's ownership handle.</param>
            public NotificationPlayback(AudioPlaybackHandle playback)
            {
                this.playback = playback;
            }

            /// <summary>
            /// Stops only the source owned by this notification.
            /// </summary>
            public void Stop() => playback.Stop();
        }
    }
}
