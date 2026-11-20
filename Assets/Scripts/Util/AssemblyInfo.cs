using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Rebellion.App")]
[assembly: InternalsVisibleTo("Rebellion.Game")]
[assembly: InternalsVisibleTo("Rebellion.Gameplay")]
[assembly: InternalsVisibleTo("Rebellion.AI")]
[assembly: InternalsVisibleTo("Rebellion.Saves")]
[assembly: InternalsVisibleTo("Rebellion.Content")]
[assembly: InternalsVisibleTo("Rebellion.Generation")]
[assembly: InternalsVisibleTo("Rebellion.Analytics")]
[assembly: InternalsVisibleTo("Rebellion.Sessions")]
[assembly: InternalsVisibleTo("Rebellion.Multiplayer")]
[assembly: InternalsVisibleTo("UnitTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]
#if UNITY_EDITOR || REBELLION_EOS_TACTICAL_TEST
[assembly: InternalsVisibleTo("EosTacticalIntegration")]
#endif
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
