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
    public sealed class SummaryDocumentationLayoutAnalyzerTests
    {
        [Test]
        public async Task Summary_Inline_ReportsDiagnosticAsync()
        {
            const string tagName = "summary";
            string source = $"/// <{tagName}>Runs an operation.</{tagName}>\nclass Example {{ }}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            AssertDiagnostic(diagnostics);
        }

        [Test]
        public async Task Summary_Multiline_DoesNotReportDiagnosticAsync()
        {
            const string source =
                @"
/// <summary>
/// Runs an operation.
/// </summary>
class Example { }";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task Summary_ContentStartsOnOpeningTagLine_ReportsDiagnosticAsync()
        {
            const string tagName = "summary";
            string source =
                $"/// <{tagName}>Runs an operation.\n/// </{tagName}>\nclass Example {{ }}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            AssertDiagnostic(diagnostics);
        }

        [Test]
        public async Task Summary_ContentEndsOnClosingTagLine_ReportsDiagnosticAsync()
        {
            const string tagName = "summary";
            string source =
                $"/// <{tagName}>\n/// Runs an operation.</{tagName}>\nclass Example {{ }}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            AssertDiagnostic(diagnostics);
        }

        [Test]
        public async Task Parameter_Inline_DoesNotReportDiagnosticAsync()
        {
            const string source =
                @"
/// <summary>
/// Runs an operation.
/// </summary>
/// <param name=""count"">The operation count.</param>
class Example { }";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            Assert.IsEmpty(diagnostics);
        }

        /// <summary>
        /// Runs the summary-layout analyzer against one source document.
        /// </summary>
        /// <param name="source">The C# source to analyze.</param>
        /// <returns>The analyzer diagnostics.</returns>
        private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
        {
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
                ImmutableArray.Create<DiagnosticAnalyzer>(new SummaryDocumentationLayoutAnalyzer());

            return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        }

        /// <summary>
        /// Verifies the summary-layout diagnostic was reported once.
        /// </summary>
        /// <param name="diagnostics">The reported diagnostics.</param>
        private static void AssertDiagnostic(ImmutableArray<Diagnostic> diagnostics)
        {
            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(SummaryDocumentationLayoutAnalyzer.DiagnosticId, diagnostics[0].Id);
        }
    }
}
