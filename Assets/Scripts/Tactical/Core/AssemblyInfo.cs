using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GameAssembly")]
[assembly: InternalsVisibleTo("Rebellion.Gameplay")]
[assembly: InternalsVisibleTo("Rebellion.AI")]
[assembly: InternalsVisibleTo("UnitTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
#if UNITY_EDITOR || REBELLION_EOS_TACTICAL_TEST
[assembly: InternalsVisibleTo("EosTacticalIntegration")]
#endif
