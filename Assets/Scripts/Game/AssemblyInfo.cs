using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Rebellion.App")]
[assembly: InternalsVisibleTo("Rebellion.Gameplay")]
[assembly: InternalsVisibleTo("Rebellion.Tactical.Core")]
[assembly: InternalsVisibleTo("Rebellion.AI")]
[assembly: InternalsVisibleTo("Rebellion.Content")]
[assembly: InternalsVisibleTo("Rebellion.Generation")]
[assembly: InternalsVisibleTo("Rebellion.Sessions")]
[assembly: InternalsVisibleTo("Rebellion.Multiplayer")]
[assembly: InternalsVisibleTo("UnitTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
#if UNITY_EDITOR || REBELLION_EOS_TACTICAL_TEST
[assembly: InternalsVisibleTo("EosTacticalIntegration")]
#endif
