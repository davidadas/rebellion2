using System;
using System.Runtime.InteropServices;
using AOT;

/// <summary>
/// Reads macOS trackpad pinch gestures from the content editor window's AppKit view.
/// </summary>
internal sealed class ContentEditorMacMagnification
{
    private const string _objectiveC = "/usr/lib/libobjc.A.dylib";
    private const string _receiverName = "RebellionContentEditorMagnificationReceiver";
    private const int _began = 1;
    private const int _ended = 3;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GestureAction(IntPtr receiver, IntPtr action, IntPtr gesture);

    private static readonly GestureAction _gestureAction = ReceiveMagnification;
    private static ContentEditorMacMagnification _attached;

    private IntPtr recognizer;
    private IntPtr receiver;
    private double previousScale = 1d;
    private double pendingScale = 1d;

    /// <summary>
    /// Attaches the recognizer on demand and consumes any scale changes it has received.
    /// </summary>
    /// <param name="factor">The combined magnification since the last read.</param>
    /// <returns>Whether a pinch changed the scale.</returns>
    internal bool TryRead(out float factor)
    {
        if (recognizer == IntPtr.Zero && _attached == null)
            Attach();
        factor = (float)pendingScale;
        pendingScale = 1d;
        return factor != 1f;
    }

    /// <summary>
    /// Removes the recognizer when the pointer leaves the canvas or its preview closes.
    /// </summary>
    internal void Detach()
    {
        if (recognizer == IntPtr.Zero)
            return;
        _attached = null;
        Invoke(Get(recognizer, Selector("view")), Selector("removeGestureRecognizer:"), recognizer);
        Invoke(recognizer, Selector("release"));
        Invoke(receiver, Selector("release"));
        recognizer = receiver = IntPtr.Zero;
        previousScale = pendingScale = 1d;
    }

    /// <summary>
    /// Installs a magnification recognizer on the focused editor window's content view.
    /// </summary>
    private void Attach()
    {
        IntPtr application = Get(GetClass("NSApplication"), Selector("sharedApplication"));
        IntPtr window = Get(application, Selector("keyWindow"));
        IntPtr view = Get(window, Selector("contentView"));
        if (view == IntPtr.Zero)
            return;
        IntPtr action = Selector("strategyPreviewMagnified:");
        IntPtr receiverClass = GetClass(_receiverName);
        if (receiverClass == IntPtr.Zero)
        {
            receiverClass = AllocateClass(GetClass("NSObject"), _receiverName, UIntPtr.Zero);
            if (receiverClass == IntPtr.Zero)
                return;
            if (!AddMethod(receiverClass, action, _gestureAction, "v@:@"))
            {
                DisposeClass(receiverClass);
                return;
            }
            RegisterClass(receiverClass);
        }

        receiver = Get(receiverClass, Selector("new"));
        recognizer = Initialize(
            Get(GetClass("NSMagnificationGestureRecognizer"), Selector("alloc")),
            Selector("initWithTarget:action:"),
            receiver,
            action
        );
        if (recognizer == IntPtr.Zero)
        {
            Invoke(receiver, Selector("release"));
            receiver = IntPtr.Zero;
            return;
        }
        _attached = this;
        Invoke(view, Selector("addGestureRecognizer:"), recognizer);
    }

    /// <summary>
    /// Converts AppKit's cumulative magnification into frame-independent scale changes.
    /// </summary>
    /// <param name="receiver">The Objective-C target registered for the action.</param>
    /// <param name="action">The invoked selector.</param>
    /// <param name="gesture">The AppKit recognizer reporting the gesture.</param>
    [MonoPInvokeCallback(typeof(GestureAction))]
    private static void ReceiveMagnification(IntPtr receiver, IntPtr action, IntPtr gesture)
    {
        ContentEditorMacMagnification owner = _attached;
        if (owner == null || owner.recognizer != gesture)
            return;
        int state = Get(gesture, Selector("state")).ToInt32();
        if (state is < _began or > _ended)
        {
            owner.previousScale = 1d;
            return;
        }
        if (state == _began)
            owner.previousScale = 1d;
        double scale = 1d + GetNumber(gesture, Selector("magnification"));
        if (scale > 0d && double.IsFinite(scale))
        {
            owner.pendingScale *= scale / owner.previousScale;
            owner.previousScale = scale;
        }
        if (state == _ended)
            owner.previousScale = 1d;
    }

