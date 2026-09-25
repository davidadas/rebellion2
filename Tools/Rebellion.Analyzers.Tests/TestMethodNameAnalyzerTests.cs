using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using Rebellion.Analyzers;

namespace Rebellion.Analyzers.Tests
{
    [TestFixture]
    public sealed class TestMethodNameAnalyzerTests
    {
        [Test]
        public async Task AnalyzeMethod_ThreeNameSections_DoesNotReportDiagnosticAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "[Test] void Save_ValidState_PersistsData() {}"
            );

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task AnalyzeMethod_OneUnderscore_ReportsDiagnosticAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "[Test] void Save_PersistsData() {}"
            );

            AssertDiagnostic(diagnostics);
        }

        [Test]
        public async Task AnalyzeMethod_NoUnderscores_ReportsDiagnosticAsync()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                "[Test] void SavesData() {}"
            );

            AssertDiagnostic(diagnostics);
        }

        /// <summary>
        /// Runs the test-method name analyzer against one method.
        /// </summary>
        /// <param name="method">The test method source.</param>
        /// <returns>The analyzer diagnostics.</returns>
        private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string method)
        {
            string source =
                $"class TestAttribute : System.Attribute {{}} class Example {{ {method} }}";
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
            MetadataReference coreLibrary = MetadataReference.CreateFromFile(
                typeof(object).Assembly.Location
            );
            CSharpCompilation compilation = CSharpCompilation.Create(
                "AnalyzerTests",
                new[] { syntaxTree },
                new[] { coreLibrary },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            ImmutableArray<DiagnosticAnalyzer> analyzers =
                ImmutableArray.Create<DiagnosticAnalyzer>(new TestMethodNameAnalyzer());

            return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        }

        /// <summary>
        /// Verifies the test-name diagnostic was reported once.
        /// </summary>
        /// <param name="diagnostics">The reported diagnostics.</param>
        private static void AssertDiagnostic(ImmutableArray<Diagnostic> diagnostics)
        {
            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(TestMethodNameAnalyzer.DiagnosticId, diagnostics[0].Id);
        }
    }
}
