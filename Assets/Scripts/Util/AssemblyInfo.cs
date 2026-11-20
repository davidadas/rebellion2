using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GameAssembly")]
[assembly: InternalsVisibleTo("Rebellion.Game")]
[assembly: InternalsVisibleTo("Rebellion.Gameplay")]
[assembly: InternalsVisibleTo("Rebellion.AI")]
[assembly: InternalsVisibleTo("UnitTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]
#if UNITY_EDITOR || REBELLION_EOS_TACTICAL_TEST
[assembly: InternalsVisibleTo("EosTacticalIntegration")]
#endif
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
