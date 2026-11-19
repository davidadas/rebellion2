using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using Rebellion.Analyzers;

namespace Rebellion.Analyzers.Tests
{
    [TestFixture]
    public sealed class NamespaceQualifiedTypeAnalyzerTests
    {
        [TestCase("Sample.Widget value;")]
        [TestCase("Sample.Widget[] value;")]
        [TestCase("object value = new Sample.Widget();")]
        [TestCase("object value = typeof(Sample.Widget);")]
        [TestCase("string value = nameof(Sample.Widget);")]
        [TestCase("int value = Sample.Widget.Count;")]
        [TestCase("global::Sample.Widget value;")]
        public async Task AnalyzeName_MissingImport_ReportsDiagnosticAsync(string member)
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "namespace Sample { public class Widget { public static int Count; } } "
                    + "class Example { "
                    + member
                    + " }"
            );

            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(NamespaceQualifiedTypeAnalyzer.DiagnosticId, diagnostics[0].Id);
            Assert.AreEqual(DiagnosticSeverity.Error, diagnostics[0].Severity);
        }

        [Test]
        public async Task AnalyzeName_ExistingImport_ReportsDiagnosticAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "using Sample; namespace Sample { public class Widget {} } "
                    + "class Example { Sample.Widget value; }"
            );

            Assert.AreEqual(1, diagnostics.Length);
        }

        [Test]
        public async Task AnalyzeName_QualifiedTacticalDefinition_ReportsDeclarationAndGenericArgumentAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "namespace Rebellion.Game.SpaceCombat { public class TacticalCombatDefinition {} } "
                    + "class Example { object Load() { "
                    + "Rebellion.Game.SpaceCombat.TacticalCombatDefinition tacticalCombat = "
                    + "Deserialize<Rebellion.Game.SpaceCombat.TacticalCombatDefinition>(); "
                    + "return tacticalCombat; } T Deserialize<T>() => default(T); }"
            );

            Assert.AreEqual(2, diagnostics.Length);
        }

        [TestCase("System.Serializable")]
        [TestCase("System.SerializableAttribute")]
        public async Task AnalyzeName_QualifiedAttribute_ReportsDiagnosticAsync(string name)
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "[" + name + "] class Example {}"
            );

            Assert.AreEqual(1, diagnostics.Length);
        }

        [Test]
        public async Task AnalyzeName_ShadowedAttribute_DoesNotReportDiagnosticAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "using System; class SerializableAttribute : Attribute {} [System.Serializable] class Example {}"
            );

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task AnalyzeName_GenericTypeAndArgument_ReportsBothQualificationsAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "namespace Sample { public class Widget {} } "
                    + "class Example { System.Collections.Generic.List<Sample.Widget> value; }"
            );

            Assert.AreEqual(2, diagnostics.Length);
        }

        [Test]
        public async Task AnalyzeName_NamespaceQualifiedOuterType_ReportsOuterQualificationOnceAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "namespace Sample { public class Outer { public class Inner {} } } "
                    + "class Example { Sample.Outer.Inner value; }"
            );

            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(
                "Sample.Outer",
                diagnostics[0]
                    .Location.SourceTree.GetText()
                    .ToString(diagnostics[0].Location.SourceSpan)
            );
        }

        [TestCase("using Sample; class Example { Widget value; }")]
        [TestCase("using Choice = Sample.Widget; class Example { Choice value; }")]
        [TestCase("using Sample; class Example { Outer.Inner value; }")]
        [TestCase("using static Sample.Widget; class Example { int value = Count; }")]
        public async Task AnalyzeName_ImportedOrNestedType_DoesNotReportDiagnosticAsync(
            string source
        )
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                source
                    + " namespace Sample { public class Widget { public static int Count; } "
                    + "public class Outer { public class Inner {} } }"
            );

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task AnalyzeName_AmbiguousImportedName_DoesNotReportDiagnosticAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "using First; using Second; namespace First { public class Widget {} } "
                    + "namespace Second { public class Widget {} } "
                    + "class Example { First.Widget value; }"
            );

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task AnalyzeName_LocalTypeCollision_DoesNotReportDiagnosticAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "namespace Sample { public class Widget {} } "
                    + "class Widget {} class Example { Sample.Widget value; }"
            );

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task AnalyzeName_LocalVariableShadowsStaticType_DoesNotReportDiagnosticAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "namespace Sample { public class Widget { public static int Count; } } "
                    + "class Example { int Read() { int Widget = 1; return Sample.Widget.Count; } }"
            );

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task AnalyzeName_FieldShadowsOnlyExpression_ReportsTypeQualificationAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "namespace Sample { public class Widget {} } "
                    + "class Example { object Widget; Sample.Widget other; }"
            );

            Assert.AreEqual(1, diagnostics.Length);
        }

        [Test]
        public async Task AnalyzeName_ExistingAlias_ReportsNamespaceQualificationAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "using Widget = Sample.Widget; namespace Sample { public class Widget {} } "
                    + "class Example { Sample.Widget value; }"
            );

            Assert.AreEqual(1, diagnostics.Length);
        }

        [Test]
        public async Task AnalyzeName_QualifiedDocumentation_DoesNotReportDiagnosticAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "namespace Sample { public class Widget {} } "
                    + "/// <summary>See <see cref=\"Sample.Widget\"/>.</summary>\nclass Example {}"
            );

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task AnalyzeName_UnresolvedType_DoesNotReportDiagnosticAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "class Example { Unavailable.Widget value; }",
                allowCompilationErrors: true
            );

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task AnalyzeName_ExternalAssemblyAlias_DoesNotReportDiagnosticAsync()
        {
            CSharpCompilation dependency = CSharpCompilation.Create(
                "ExternalTypes",
                new[] { CSharpSyntaxTree.ParseText("namespace Sample { public class Widget {} }") },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "extern alias Dependency; class Example { Dependency::Sample.Widget value; }",
                extraReference: dependency.ToMetadataReference(
                    aliases: ImmutableArray.Create("Dependency")
                )
            );

            Assert.IsEmpty(diagnostics);
        }

        /// <summary>
        /// Runs qualification analysis against a bound compilation.
        /// </summary>
        /// <param name="source">The complete source to analyze.</param>
        /// <param name="allowCompilationErrors">Whether the fixture intentionally omits a type.</param>
        /// <param name="extraReference">An optional externally aliased assembly.</param>
        /// <returns>The qualification diagnostics.</returns>
        private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
            string source,
            bool allowCompilationErrors = false,
            MetadataReference extraReference = null
        )
        {
            List<MetadataReference> references = new()
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            };
            if (extraReference != null)
                references.Add(extraReference);
            CSharpCompilation compilation = CSharpCompilation.Create(
                "QualificationTests",
                new[] { CSharpSyntaxTree.ParseText(source) },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            if (!allowCompilationErrors)
                Assert.IsEmpty(
                    compilation
                        .GetDiagnostics()
                        .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                );
            return await compilation
                .WithAnalyzers(
                    ImmutableArray.Create<DiagnosticAnalyzer>(new NamespaceQualifiedTypeAnalyzer())
                )
                .GetAnalyzerDiagnosticsAsync();
        }
    }
}
