using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
using static ContentEditorMacNative;

/// <summary>
/// Adapts Finder file drops on the editor's existing AppKit content view.
/// </summary>
internal sealed class ContentEditorMacFileDrops : ContentEditorFileDrops
{
    private const ulong _copy = 1;
    private static readonly DragOperation _operation = CopyOperation;
    private static readonly DragDecision _prepare = PrepareDrop;
    private static readonly DragDecision _perform = PerformDrop;
    private static ContentEditorMacFileDrops _current;
    private IntPtr view;
    private IntPtr baseClass;
    private IntPtr dropClass;
    private IntPtr registeredTypes;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong DragOperation(IntPtr receiver, IntPtr selector, IntPtr drag);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool DragDecision(IntPtr receiver, IntPtr selector, IntPtr drag);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        internal double X;
        internal double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Bounds
    {
        internal Point Origin;
        internal double Width;
        internal double Height;
    }

    /// <summary>
    /// Restores the content view and its previous registered drop types.
    /// </summary>
    public override void Dispose()
    {
        if (view == IntPtr.Zero)
            return;
        _current = null;
        if (ObjectClass(view) == dropClass)
        {
            Invoke(view, Selector("unregisterDraggedTypes"));
            SetObjectClass(view, baseClass);
            if (registeredTypes != IntPtr.Zero)
                Invoke(view, Selector("registerForDraggedTypes:"), registeredTypes);
        }
        Invoke(registeredTypes, Selector("release"));
        Invoke(view, Selector("release"));
        view = registeredTypes = IntPtr.Zero;
    }

    /// <summary>
    /// Registers the editor's content view as a destination for Finder file URLs.
    /// </summary>
    protected override void Poll()
    {
        if (view != IntPtr.Zero)
            return;
        IntPtr app = Get(GetClass("NSApplication"), Selector("sharedApplication"));
        IntPtr window = Get(app, Selector("mainWindow"));
        IntPtr content = Get(window, Selector("contentView"));
        if (content == IntPtr.Zero)
            return;
        baseClass = ObjectClass(content);
        dropClass = GetDropClass(baseClass);
        if (dropClass == IntPtr.Zero)
            return;
        view = Get(content, Selector("retain"));
        registeredTypes = Get(Get(view, Selector("registeredDraggedTypes")), Selector("retain"));
        IntPtr fileType = StringFromUtf8(
            GetClass("NSString"),
            Selector("stringWithUTF8String:"),
            "public.file-url"
        );
        IntPtr updated =
            registeredTypes == IntPtr.Zero
                ? SendObject(GetClass("NSArray"), Selector("arrayWithObject:"), fileType)
                : SendObject(registeredTypes, Selector("arrayByAddingObject:"), fileType);
        _current = this;
        SetObjectClass(view, dropClass);
        Invoke(view, Selector("registerForDraggedTypes:"), updated);
    }

    /// <summary>
    /// Adds drag destination methods without replacing Unity's other content view methods.
    /// </summary>
    /// <param name="parent">The original native content view class.</param>
    /// <returns>The registered subclass or zero if it could not be registered.</returns>
    private static IntPtr GetDropClass(IntPtr parent)
    {
        string name = $"RebellionEditorFileDrops_{parent.ToInt64():x}";
        IntPtr type = GetClass(name);
        if (type != IntPtr.Zero)
            return type;
        type = AllocateClass(parent, name, UIntPtr.Zero);
        if (type == IntPtr.Zero)
            return type;
        string boolean =
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "B@:@" : "c@:@";
        IntPtr operation = Marshal.GetFunctionPointerForDelegate(_operation);
        if (
            !AddMethod(type, Selector("draggingEntered:"), operation, "Q@:@")
            || !AddMethod(type, Selector("draggingUpdated:"), operation, "Q@:@")
            || !AddMethod(
                type,
                Selector("prepareForDragOperation:"),
                Marshal.GetFunctionPointerForDelegate(_prepare),
                boolean
            )
            || !AddMethod(
                type,
                Selector("performDragOperation:"),
                Marshal.GetFunctionPointerForDelegate(_perform),
                boolean
            )
        )
        {
            DisposeClass(type);
            return IntPtr.Zero;
        }
        RegisterClass(type);
        return type;
    }

