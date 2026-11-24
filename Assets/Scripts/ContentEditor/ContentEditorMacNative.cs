using System;
using System.Runtime.InteropServices;

/// <summary>
/// Shares the Objective-C runtime calls used by the content editor's native input.
/// </summary>
internal static class ContentEditorMacNative
{
    internal const string Library = "/usr/lib/libobjc.A.dylib";

    /// <summary>
    /// Looks up a registered Objective-C class by name.
    /// </summary>
    /// <param name="name">The class name.</param>
    /// <returns>The class handle or zero.</returns>
    [DllImport(Library, EntryPoint = "objc_getClass")]
    internal static extern IntPtr GetClass(string name);

    /// <summary>
    /// Looks up or registers an Objective-C message name.
    /// </summary>
    /// <param name="name">The message name.</param>
    /// <returns>The selector handle.</returns>
    [DllImport(Library, EntryPoint = "sel_registerName")]
    internal static extern IntPtr Selector(string name);

    /// <summary>
    /// Allocates the receiver class before it is registered.
    /// </summary>
    /// <param name="superclass">The class to inherit from.</param>
    /// <param name="name">The class name.</param>
    /// <param name="extraBytes">Additional class storage required.</param>
    /// <returns>The allocated class handle or zero.</returns>
    [DllImport(Library, EntryPoint = "objc_allocateClassPair")]
    internal static extern IntPtr AllocateClass(IntPtr superclass, string name, UIntPtr extraBytes);

    /// <summary>
    /// Associates a managed callback with the receiver class.
    /// </summary>
    /// <param name="target">The class receiving the instance method.</param>
    /// <param name="action">The action selector.</param>
    /// <param name="callback">The action's managed implementation.</param>
    /// <param name="types">The Objective-C return and parameter type encoding.</param>
    /// <returns>Whether the method was added.</returns>
    [DllImport(Library, EntryPoint = "class_addMethod")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool AddMethod(
        IntPtr target,
        IntPtr action,
        IntPtr callback,
        string types
    );

    /// <summary>
    /// Makes an allocated class available to the Objective-C runtime.
    /// </summary>
    /// <param name="target">The new class.</param>
    [DllImport(Library, EntryPoint = "objc_registerClassPair")]
    internal static extern void RegisterClass(IntPtr target);

    /// <summary>
    /// Discards a class whose registration could not be completed.
    /// </summary>
    /// <param name="target">The unregistered class.</param>
    [DllImport(Library, EntryPoint = "objc_disposeClassPair")]
    internal static extern void DisposeClass(IntPtr target);

    /// <summary>
    /// Reads an object or pointer-sized property.
    /// </summary>
    /// <param name="target">The object or class receiving the message.</param>
    /// <param name="message">The property or factory selector.</param>
    /// <returns>The returned object or pointer-sized value.</returns>
    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr Get(IntPtr target, IntPtr message);

    /// <summary>
    /// Sends a parameterless Objective-C command.
    /// </summary>
    /// <param name="target">The object receiving the message.</param>
    /// <param name="message">The command selector.</param>
    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern void Invoke(IntPtr target, IntPtr message);

    /// <summary>
    /// Sends an Objective-C command taking an object argument.
    /// </summary>
    /// <param name="target">The object receiving the message.</param>
    /// <param name="message">The command selector.</param>
    /// <param name="argument">The command argument.</param>
    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern void Invoke(IntPtr target, IntPtr message, IntPtr argument);
}
