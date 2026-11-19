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
        public void CampaignAI_Dependencies_ExcludeTacticalAndMultiplayerModes()
        {
            Types()
                .That()
                .ResideInNamespaceMatching("^Rebellion\\.Campaign\\.AI(?:\\.|$)")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .ResideInNamespaceMatching("^Rebellion\\.(?:Multiplayer|Tactical)(?:\\.|$)")
                )
                .Check(ProductionArchitecture.Load());
        }
    }
}
