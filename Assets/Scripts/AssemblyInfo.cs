using System.Runtime.CompilerServices;

// Exposes internal types to Unity edit-mode tests.
[assembly: InternalsVisibleTo("UnitTests")]

// Allows the Editor verification runner to exercise the internal ordered session protocol.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
