using System.Runtime.CompilerServices;

// Exposes internal types to Unity test assemblies.
[assembly: InternalsVisibleTo("UnitTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]

#if UNITY_EDITOR || REBELLION_EOS_TACTICAL_TEST
// Allows opt-in runtime integration checks to compose the internal session owners.
[assembly: InternalsVisibleTo("EosTacticalIntegration")]
#endif

// Allows the Editor verification runner to exercise the internal ordered session protocol.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