    /// <summary>
    /// Advertises copying files into the working pack.
    /// </summary>
    /// <param name="receiver">The destination view.</param>
    /// <param name="selector">The AppKit action selector.</param>
    /// <param name="drag">The current AppKit drag.</param>
    /// <returns>The supported copy operation, when the source permits it.</returns>
    [MonoPInvokeCallback(typeof(DragOperation))]
    private static ulong CopyOperation(IntPtr receiver, IntPtr selector, IntPtr drag) =>
        (ulong)Get(drag, Selector("draggingSourceOperationMask")).ToInt64() & _copy;

    /// <summary>
    /// Lets AppKit finish a drag for which copying is available.
    /// </summary>
    /// <param name="receiver">The destination view.</param>
    /// <param name="selector">The AppKit action selector.</param>
    /// <param name="drag">The current AppKit drag.</param>
    /// <returns>Whether the destination can receive the drop.</returns>
    [MonoPInvokeCallback(typeof(DragDecision))]
    private static bool PrepareDrop(IntPtr receiver, IntPtr selector, IntPtr drag) =>
        CopyOperation(receiver, selector, drag) != 0;

    /// <summary>
    /// Reads local file URLs and queues the drop at its location in the content view.
    /// </summary>
    /// <param name="receiver">The destination view.</param>
    /// <param name="selector">The AppKit action selector.</param>
    /// <param name="drag">The completed AppKit drag.</param>
    /// <returns>Whether file URLs were queued.</returns>
    [MonoPInvokeCallback(typeof(DragDecision))]
    private static bool PerformDrop(IntPtr receiver, IntPtr selector, IntPtr drag)
    {
        if (_current == null || _current.view != receiver)
            return false;
        IntPtr pasteboard = Get(drag, Selector("draggingPasteboard"));
        IntPtr classes = SendObject(
            GetClass("NSArray"),
            Selector("arrayWithObject:"),
            GetClass("NSURL")
        );
        IntPtr urls = ReadObjects(
            pasteboard,
            Selector("readObjectsForClasses:options:"),
            classes,
            IntPtr.Zero
        );
        long count = Get(urls, Selector("count")).ToInt64();
        List<string> files = new();
        for (long index = 0; index < count; index++)
        {
            IntPtr url = SendObject(urls, Selector("objectAtIndex:"), new IntPtr(index));
            if (!GetBoolean(url, Selector("isFileURL")))
                continue;
            IntPtr path = Get(Get(url, Selector("path")), Selector("UTF8String"));
            if (path != IntPtr.Zero)
                files.Add(Marshal.PtrToStringUTF8(path));
        }
        if (files.Count == 0)
            return false;
        Point location = GetPoint(drag, Selector("draggingLocation"));
        Point point = ConvertPoint(
            receiver,
            Selector("convertPoint:fromView:"),
            location,
            IntPtr.Zero
        );
        Bounds bounds;
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            GetBoundsIntel(out bounds, receiver, Selector("bounds"));
        else
            bounds = GetBounds(receiver, Selector("bounds"));
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return false;
        double y = (point.Y - bounds.Origin.Y) / bounds.Height;
        if (GetBoolean(receiver, Selector("isFlipped")))
            y = 1 - y;
        _current.Enqueue(
            files.ToArray(),
            new Vector2((float)((point.X - bounds.Origin.X) / bounds.Width), (float)y)
        );
        return true;
    }

    /// <summary>
    /// Gets the current native class of an object.
    /// </summary>
    /// <param name="target">The Objective-C object.</param>
    /// <returns>The object's runtime class.</returns>
    [DllImport(Library, EntryPoint = "object_getClass")]
    private static extern IntPtr ObjectClass(IntPtr target);

