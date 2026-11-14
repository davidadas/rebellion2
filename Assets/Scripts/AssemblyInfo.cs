using System.Runtime.CompilerServices;

// Exposes internal types to Unity test assemblies.
[assembly: InternalsVisibleTo("UnitTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]

// Allows the Editor verification runner to exercise the internal ordered session protocol.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
