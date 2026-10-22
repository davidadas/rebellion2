using System;

namespace TND.FrameGen
{
    [Flags]
    public enum FrameGenDebugFlags
    {
        DebugLines = 1 << 0,
        DebugView = 1 << 1,
        ShowOnlyGeneratedFrames = 1 << 2,
    }
}
