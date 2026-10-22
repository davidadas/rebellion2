namespace TND.FrameGen
{
    /// <summary>
    /// This is a comprehensive list of all known frame generation implementations that may be supported at some point in the past, present or future.
    /// Not all of these enum values translate to an actual existing frame gen provider plugin, nor should this list be seen as a promise for future support.
    /// </summary>
    public enum FrameGenName: ulong
    {
        None = 0,

        /// <summary>
        /// AMD FidelityFX SDK 1.x swapchain with FSR 3.1 FG. For internal testing purposes only.
        /// </summary>
        FSR3 = 0x414D465352334647u,

        /// <summary>
        /// AMD FidelityFX SDK 2.x swapchain with either FSR 3.1 FG or FSR 4 FG depending on hardware support.
        /// </summary>
        FSR4 = 0x414D465352344647u,

        /// <summary>
        /// Nvidia DLSS Frame Generation using Streamline SDK swapchain.
        /// </summary>
        DLSS = 0x4E56444C53534647u,

        /// <summary>
        /// Intel XeSS Frame Generation using XeSS-FG proxy swapchain.
        /// </summary>
        XeSS = 0x494E544358654647u,
    }
}