    /// <summary>
    /// Applies or removes the destination subclass on this view instance.
    /// </summary>
    /// <param name="target">The content view.</param>
    /// <param name="type">The class to assign.</param>
    /// <returns>The previous class.</returns>
    [DllImport(Library, EntryPoint = "object_setClass")]
    private static extern IntPtr SetObjectClass(IntPtr target, IntPtr type);

    /// <summary>
    /// Sends a message taking one pointer-sized argument.
    /// </summary>
    /// <param name="target">The object receiving the message.</param>
    /// <param name="message">The native selector.</param>
    /// <param name="value">The object or index passed to the selector.</param>
    /// <returns>The object returned by the selector.</returns>
    [DllImport(Library, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendObject(IntPtr target, IntPtr message, IntPtr value);

    /// <summary>
    /// Creates an NSString containing the native pasteboard type.
    /// </summary>
    /// <param name="target">The NSString class.</param>
    /// <param name="message">The string factory selector.</param>
    /// <param name="value">The pasteboard type name.</param>
    /// <returns>The native string.</returns>
    [DllImport(Library, EntryPoint = "objc_msgSend")]
    private static extern IntPtr StringFromUtf8(
        IntPtr target,
        IntPtr message,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value
    );

    /// <summary>
    /// Reads URL objects from the drag pasteboard.
    /// </summary>
    /// <param name="target">The drag pasteboard.</param>
    /// <param name="message">The pasteboard reader selector.</param>
    /// <param name="classes">The accepted object classes.</param>
    /// <param name="options">Optional reading preferences.</param>
    /// <returns>The array of pasted objects.</returns>
    [DllImport(Library, EntryPoint = "objc_msgSend")]
    private static extern IntPtr ReadObjects(
        IntPtr target,
        IntPtr message,
        IntPtr classes,
        IntPtr options
    );

    /// <summary>
    /// Reads an Objective-C Boolean property.
    /// </summary>
    /// <param name="target">The queried object.</param>
    /// <param name="message">The property selector.</param>
    /// <returns>The Boolean value.</returns>
    [DllImport(Library, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool GetBoolean(IntPtr target, IntPtr message);

    /// <summary>
    /// Reads a point from an AppKit object.
    /// </summary>
    /// <param name="target">The queried object.</param>
    /// <param name="message">The point property selector.</param>
    /// <returns>The point returned by AppKit.</returns>
    [DllImport(Library, EntryPoint = "objc_msgSend")]
    private static extern Point GetPoint(IntPtr target, IntPtr message);

    /// <summary>
    /// Converts a dragged point into the receiving view's coordinate system.
    /// </summary>
    /// <param name="target">The destination view.</param>
    /// <param name="message">The coordinate conversion selector.</param>
    /// <param name="point">The original point.</param>
    /// <param name="source">The view owning the original coordinates, or zero for the window.</param>
    /// <returns>The destination point.</returns>
    [DllImport(Library, EntryPoint = "objc_msgSend")]
    private static extern Point ConvertPoint(
        IntPtr target,
        IntPtr message,
        Point point,
        IntPtr source
    );

    /// <summary>
    /// Reads bounds using the Apple Silicon structure return convention.
    /// </summary>
    /// <param name="target">The view whose bounds are requested.</param>
    /// <param name="message">The bounds property selector.</param>
    /// <returns>The view bounds.</returns>
    [DllImport(Library, EntryPoint = "objc_msgSend")]
    private static extern Bounds GetBounds(IntPtr target, IntPtr message);

    /// <summary>
    /// Reads bounds using the Intel macOS structure return convention.
    /// </summary>
    /// <param name="bounds">The output rectangle.</param>
    /// <param name="target">The view whose bounds are requested.</param>
    /// <param name="message">The bounds property selector.</param>
    [DllImport(Library, EntryPoint = "objc_msgSend_stret")]
    private static extern void GetBoundsIntel(out Bounds bounds, IntPtr target, IntPtr message);
}
