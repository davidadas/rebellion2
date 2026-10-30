using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Retains reversible text edits while an author is editing a shared input control.
/// </summary>
[RequireComponent(typeof(MenuInputField))]
public sealed class TextInputHistory : MonoBehaviour
{
    private readonly Stack<State> undo = new Stack<State>();
    private readonly Stack<State> redo = new Stack<State>();
    private MenuInputField input;
    private State current;
    private bool restoring;
    private bool tracking;
    private float lastChange;
    private int lastDelta;
    private bool lastWordEdit;
    private bool bound;
    private int insertionDepth;
    private State insertionStart;

    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;

    /// <summary>
    /// Observes text changes without committing incomplete values to the content model.
    /// </summary>
    private void Awake()
    {
        ClearHistory();
    }

    /// <summary>
    /// Connects once when a runtime or edit-mode view first binds this input.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;
        input = GetComponent<MenuInputField>();
        input.onSelect.AddListener(BeginEditing);
        input.onValueChanged.AddListener(Record);
        input.TextInsertionStarted += BeginInsertion;
        input.TextInsertionCompleted += CompleteInsertion;
        bound = true;
    }

    /// <summary>
    /// Detaches the input listeners when its owning view is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        input.onSelect.RemoveListener(BeginEditing);
        input.onValueChanged.RemoveListener(Record);
        input.TextInsertionStarted -= BeginInsertion;
        input.TextInsertionCompleted -= CompleteInsertion;
    }

    /// <summary>
    /// Resumes an uncommitted draft or starts a session for a newly bound value.
    /// </summary>
    /// <param name="text">The input's selected text value.</param>
    private void BeginEditing(string text)
    {
        if (current.Text != text)
            ClearHistory();
        current = Capture();
        lastDelta = 0;
        lastWordEdit = false;
        tracking = true;
    }

    /// <summary>
    /// Resets a pooled control after the form binds it to another authored value.
    /// </summary>
    public void ClearHistory()
    {
        BindControls();
        undo.Clear();
        redo.Clear();
        current = Capture();
        lastDelta = 0;
        lastWordEdit = false;
        tracking = input.isFocused;
        insertionDepth = 0;
    }

    /// <summary>
    /// Records paste, deletion, or a contiguous typing group as a reversible text change.
    /// </summary>
    /// <param name="text">The new input value.</param>
    private void Record(string text)
    {
        if (restoring || !tracking || insertionDepth > 0 || text == current.Text)
            return;
        int delta = text.Length - current.Text.Length;
        bool wordEdit = IsWordEdit(current.Text, text);
        bool typing =
            Math.Abs(delta) == 1
            && wordEdit
            && lastWordEdit
            && delta == lastDelta
            && Time.unscaledTime - lastChange < 0.75f
            && current.Anchor == current.Focus
            && input.selectionStringFocusPosition == current.Focus + delta;
        if (!typing)
            undo.Push(current);
        redo.Clear();
        current = Capture();
        lastChange = Time.unscaledTime;
        lastDelta = delta;
        lastWordEdit = wordEdit;
    }

    /// <summary>
    /// Keeps spaces and line breaks as undo boundaries instead of merging an entire XML draft.
    /// </summary>
    /// <param name="before">The preceding text.</param>
    /// <param name="after">The edited text.</param>
    /// <returns>Whether this change inserts or deletes one non-whitespace character.</returns>
    private static bool IsWordEdit(string before, string after)
    {
        if (Math.Abs(after.Length - before.Length) != 1)
            return false;
        int index = 0;
        while (index < Math.Min(before.Length, after.Length) && before[index] == after[index])
            index++;
        string longer = before.Length > after.Length ? before : after;
        string shorter = before.Length > after.Length ? after : before;
        return !char.IsWhiteSpace(longer[index])
            && longer.AsSpan(index + 1).SequenceEqual(shorter.AsSpan(index));
    }

    /// <summary>
    /// Captures the selection before one complete native string insertion.
    /// </summary>
    private void BeginInsertion()
    {
        if (insertionDepth++ == 0)
            insertionStart = Capture();
    }

    /// <summary>
    /// Records a complete paste once, independently of its character count or frame timing.
    /// </summary>
    private void CompleteInsertion()
    {
        if (insertionDepth == 0 || --insertionDepth != 0 || !tracking || restoring)
            return;
        State after = Capture();
        if (after.Text == insertionStart.Text)
            return;
        undo.Push(insertionStart);
        redo.Clear();
        current = after;
        lastDelta = 0;
        lastWordEdit = false;
    }

    /// <summary>
    /// Restores the preceding text change without altering unrelated document edits.
    /// </summary>
    /// <returns>Whether a text change was undone.</returns>
    public bool Undo() => Restore(undo, redo);

    /// <summary>
    /// Reapplies the most recently undone text change.
    /// </summary>
    /// <returns>Whether a text change was reapplied.</returns>
    public bool Redo() => Restore(redo, undo);

    /// <summary>
    /// Moves one text state between stacks and notifies ordinary input consumers.
    /// </summary>
    /// <param name="source">The states to restore.</param>
    /// <param name="destination">The states available for the opposite operation.</param>
    /// <returns>Whether a state was restored.</returns>
    private bool Restore(Stack<State> source, Stack<State> destination)
    {
        if (source.Count == 0)
            return false;
        destination.Push(Capture());
        State state = source.Pop();
        restoring = true;
        try
        {
            input.SetTextWithoutNotify(state.Text);
            input.selectionStringAnchorPosition = Mathf.Clamp(state.Anchor, 0, state.Text.Length);
            input.selectionStringFocusPosition = Mathf.Clamp(state.Focus, 0, state.Text.Length);
            input.onValueChanged.Invoke(state.Text);
            current = state;
            lastDelta = 0;
            lastWordEdit = false;
        }
        finally
        {
            restoring = false;
        }
        return true;
    }

    /// <summary>
    /// Captures the text and selection of the bound input.
    /// </summary>
    /// <returns>The current editable text state.</returns>
    private State Capture() =>
        new State(
            input.text,
            input.selectionStringAnchorPosition,
            input.selectionStringFocusPosition
        );

    /// <summary>
    /// Holds the text and selection restored by one history operation.
    /// </summary>
    private readonly struct State
    {
        internal string Text { get; }
        internal int Anchor { get; }
        internal int Focus { get; }

        /// <summary>
        /// Captures one input value and its selection.
        /// </summary>
        /// <param name="text">The input text.</param>
        /// <param name="anchor">The selection anchor.</param>
        /// <param name="focus">The selection focus.</param>
        internal State(string text, int anchor, int focus)
        {
            Text = text;
            Anchor = anchor;
            Focus = focus;
        }
    }
}
