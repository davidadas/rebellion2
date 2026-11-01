using System.Linq;
using System.Runtime.CompilerServices;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.NUnit;
using NUnit.Framework;
using Rebellion.Game;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Rebellion.Architecture.Tests
{
    [TestFixture]
    public sealed class ContentEditorArchitectureTests
    {
        private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
            .LoadAssemblies(typeof(GameRoot).Assembly, typeof(ContentPackEditorProject).Assembly)
            .Build();

        [Test]
        public void ContentEditorViews_ProjectMutations_UseWorkspaceController()
        {
            IArchRule rule = Classes()
                .That()
                .ResideInAssembly(typeof(ContentPackEditorProject).Assembly.FullName)
                .And()
                .HaveFullNameContaining("View")
                .Should()
                .NotCallAny(
                    MethodMembers()
                        .That()
                        .AreDeclaredIn(
                            Types()
                                .That()
                                .HaveFullName("ContentPackEditorProject")
                                .Or()
                                .HaveFullName("ContentPackEditorField")
                                .Or()
                                .HaveFullName("ContentPackEditorCatalog")
                                .Or()
                                .HaveFullName("ContentPackEditorDocument")
                        )
                        .And()
                        .HaveNameMatching(
                            "^(Set|Add|Remove|Move|Duplicate|BeginEdit|Undo|Redo|Commit|Save|Restore).*"
                        )
                );
            rule.Check(_architecture);
        }

        [Test]
        public void ContentEditorPresentation_XmlMutations_BelongToTheAuthoringModel()
        {
            IArchRule rule = Types()
                .That()
                .ResideInAssembly(typeof(ContentPackEditorProject).Assembly.FullName)
                .And()
                .Are(
                    Types()
                        .That()
                        .HaveFullNameContaining("View")
                        .Or()
                        .HaveFullNameContaining("ContentEditorNumericGrid")
                )
                .Should()
                .NotCallAny(
                    MethodMembers()
                        .That()
                        .AreDeclaredIn(
                            Types()
                                .That()
                                .HaveFullNameMatching(
                                    @"^System\.Xml\.Linq\.(XContainer|XElement|XNode)$"
                                )
                        )
                        .And()
                        .HaveNameMatching("^(Add|Remove|Replace|Set).*")
                );
            rule.Check(_architecture);
        }

        [Test]
        public void ContentEditorViews_Dependencies_ExcludePersistenceAndValidationOwners()
        {
            IArchRule rule = Types()
                .That()
                .ResideInAssembly(typeof(ContentPackEditorProject).Assembly.FullName)
                .And()
                .HaveFullNameContaining("View")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .HaveFullName("ContentPackEditorStorage")
                        .Or()
                        .HaveFullName("ContentEditorPackValidator")
                        .Or()
                        .HaveFullName("ContentPackEditorExporter")
                        .Or()
                        .HaveFullName("ContentPackEditorWorkspaceController")
                        .Or()
                        .HaveFullName("ContentPackEditorSession")
                );
            rule.Check(_architecture);
        }

        [Test]
        public void SharedControls_Dependencies_ExcludeContentEditor()
        {
            var controls = Types()
                .That()
                .HaveFullName("TextInputHistory")
                .Or()
                .HaveFullName("TextInputOverflowView")
                .Or()
                .HaveFullName("HelpTooltipView")
                .Or()
                .HaveFullName("ScrollAreaLayout")
                .Or()
                .HaveFullName("ImagePreviewView")
                .Or()
                .HaveFullName("ActionDialogView")
                .Or()
                .HaveFullName("SplitterView")
                .Or()
                .HaveFullName("MenuBarHeadingView")
                .Or()
                .HaveFullName("ShortcutNavigationInput")
                .Or()
                .HaveFullName("RectangleLayout")
                .Or()
                .HaveFullName("RectangleLayoutItem");
            IArchRule rule = Types()
                .That()
                .Are(controls)
                .Should()
                .Be(Types().That().ResideInAssembly(typeof(GameRoot).Assembly.FullName))
                .And(
                    Types()
                        .That()
                        .Are(controls)
                        .Should()
                        .NotDependOnAny(
                            Types()
                                .That()
                                .ResideInAssembly(
                                    typeof(ContentPackEditorProject).Assembly.FullName
                                )
                        )
                );
            rule.Check(_architecture);
        }

        [Test]
        public void ContentEditorProject_Dependencies_ExcludePersistenceAndPresentationOwners()
        {
            IArchRule rule = Types()
                .That()
                .HaveFullName("ContentPackEditorProject")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .HaveFullName("ContentPackEditorStorage")
                        .Or()
                        .HaveFullName("ContentEditorPackValidator")
                        .Or()
                        .HaveFullName("ContentPackEditorExporter")
                        .Or()
                        .HaveFullName("ContentAssets")
                        .Or()
                        .HaveFullName("ContentPackEditorView")
                );
            rule.Check(_architecture);
        }

        [Test]
        public void ContentEditorDocuments_FileAccess_BelongsToStorage()
        {
            IArchRule rule = Types()
                .That()
                .HaveFullName("ContentPackEditorDocument")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .HaveFullName("System.IO.File")
                        .Or()
                        .HaveFullName("System.IO.Directory")
                        .Or()
                        .HaveFullName("System.IO.FileStream")
                );
            rule.Check(_architecture);
        }

        [Test]
        public void GameAssembly_InternalAccess_DoesNotExposeInternalsToContentEditor()
        {
            string[] friends = typeof(GameRoot)
                .Assembly.GetCustomAttributes(typeof(InternalsVisibleToAttribute), false)
                .Cast<InternalsVisibleToAttribute>()
                .Select(attribute => attribute.AssemblyName)
                .ToArray();
            CollectionAssert.DoesNotContain(friends, "ContentPackEditorAssembly");
        }
    }
}
