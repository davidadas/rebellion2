using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Rebellion.Tests.UI.SceneUI.OptionsMenu
{
    [TestFixture]
    public sealed class OptionsBindingSessionTests
    {
        private GameObject _root;
        private InputManager _inputManager;

        /// <summary>
        /// Creates the generated input asset used by each binding-schema test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("InputManager");
            _inputManager = _root.AddComponent<InputManager>();
        }

        /// <summary>
        /// Destroys the generated input asset after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BeginRebind_PresentationFailure_ReleasesInputAndAllowsRetry(
            bool failNotification
        )
        {
            WithKeyboard(
                (session, manager, _, _) =>
                {
                    session.Rebuild();
                    int row = FindRow(session, "Increase Game Speed");
                    Action fail = () =>
                        throw new InvalidOperationException("Injected presentation failure.");
                    session.PresentationChanged += fail;
                    string error = null;
                    session.Failed += message => error = message;
                    ExpectRebindFailure();
                    if (failNotification)
                    {
                        session.Failed += _ =>
                            throw new InvalidOperationException(
                                "Injected error notification failure."
                            );
                        LogAssert.Expect(
                            LogType.Error,
                            new Regex("Shortcut error notification failed:")
                        );
                    }

                    Assert.DoesNotThrow(() => session.BeginRebind(row, false));

                    session.PresentationChanged -= fail;
                    InputSystem.Update();
                    Assert.AreEqual(-1, session.ListeningRow);
                    Assert.IsFalse(session.HasPendingConflict);
                    Assert.IsFalse(manager.AreApplicationShortcutsSuppressed);
                    StringAssert.Contains("restored", error);
                    session.BeginRebind(row, false);
                    Assert.AreEqual(row, session.ListeningRow);
                    session.CancelRebind();
                }
            );
        }

        [Test]
        public void BeginRebind_ChangeNotificationFailure_RestoresCompleteBindingOverrides()
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    InputAction action = manager.Asset.FindAction(
                        "Strategy/IncreaseGameSpeed",
                        true
                    );
                    int primary = FindBinding(action, "Primary");
                    action.ApplyBindingOverride(
                        primary,
                        new InputBinding
                        {
                            overridePath = "<Keyboard>/v",
                            overrideInteractions = "press(behavior=1)",
                            overrideProcessors = "scale(factor=0.5)",
                        }
                    );
                    string original = manager.SaveBindingOverrides();
                    session.Rebuild();
                    int row = FindRow(session, "Increase Game Speed");
                    session.Changed += () =>
                        throw new InvalidOperationException("Injected change failure.");
                    ExpectRebindFailure();
                    session.BeginRebind(row, false);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightBracket));
                    InputSystem.Update();

                    FinishCapture(fixture);
                    InputSystem.Update();

                    Assert.AreEqual(original, manager.SaveBindingOverrides());
                    Assert.AreEqual(-1, session.ListeningRow);
                    Assert.IsFalse(manager.AreApplicationShortcutsSuppressed);
                    session.BeginRebind(row, true);
                    Assert.AreEqual(row, session.ListeningRow);
                    session.CancelRebind();
                }
            );
        }

        [Test]
        public void BeginRebind_ConflictPromptFailure_RestoresBindingAndClearsConflict()
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    InputAction oldAction = manager.Asset.FindAction("Strategy/ShowTroopers", true);
                    oldAction.ApplyBindingOverride(
                        FindBinding(oldAction, "Primary"),
                        "<Keyboard>/t"
                    );
                    string original = manager.SaveBindingOverrides();
                    session.Rebuild();
                    session.ConflictRequested += _ =>
                        throw new InvalidOperationException("Injected conflict prompt failure.");
                    ExpectRebindFailure();
                    session.BeginRebind(FindRow(session, "Increase Game Speed"), false);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T));
                    InputSystem.Update();

                    FinishCapture(fixture);
                    InputSystem.Update();

                    Assert.AreEqual(original, manager.SaveBindingOverrides());
                    Assert.IsFalse(session.HasPendingConflict);
                    Assert.IsFalse(manager.AreApplicationShortcutsSuppressed);
                    session.BeginRebind(FindRow(session, "Increase Game Speed"), true);
                    Assert.GreaterOrEqual(session.ListeningRow, 0);
                    session.CancelRebind();
                }
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResolveConflict_ChangeNotificationFailure_RestoresBothBindings(bool sameAction)
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    InputAction action = manager.Asset.FindAction(
                        "Strategy/IncreaseGameSpeed",
                        true
                    );
                    InputAction oldAction = sameAction
                        ? action
                        : manager.Asset.FindAction("Strategy/ShowTroopers", true);
                    oldAction.ApplyBindingOverride(
                        FindBinding(oldAction, sameAction ? "Secondary" : "Primary"),
                        "<Keyboard>/t"
                    );
                    string original = manager.SaveBindingOverrides();
                    session.Rebuild();
                    int row = FindRow(session, "Increase Game Speed");
                    session.BeginRebind(row, false);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T));
                    InputSystem.Update();
                    FinishCapture(fixture);
                    InputSystem.Update();
                    Assert.IsTrue(session.HasPendingConflict);
                    session.Changed += () =>
                        throw new InvalidOperationException(
                            "Injected conflict acceptance failure."
                        );
                    ExpectRebindFailure();

                    Assert.DoesNotThrow(() => session.ResolveConflict(true));

                    Assert.AreEqual(original, manager.SaveBindingOverrides());
                    Assert.IsFalse(session.HasPendingConflict);
                    Assert.IsFalse(manager.AreApplicationShortcutsSuppressed);
                    session.BeginRebind(row, true);
                    Assert.AreEqual(row, session.ListeningRow);
                    session.CancelRebind();
                }
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResolveConflict_RollbackFailure_ReportsRestartAndReleasesInput(
            bool replaceBinding
        )
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    InputAction action = manager.Asset.FindAction(
                        "Strategy/IncreaseGameSpeed",
                        true
                    );
                    InputAction oldAction = manager.Asset.FindAction("Strategy/ShowTroopers", true);
                    oldAction.ApplyBindingOverride(
                        FindBinding(oldAction, "Primary"),
                        "<Keyboard>/t"
                    );
                    int originalCount = action.bindings.Count;
                    session.Rebuild();
                    int row = FindRow(session, "Increase Game Speed");
                    session.BeginRebind(row, false);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T));
                    InputSystem.Update();
                    FinishCapture(fixture);
                    InputSystem.Update();
                    session.Changed += () =>
                    {
                        action.ChangeBinding(0).Erase();
                        if (replaceBinding)
                            action.AddBinding("<Keyboard>/z").WithName("Primary");
                        throw new InvalidOperationException(
                            "Injected schema change during conflict acceptance."
                        );
                    };
                    string error = null;
                    session.Failed += message => error = message;
                    ExpectRebindFailure();

                    Assert.DoesNotThrow(() => session.ResolveConflict(true));

                    Assert.AreEqual(-1, session.ListeningRow);
                    Assert.IsFalse(session.HasPendingConflict);
                    Assert.IsFalse(manager.AreApplicationShortcutsSuppressed);
                    StringAssert.Contains("Restart the game", error);
                    Assert.IsTrue(manager.RequiresInputRestart);
                    Assert.IsFalse(manager.TrySaveBindingOverrides(out _));
                    session.BeginRebind(row, true);
                    session.RestoreDefault(row);
                    session.RestoreAllDefaults();
                    Assert.AreEqual(-1, session.ListeningRow);
                    Assert.DoesNotThrow(session.Rebuild);
                    Assert.AreEqual(
                        "<Keyboard>/t",
                        oldAction.bindings[FindBinding(oldAction, "Primary")].effectivePath
                    );
                    Assert.AreEqual(
                        originalCount - (replaceBinding ? 0 : 1),
                        action.bindings.Count
                    );
                }
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Dispose_UnfinishedEdit_RestoresPreviousBinding(bool conflict)
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    InputAction oldAction = manager.Asset.FindAction("Strategy/ShowTroopers", true);
                    oldAction.ApplyBindingOverride(
                        FindBinding(oldAction, "Primary"),
                        "<Keyboard>/t"
                    );
                    string original = manager.SaveBindingOverrides();
                    session.Rebuild();
                    session.BeginRebind(FindRow(session, "Increase Game Speed"), false);
                    if (conflict)
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T));
                        InputSystem.Update();
                        FinishCapture(fixture);
                        InputSystem.Update();
                    }

                    session.Dispose();

                    Assert.AreEqual(original, manager.SaveBindingOverrides());
                    Assert.IsFalse(session.HasPendingConflict);
                    Assert.IsFalse(manager.AreApplicationShortcutsSuppressed);
                }
            );
        }

        [TestCase(Key.Minus, "[")]
        [TestCase(Key.Equals, "]")]
        [TestCase(Key.RightBracket, "=")]
        [TestCase(Key.Quote, "-")]
        [TestCase(Key.T, "y")]
        public void Rebuild_DvorakKeyboard_ShowsActiveKeyName(Key key, string displayName)
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    fixture.SetKeyInfo(key, displayName);
                    InputAction action = manager.Asset.FindAction("Strategy/ShowTroopers", true);
                    action.ApplyBindingOverride(
                        FindBinding(action, "Primary"),
                        "<Keyboard>/" + keyboard[key].name
                    );

                    session.Rebuild();

                    Assert.AreEqual(
                        displayName.ToUpperInvariant(),
                        session.Rows.Single(row => row.Action == "Show Troopers").Primary
                    );
                }
            );
        }

        [Test]
        public void Rebuild_DvorakChord_ShowsActiveKeyName()
        {
            WithKeyboard(
                (session, _, _, fixture) =>
                {
                    fixture.SetKeyInfo(Key.Equals, "]");

                    session.Rebuild();

                    string modifier =
                        Application.platform == RuntimePlatform.OSXEditor ? "CMD" : "CTRL";
                    Assert.AreEqual(
                        modifier + "+]",
                        session.Rows.Single(row => row.Action == "Increase Game Speed").Primary
                    );
                }
            );
        }

        [TestCase(Key.LeftShift, "<Keyboard>/shift")]
        [TestCase(Key.RightShift, "<Keyboard>/shift")]
        [TestCase(Key.LeftCtrl, "<Keyboard>/ctrl")]
        [TestCase(Key.RightCtrl, "<Keyboard>/ctrl")]
        [TestCase(Key.LeftAlt, "<Keyboard>/alt")]
        [TestCase(Key.RightAlt, "<Keyboard>/alt")]
        [TestCase(Key.LeftMeta, "<Keyboard>/leftMeta")]
        [TestCase(Key.RightMeta, "<Keyboard>/leftMeta")]
        public void BeginRebind_ReleasedModifier_PreservesCapturedChord(
            Key modifier,
            string expectedPath
        )
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    session.Rebuild();
                    session.BeginRebind(FindRow(session, "Increase Game Speed"), false);
                    InputSystem.QueueStateEvent(
                        keyboard,
                        new KeyboardState(modifier, Key.RightBracket)
                    );
                    InputSystem.Update();
                    fixture.currentTime += 0.01;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();

                    FinishCapture(fixture);

                    InputAction action = manager.Asset.FindAction(
                        "Strategy/IncreaseGameSpeed",
                        true
                    );
                    Assert.AreEqual(
                        expectedPath + "+<Keyboard>/rightBracket",
                        OptionsBindingSession.GetBindingSignature(
                            action,
                            FindBinding(action, "PrimaryChord")
                        )
                    );
                }
            );
        }

        [Test]
        public void BeginRebind_PressAndReleaseInOneUpdate_PreservesCapturedChord()
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    session.Rebuild();
                    session.BeginRebind(FindRow(session, "Increase Game Speed"), false);
                    InputSystem.QueueStateEvent(
                        keyboard,
                        new KeyboardState(Key.LeftShift, Key.RightBracket)
                    );
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();

                    FinishCapture(fixture);

                    InputAction action = manager.Asset.FindAction(
                        "Strategy/IncreaseGameSpeed",
                        true
                    );
                    Assert.AreEqual(
                        "<Keyboard>/shift+<Keyboard>/rightBracket",
                        OptionsBindingSession.GetBindingSignature(
                            action,
                            FindBinding(action, "PrimaryChord")
                        )
                    );
                }
            );
        }

        [Test]
        public void BeginRebind_ModifierPressedAfterKey_KeepsPlainBinding()
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    session.Rebuild();
                    session.BeginRebind(FindRow(session, "Increase Game Speed"), false);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightBracket));
                    InputSystem.Update();
                    fixture.currentTime += 0.01;
                    InputSystem.QueueStateEvent(
                        keyboard,
                        new KeyboardState(Key.RightBracket, Key.LeftShift)
                    );
                    InputSystem.Update();

                    FinishCapture(fixture);

                    InputAction action = manager.Asset.FindAction(
                        "Strategy/IncreaseGameSpeed",
                        true
                    );
                    Assert.AreEqual(
                        "<Keyboard>/rightBracket",
                        action.bindings[FindBinding(action, "Primary")].effectivePath
                    );
                }
            );
        }

        [Test]
        public void BeginRebind_SecondCandidateWithoutModifier_KeepsFirstCandidateChord()
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    session.Rebuild();
                    session.BeginRebind(FindRow(session, "Increase Game Speed"), false);
                    InputSystem.QueueStateEvent(
                        keyboard,
                        new KeyboardState(Key.LeftShift, Key.RightBracket)
                    );
                    InputSystem.Update();
                    fixture.currentTime += 0.01;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftBracket));
                    InputSystem.Update();

                    FinishCapture(fixture);

                    InputAction action = manager.Asset.FindAction(
                        "Strategy/IncreaseGameSpeed",
                        true
                    );
                    Assert.AreEqual(
                        "<Keyboard>/shift+<Keyboard>/rightBracket",
                        OptionsBindingSession.GetBindingSignature(
                            action,
                            FindBinding(action, "PrimaryChord")
                        )
                    );
                }
            );
        }

        [Test]
        public void BeginRebind_DeltaMouseEvent_UsesHeldModifier()
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    Mouse mouse = InputSystem.AddDevice<Mouse>();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                    InputSystem.Update();
                    session.Rebuild();
                    session.BeginRebind(FindRow(session, "Increase Game Speed"), true);
                    InputSystem.QueueDeltaStateEvent(mouse.scroll, Vector2.up);
                    InputSystem.Update();
                    fixture.currentTime += 0.01;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();

                    FinishCapture(fixture);

                    InputAction action = manager.Asset.FindAction(
                        "Strategy/IncreaseGameSpeed",
                        true
                    );
                    Assert.AreEqual(
                        "<Keyboard>/shift+<Mouse>/scroll/up",
                        OptionsBindingSession.GetBindingSignature(
                            action,
                            FindBinding(action, "SecondaryChord")
                        )
                    );
                }
            );
        }

        [Test]
        public void BeginRebind_ModifiedMouseWheel_PreservesReleasedModifier()
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    Mouse mouse = InputSystem.AddDevice<Mouse>();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                    InputSystem.Update();
                    session.Rebuild();
                    session.BeginRebind(FindRow(session, "Increase Game Speed"), true);
                    InputSystem.QueueStateEvent(mouse, new MouseState { scroll = Vector2.up });
                    InputSystem.Update();
                    fixture.currentTime += 0.01;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();

                    FinishCapture(fixture);

                    InputAction action = manager.Asset.FindAction(
                        "Strategy/IncreaseGameSpeed",
                        true
                    );
                    Assert.AreEqual(
                        "<Keyboard>/shift+<Mouse>/scroll/up",
                        OptionsBindingSession.GetBindingSignature(
                            action,
                            FindBinding(action, "SecondaryChord")
                        )
                    );
                }
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ResolveConflict_DuplicateBinding_AllowsSubsequentRebind(bool accept)
        {
            WithKeyboard(
                (session, manager, keyboard, fixture) =>
                {
                    session.Rebuild();
                    int row = FindRow(session, "Increase Game Speed");
                    InputAction action = manager.Asset.FindAction(
                        "Strategy/IncreaseGameSpeed",
                        true
                    );
                    InputAction oldAction = manager.Asset.FindAction("Strategy/ShowTroopers", true);
                    oldAction.ApplyBindingOverride(
                        FindBinding(oldAction, "Primary"),
                        "<Keyboard>/t"
                    );
                    string oldPath = oldAction
                        .bindings[FindBinding(oldAction, "Primary")]
                        .effectivePath;
                    int presentationChanges = 0;
                    session.PresentationChanged += () => presentationChanges++;
                    session.BeginRebind(row, false);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T));
                    InputSystem.Update();
                    FinishCapture(fixture);

                    Assert.IsTrue(session.HasPendingConflict);
                    Assert.AreEqual(-1, session.ListeningRow);
                    Assert.AreEqual(
                        2,
                        presentationChanges,
                        "Capture completion must refresh the listening indicator before the conflict is resolved."
                    );

                    session.ResolveConflict(accept);

                    Assert.IsFalse(session.HasPendingConflict);
                    if (accept)
                    {
                        Assert.AreEqual(
                            oldPath,
                            action.bindings[FindBinding(action, "Primary")].effectivePath
                        );
                        Assert.AreEqual(
                            string.Empty,
                            oldAction.bindings[FindBinding(oldAction, "Primary")].effectivePath
                        );
                    }
                    else
                        Assert.AreEqual(
                            oldPath,
                            oldAction.bindings[FindBinding(oldAction, "Primary")].effectivePath
                        );
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                    session.BeginRebind(row, true);
                    Assert.AreEqual(row, session.ListeningRow);
                    session.CancelRebind();
                }
            );
        }

        [Test]
        public void CompositeSignatures_Default_UsePlatformModifierAndAuthoredBindingParts()
        {
            InputAction decrease = _inputManager.Asset.FindAction(
                "Strategy/DecreaseGameSpeed",
                true
            );
            InputAction increase = _inputManager.Asset.FindAction(
                "Strategy/IncreaseGameSpeed",
                true
            );

            string decreaseSignature = OptionsBindingSession.GetBindingSignature(
                decrease,
                FindBinding(decrease, "PrimaryChord")
            );
            string increaseSignature = OptionsBindingSession.GetBindingSignature(
                increase,
                FindBinding(increase, "PrimaryChord")
            );

            StringAssert.Contains(GetPlatformModifierPath(), decreaseSignature);
            StringAssert.Contains("<Keyboard>/minus", decreaseSignature);
            Assert.AreNotEqual(decreaseSignature, increaseSignature);
        }

        [Test]
        public void Rebuild_GameSpeedBindings_ShowsDirectionalMouseWheelDefaults()
        {
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);

            session.Rebuild();

            OptionsBindingRow decrease = session.Rows.Single(row =>
                row.Action == "Decrease Game Speed"
            );
            OptionsBindingRow increase = session.Rows.Single(row =>
                row.Action == "Increase Game Speed"
            );
            string modifier = Application.platform == RuntimePlatform.OSXEditor ? "CMD" : "CTRL";
            Assert.AreEqual($"{modifier}+WHEEL DOWN", decrease.Secondary);
            Assert.AreEqual($"{modifier}+WHEEL UP", increase.Secondary);
        }

        [Test]
        public void Rebuild_CloseAllWindowsBinding_ShowsPlatformShortcut()
        {
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);

            session.Rebuild();

            OptionsBindingRow row = session.Rows.Single(candidate =>
                candidate.Action == "Close All Windows"
            );
            string modifier = Application.platform == RuntimePlatform.OSXEditor ? "CMD" : "CTRL";
            Assert.AreEqual($"{modifier}+W", row.Primary);
        }

        [Test]
        public void CompositeSignatures_EquivalentChordOverrides_Match()
        {
            InputAction expected = _inputManager.Asset.FindAction(
                "Strategy/DecreaseGameSpeed",
                true
            );
            InputAction rebound = _inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            int expectedChord = FindBinding(expected, "PrimaryChord");
            int reboundChord = FindBinding(rebound, "PrimaryChord");
            rebound.ApplyBindingOverride(
                FindPart(rebound, reboundChord, "Modifier"),
                GetPlatformModifierPath()
            );
            rebound.ApplyBindingOverride(
                FindPart(rebound, reboundChord, "Binding"),
                "<Keyboard>/minus"
            );

            Assert.AreEqual(
                OptionsBindingSession.GetBindingSignature(expected, expectedChord),
                OptionsBindingSession.GetBindingSignature(rebound, reboundChord)
            );
        }

        [Test]
        public void BindingSignatures_PlainAndModifiedKeys_DistinguishSignatures()
        {
            InputAction plain = _inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            InputAction chord = _inputManager.Asset.FindAction(
                "Strategy/ShowFighterSquadrons",
                true
            );
            int plainIndex = FindBinding(plain, "Primary");
            int chordIndex = FindBinding(chord, "PrimaryChord");
            plain.ApplyBindingOverride(plainIndex, "<Keyboard>/b");
            chord.ApplyBindingOverride(FindPart(chord, chordIndex, "Modifier"), "<Keyboard>/ctrl");
            chord.ApplyBindingOverride(FindPart(chord, chordIndex, "Binding"), "<Keyboard>/b");

            Assert.AreNotEqual(
                OptionsBindingSession.GetBindingSignature(plain, plainIndex),
                OptionsBindingSession.GetBindingSignature(chord, chordIndex)
            );
        }

        [Test]
        public void ProjectInputSettings_ShortcutConsumption_IsEnabled()
        {
            Assert.IsTrue(InputSystem.settings.shortcutKeysConsumeInput);
        }

        [Test]
        public void Rebuild_GlobalBindings_IncludesOpenGameMenu()
        {
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);
            InputAction openGameMenu = _inputManager.Asset.FindAction("Global/OpenGameMenu", true);

            session.Rebuild();

            OptionsBindingRow row = session.Rows.Single(row => row.Action == "Open Game Menu");
            Assert.IsFalse(row.IsHeader);
            Assert.AreEqual("SHIFT+ESC", row.Primary);
            Assert.IsFalse(row.PrimaryEditable);
            int chord = FindBinding(openGameMenu, "PrimaryChord");
            Assert.AreEqual("<Keyboard>/shift", openGameMenu.bindings[chord + 1].effectivePath);
            Assert.AreEqual("<Keyboard>/escape", openGameMenu.bindings[chord + 2].effectivePath);
        }

        [Test]
        public void Rebuild_SelectionModifiers_ExposeToggleAndRangeOnly()
        {
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);
            session.Rebuild();

            string[] labels = session.Rows.Select(row => row.Action).ToArray();

            CollectionAssert.Contains(labels, "Toggle Selection Modifier");
            CollectionAssert.Contains(labels, "Range Selection Modifier");
            CollectionAssert.DoesNotContain(labels, "Alternate Select Modifier");
        }

        [Test]
        public void BeginRebind_OpenGameMenuPrimary_DoesNotStartCapture()
        {
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);
            session.Rebuild();
            int row = session
                .Rows.Select((binding, index) => (binding, index))
                .Single(item => item.binding.Action == "Open Game Menu")
                .index;

            session.BeginRebind(row, false);

            Assert.AreEqual(-1, session.ListeningRow);
        }

        [Test]
        public void BeginRebind_UnboundSecondarySlot_StartsInteractiveCapture()
        {
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);
            session.Rebuild();
            int row = session
                .Rows.Select((binding, index) => (binding, index))
                .First(item => item.binding.Action == "Show Troopers")
                .index;

            Assert.AreEqual("UNBOUND", session.Rows[row].Secondary);
            Assert.DoesNotThrow(() => session.BeginRebind(row, true));

            Assert.AreEqual(row, session.ListeningRow);
            Assert.IsTrue(session.ListeningSecondary);
            session.CancelRebind();
            Assert.AreEqual(-1, session.ListeningRow);
        }

        [Test]
        public void BeginRebind_MouseWheelUp_BindsDirectionalWheelControl()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject inputRoot = new("IsolatedInputManager");
            InputManager inputManager = inputRoot.AddComponent<InputManager>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();

            try
            {
                using OptionsBindingSession session = new OptionsBindingSession(inputManager);
                session.Rebuild();
                int row = session
                    .Rows.Select((binding, index) => (binding, index))
                    .Single(item => item.binding.Action == "Show Troopers")
                    .index;
                session.BeginRebind(row, false);
                InputSystem.Update();
                InputSystem.QueueStateEvent(mouse, new MouseState { scroll = Vector2.up });
                InputSystem.Update();
                inputFixture.currentTime += 0.1;
                InputSystem.Update();

                InputAction action = inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
                Assert.AreEqual(-1, session.ListeningRow);
                Assert.AreEqual(
                    "<Mouse>/scroll/up",
                    action.bindings[FindBinding(action, "Primary")].effectivePath
                );
                Assert.AreEqual("WHEEL UP", session.Rows[row].Primary);
            }
            finally
            {
                Object.DestroyImmediate(inputRoot);
                inputFixture.TearDown();
            }
        }

        [Test]
        public void BeginRebind_MouseMovementThenBackButton_BindsMouseButton()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject inputRoot = new("IsolatedInputManager");
            InputManager inputManager = inputRoot.AddComponent<InputManager>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();

            try
            {
                using OptionsBindingSession session = new OptionsBindingSession(inputManager);
                session.Rebuild();
                int row = session
                    .Rows.Select((binding, index) => (binding, index))
                    .First(item => item.binding.Action == "Show Troopers")
                    .index;
                session.BeginRebind(row, false);

                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { position = new Vector2(100f, 100f), delta = Vector2.one }
                );
                InputSystem.Update();

                Assert.AreEqual(row, session.ListeningRow);

                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Back));
                InputSystem.Update();
                inputFixture.currentTime += 0.1;
                InputSystem.Update();

                InputAction action = inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
                Assert.AreEqual(-1, session.ListeningRow);
                Assert.AreEqual(
                    "<Mouse>/backButton",
                    action.bindings[FindBinding(action, "Primary")].effectivePath
                );
                Assert.AreEqual("BACK", session.Rows[row].Primary);
            }
            finally
            {
                Object.DestroyImmediate(inputRoot);
                inputFixture.TearDown();
            }
        }

        [TestCase(MouseButton.Left)]
        [TestCase(MouseButton.Right)]
        public void BeginRebind_ReservedMouseButton_KeepsCaptureActive(MouseButton button)
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject inputRoot = new("IsolatedInputManager");
            InputManager inputManager = inputRoot.AddComponent<InputManager>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();

            try
            {
                using OptionsBindingSession session = new OptionsBindingSession(inputManager);
                session.Rebuild();
                int row = session
                    .Rows.Select((binding, index) => (binding, index))
                    .First(item => item.binding.Action == "Show Troopers")
                    .index;
                InputAction action = inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
                string originalPath = action.bindings[FindBinding(action, "Primary")].effectivePath;
                session.BeginRebind(row, false);

                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(button));
                InputSystem.Update();
                inputFixture.currentTime += 0.1;
                InputSystem.Update();

                Assert.AreEqual(row, session.ListeningRow);
                Assert.AreEqual(
                    originalPath,
                    action.bindings[FindBinding(action, "Primary")].effectivePath
                );
                session.CancelRebind();
            }
            finally
            {
                Object.DestroyImmediate(inputRoot);
                inputFixture.TearDown();
            }
        }

        [Test]
        public void BeginRebind_Escape_CancelsCaptureBeforeSubsequentApplicationCancellation()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject inputRoot = new("IsolatedInputManager");
            InputManager inputManager = inputRoot.AddComponent<InputManager>();
            AppInputController controller = inputRoot.AddComponent<AppInputController>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            InputAction cancel = inputManager.Asset.FindAction("Global/CancelOrSettings", true);
            CountingCancelable cancelable = new();
            CancelStack cancelStack = new();
            cancelStack.Register(cancelable);
            controller.Initialize(inputManager, cancelStack, null);

            try
            {
                using OptionsBindingSession session = new OptionsBindingSession(inputManager);
                session.Rebuild();
                int row = session
                    .Rows.Select((binding, index) => (binding, index))
                    .First(item => item.binding.Action == "Show Troopers")
                    .index;
                session.BeginRebind(row, false);

                Assert.IsTrue(cancel.enabled);
                Assert.IsTrue(inputManager.AreApplicationShortcutsSuppressed);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();

                Assert.AreEqual(-1, session.ListeningRow);
                Assert.AreEqual(0, cancelable.CancelCount);
                Assert.IsTrue(cancel.enabled);
                Assert.IsFalse(inputManager.AreApplicationShortcutsSuppressed);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();

                Assert.AreEqual(1, cancelable.CancelCount);
                Assert.IsTrue(cancel.enabled);
            }
            finally
            {
                Object.DestroyImmediate(inputRoot);
                inputFixture.TearDown();
            }
        }

        [Test]
        public void SetTextEntryActive_Inactive_ReleasesSuppressionWithoutDisablingActions()
        {
            InputAction cancel = _inputManager.Asset.FindAction("Global/CancelOrSettings", true);
            cancel.actionMap.Enable();

            try
            {
                using OptionsBindingSession session = new OptionsBindingSession(_inputManager);

                session.SetTextEntryActive(true);

                Assert.IsTrue(cancel.enabled);
                Assert.IsTrue(_inputManager.AreApplicationShortcutsSuppressed);

                session.SetTextEntryActive(false);

                Assert.IsTrue(cancel.enabled);
                Assert.IsFalse(_inputManager.AreApplicationShortcutsSuppressed);
            }
            finally
            {
                cancel.actionMap.Disable();
            }
        }

        [Test]
        public void RestoreDefault_OneBinding_RestoresOnlySelectedAction()
        {
            InputAction troopers = _inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            InputAction fighters = _inputManager.Asset.FindAction(
                "Strategy/ShowFighterSquadrons",
                true
            );
            troopers.ApplyBindingOverride(FindBinding(troopers, "Primary"), "<Keyboard>/n");
            fighters.ApplyBindingOverride(FindBinding(fighters, "Primary"), "<Keyboard>/m");
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);
            session.Rebuild();
            int row = session
                .Rows.Select((binding, index) => (binding, index))
                .Single(item => item.binding.Action == "Show Troopers")
                .index;

            session.RestoreDefault(row);

            Assert.IsNull(troopers.bindings[FindBinding(troopers, "Primary")].overridePath);
            Assert.AreEqual(
                "<Keyboard>/m",
                fighters.bindings[FindBinding(fighters, "Primary")].overridePath
            );
        }

        [Test]
        public void RestoreAllDefaults_MultipleBindings_RemovesEveryOverride()
        {
            InputAction troopers = _inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            InputAction quickSave = _inputManager.Asset.FindAction("Global/QuickSave", true);
            troopers.ApplyBindingOverride(FindBinding(troopers, "Primary"), "<Keyboard>/n");
            quickSave.ApplyBindingOverride(FindBinding(quickSave, "Primary"), "<Keyboard>/m");
            using OptionsBindingSession session = new OptionsBindingSession(_inputManager);
            session.Rebuild();

            session.RestoreAllDefaults();

            Assert.IsFalse(
                _inputManager
                    .Asset.actionMaps.SelectMany(map => map.actions)
                    .SelectMany(action => action.bindings)
                    .Any(binding => binding.hasOverrides)
            );
        }

        [Test]
        public void AuthoredChord_ModifierHeld_ConsumesPlainBaseKeyAction()
        {
            InputTestFixture inputFixture = new();
            inputFixture.Setup();
            GameObject inputRoot = new("IsolatedInputManager");
            InputManager inputManager = inputRoot.AddComponent<InputManager>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            InputAction chord = inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            InputAction plain = inputManager.Asset.FindAction(
                "Strategy/ShowFighterSquadrons",
                true
            );
            InputActionMap strategy = chord.actionMap;
            int chordComposite = FindBinding(chord, "PrimaryChord");
            chord.ApplyBindingOverride(FindBinding(chord, "Primary"), string.Empty);
            chord.ApplyBindingOverride(
                FindPart(chord, chordComposite, "Modifier"),
                "<Keyboard>/ctrl"
            );
            chord.ApplyBindingOverride(FindPart(chord, chordComposite, "Binding"), "<Keyboard>/b");
            plain.ApplyBindingOverride(FindBinding(plain, "Primary"), "<Keyboard>/b");
            int chordCount = 0;
            int plainCount = 0;
            chord.performed += _ => chordCount++;
            plain.performed += _ => plainCount++;

            try
            {
                InputSystem.settings.shortcutKeysConsumeInput = true;
                strategy.Enable();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.B));
                InputSystem.Update();

                Assert.AreEqual(1, chordCount);
                Assert.AreEqual(0, plainCount);
            }
            finally
            {
                strategy.Disable();
                Object.DestroyImmediate(inputRoot);
                inputFixture.TearDown();
            }
        }

        /// <summary>
        /// Runs one capture scenario with an isolated keyboard and input asset.
        /// </summary>
        /// <param name="exercise">The scenario to exercise.</param>
        private static void WithKeyboard(
            Action<OptionsBindingSession, InputManager, Keyboard, InputTestFixture> exercise
        )
        {
            InputTestFixture fixture = new();
            fixture.Setup();
            GameObject root = new("IsolatedInputManager");
            InputManager manager = root.AddComponent<InputManager>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                using OptionsBindingSession session = new(manager);
                exercise(session, manager, keyboard, fixture);
            }
            finally
            {
                Object.DestroyImmediate(root);
                fixture.TearDown();
            }
        }

        /// <summary>
        /// Advances past Unity's interactive-rebind candidate settling interval.
        /// </summary>
        /// <param name="fixture">The isolated input clock.</param>
        private static void FinishCapture(InputTestFixture fixture)
        {
            fixture.currentTime += 0.1;
            InputSystem.Update();
        }

        /// <summary>
        /// Finds one visible binding row by its action caption.
        /// </summary>
        /// <param name="session">The binding session.</param>
        /// <param name="caption">The action caption.</param>
        /// <returns>The matching row index.</returns>
        private static int FindRow(OptionsBindingSession session, string caption)
        {
            return session
                .Rows.Select((row, index) => (row, index))
                .Single(item => item.row.Action == caption)
                .index;
        }

        /// <summary>
        /// Expects the game's logger to record a failed shortcut edit.
        /// </summary>
        private static void ExpectRebindFailure()
        {
            LogAssert.Expect(LogType.Error, new Regex("Shortcut rebinding failed:"));
        }

        private sealed class CountingCancelable : ICancelable
        {
            public int CancelCount { get; private set; }

            /// <summary>
            /// Records one application cancellation.
            /// </summary>
            /// <returns>True because the test cancellation is handled.</returns>
            public bool TryCancel()
            {
                CancelCount++;
                return true;
            }
        }

        /// <summary>
        /// Returns the default desktop shortcut modifier for the current test platform.
        /// </summary>
        /// <returns>The native keyboard modifier path.</returns>
        private static string GetPlatformModifierPath()
        {
            return Application.platform == RuntimePlatform.OSXEditor
                ? "<Keyboard>/leftMeta"
                : "<Keyboard>/ctrl";
        }

        /// <summary>
        /// Finds a top-level authored binding by name.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="name">The name.</param>
        /// <returns>The matching binding.</returns>
        private static int FindBinding(InputAction action, string name)
        {
            for (int index = 0; index < action.bindings.Count; index++)
            {
                InputBinding binding = action.bindings[index];
                if (!binding.isPartOfComposite && binding.name == name)
                    return index;
            }
            Assert.Fail($"Binding '{name}' was not found on {action}.");
            return -1;
        }

        /// <summary>
        /// Finds a named part belonging to one authored composite.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="compositeIndex">The composite index.</param>
        /// <param name="name">The name.</param>
        /// <returns>The matching part.</returns>
        private static int FindPart(InputAction action, int compositeIndex, string name)
        {
            for (int index = compositeIndex + 1; index < action.bindings.Count; index++)
            {
                InputBinding binding = action.bindings[index];
                if (!binding.isPartOfComposite)
                    break;
                if (binding.name == name)
                    return index;
            }
            Assert.Fail($"Composite part '{name}' was not found on {action}.");
            return -1;
        }
    }
}
