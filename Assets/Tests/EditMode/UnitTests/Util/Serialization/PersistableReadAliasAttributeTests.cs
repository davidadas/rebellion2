using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Util.Serialization
{
    [TestFixture]
    public sealed class PersistableReadAliasAttributeTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Constructor_BlankName_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => new PersistableReadAliasAttribute(name));
        }

        [Test]
        public void Deprecated_StackedDeclarations_PreservesEachAliasStatus()
        {
            PersistableReadAliasAttribute[] aliases = typeof(AliasStatus)
                .GetCustomAttributes(typeof(PersistableReadAliasAttribute), inherit: false)
                .Cast<PersistableReadAliasAttribute>()
                .ToArray();

            Assert.IsFalse(aliases.Single(alias => alias.Name == "CurrentAlias").Deprecated);
            Assert.IsTrue(aliases.Single(alias => alias.Name == "OldAlias").Deprecated);
        }

        [PersistableReadAlias("CurrentAlias")]
        [PersistableReadAlias("OldAlias", Deprecated = true)]
        public sealed class AliasStatus { }
    }
}
