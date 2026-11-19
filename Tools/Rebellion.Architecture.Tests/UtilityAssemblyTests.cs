using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Util.Random;

namespace Rebellion.Architecture.Tests
{
    [TestFixture]
    public sealed class UtilityAssemblyTests
    {
        [Test]
        public void UtilityAssembly_Compilation_IsSeparateFromTheApplication()
        {
            Assert.That(
                typeof(IRandomNumberProvider).Assembly,
                Is.Not.EqualTo(typeof(GameRoot).Assembly)
            );
        }

        [Test]
        public void UtilityAssembly_References_ExcludeApplicationAndContentEditor()
        {
            string[] references = typeof(IRandomNumberProvider)
                .Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain(typeof(GameRoot).Assembly.GetName().Name));
            Assert.That(
                references,
                Does.Not.Contain(typeof(ContentPackEditorProject).Assembly.GetName().Name)
            );
        }
    }
}
