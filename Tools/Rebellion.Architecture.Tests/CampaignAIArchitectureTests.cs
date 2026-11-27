using System.Linq;
using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using ArchUnitNET.NUnit;
using NUnit.Framework;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Rebellion.Architecture.Tests
{
    [TestFixture]
    public sealed class CampaignAIArchitectureTests
    {
        [Test]
        public void StrategicAI_Dependencies_ExcludeTacticalMultiplayerAndPresentation()
        {
            const string strategicNamespace = "^Rebellion\\.AI(?:$|\\.(?!Tactical(?:\\.|$)).*)";
            Assert.That(
                RuntimeAssemblyLoader
                    .Load()
                    .SelectMany(assembly => assembly.GetTypes())
                    .Any(type => Regex.IsMatch(type.Namespace ?? string.Empty, strategicNamespace)),
                Is.True,
                "The strategic AI ownership rule must inspect actual production types."
            );
            Types()
                .That()
                .ResideInNamespaceMatching(strategicNamespace)
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .ResideInNamespaceMatching(
                            "^Rebellion\\.(?:Multiplayer|Tactical|UI)(?:\\.|$)"
                        )
                )
                .Check(ProductionArchitecture.Load());
        }
    }
}
