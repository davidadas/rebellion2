using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace TND.Common.Native
{
    public class NativeStructs<TData>: IDisposable
        where TData: struct
    {
        // Multi-buffered native struct. Because Unity's rendering is multithreaded, it is not safe to use the same memory region on every frame.
        // Otherwise, this might lead to subtle issues when the main thread for the current frame overwrites data that a render thread is still using for the previous frame.
        private readonly IntPtr[] _nativePointers =
        {
            Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TData))),
            Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TData))),
            Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TData))),
            Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TData))),
        };

        public void SetData(ref TData data)
        {
            unsafe { UnsafeUtility.CopyStructureToPtr(ref data, _nativePointers[Time.frameCount & 3].ToPointer()); }
        }

        public IntPtr GetPointer() => _nativePointers[Time.frameCount & 3];

        public void Dispose()
        {
            for (int i = 0; i < _nativePointers.Length; ++i)
            {
                if (_nativePointers[i] == IntPtr.Zero)
                    return;

                Marshal.FreeHGlobal(_nativePointers[i]);
                _nativePointers[i] = IntPtr.Zero;
            }
        }
    }
}
