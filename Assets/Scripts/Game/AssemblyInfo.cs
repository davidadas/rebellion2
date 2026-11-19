using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GameAssembly")]
[assembly: InternalsVisibleTo("UnitTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
[assembly: InternalsVisibleTo("ContentPackEditorAssembly")]
#if UNITY_EDITOR || REBELLION_EOS_TACTICAL_TEST
[assembly: InternalsVisibleTo("EosTacticalIntegration")]
#endif