    /// <summary>
    /// Looks up a registered Objective-C class by name.
    /// </summary>
    /// <param name="name">The class name.</param>
    /// <returns>The class handle or zero.</returns>
    [DllImport(_objectiveC, EntryPoint = "objc_getClass")]
    private static extern IntPtr GetClass(string name);

    /// <summary>
    /// Looks up or registers an Objective-C message name.
    /// </summary>
    /// <param name="name">The message name.</param>
    /// <returns>The selector handle.</returns>
    [DllImport(_objectiveC, EntryPoint = "sel_registerName")]
    private static extern IntPtr Selector(string name);

    /// <summary>
    /// Allocates the receiver class before it is registered.
    /// </summary>
    /// <param name="superclass">The class to inherit from.</param>
    /// <param name="name">The class name.</param>
    /// <param name="extraBytes">Additional class storage required.</param>
    /// <returns>The allocated class handle or zero.</returns>
    [DllImport(_objectiveC, EntryPoint = "objc_allocateClassPair")]
    private static extern IntPtr AllocateClass(IntPtr superclass, string name, UIntPtr extraBytes);

    /// <summary>
    /// Associates the gesture callback with the receiver class.
    /// </summary>
    /// <param name="target">The class receiving the instance method.</param>
    /// <param name="action">The action selector.</param>
    /// <param name="callback">The action's managed implementation.</param>
    /// <param name="types">The Objective-C return and parameter type encoding.</param>
    /// <returns>Whether the method was added.</returns>
    [DllImport(_objectiveC, EntryPoint = "class_addMethod")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AddMethod(
        IntPtr target,
        IntPtr action,
        GestureAction callback,
        string types
    );

    /// <summary>
    /// Makes an allocated class available to the Objective-C runtime.
    /// </summary>
    /// <param name="target">The new class.</param>
    [DllImport(_objectiveC, EntryPoint = "objc_registerClassPair")]
    private static extern void RegisterClass(IntPtr target);

    /// <summary>
    /// Discards a class whose registration could not be completed.
    /// </summary>
    /// <param name="target">The unregistered class.</param>
    [DllImport(_objectiveC, EntryPoint = "objc_disposeClassPair")]
    private static extern void DisposeClass(IntPtr target);

    /// <summary>
    /// Reads an object or pointer-sized property.
    /// </summary>
    /// <param name="target">The object or class receiving the message.</param>
    /// <param name="message">The property or factory selector.</param>
    /// <returns>The returned object or pointer-sized value.</returns>
    [DllImport(_objectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Get(IntPtr target, IntPtr message);

    /// <summary>
    /// Reads a 64-bit AppKit floating-point property.
    /// </summary>
    /// <param name="target">The object receiving the message.</param>
    /// <param name="message">The CGFloat property selector.</param>
    /// <returns>The property value.</returns>
    [DllImport(_objectiveC, EntryPoint = "objc_msgSend")]
    private static extern double GetNumber(IntPtr target, IntPtr message);

    /// <summary>
    /// Initializes an allocated gesture recognizer.
    /// </summary>
    /// <param name="target">The allocated recognizer.</param>
    /// <param name="message">The initializer selector.</param>
    /// <param name="receiver">The callback receiver.</param>
    /// <param name="action">The callback selector.</param>
    /// <returns>The initialized recognizer or zero.</returns>
    [DllImport(_objectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Initialize(
        IntPtr target,
        IntPtr message,
        IntPtr receiver,
        IntPtr action
    );

    /// <summary>
    /// Sends a parameterless Objective-C command.
    /// </summary>
    /// <param name="target">The object receiving the message.</param>
    /// <param name="message">The command selector.</param>
    [DllImport(_objectiveC, EntryPoint = "objc_msgSend")]
    private static extern void Invoke(IntPtr target, IntPtr message);

    /// <summary>
    /// Sends an Objective-C command taking an object argument.
    /// </summary>
    /// <param name="target">The object receiving the message.</param>
    /// <param name="message">The command selector.</param>
    /// <param name="argument">The command argument.</param>
    [DllImport(_objectiveC, EntryPoint = "objc_msgSend")]
    private static extern void Invoke(IntPtr target, IntPtr message, IntPtr argument);
}
