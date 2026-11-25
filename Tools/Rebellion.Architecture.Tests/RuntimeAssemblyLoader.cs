using System.Linq;
using System.Reflection;

namespace Rebellion.Architecture.Tests
{
    internal static class RuntimeAssemblyLoader
    {
        internal static Assembly[] Load()
        {
            string[] assemblyNames =
            [
                "Rebellion.AI",
                "Rebellion.Analytics",
                "Rebellion.Content",
                "Rebellion.Game",
                "Rebellion.Gameplay",
                "Rebellion.Generation",
                "Rebellion.Multiplayer",
                "Rebellion.Networking.Transport",
                "Rebellion.Saves",
                "Rebellion.SceneGraph",
                "Rebellion.Sessions",
                "Rebellion.Tactical.Core",
                "Rebellion.UserSettings",
                "Rebellion.Util",
            ];
            return assemblyNames
                .Select(Assembly.Load)
                .Append(typeof(AppBootstrap).Assembly)
                .ToArray();
        }
    }
}
