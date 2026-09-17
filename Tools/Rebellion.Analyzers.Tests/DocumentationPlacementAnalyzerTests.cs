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
    public sealed class DocumentationPlacementAnalyzerTests
    {
        /// <summary>
        /// Verifies an XML-documented field reports a diagnostic.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        [Test]
        public async Task Field_XmlDocumentation_ReportsDiagnosticAsync()
        {
            const string source =
                @"
class Example
{
    /// <summary>Stores the value.</summary>
    private int _value;
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            AssertDiagnostic(diagnostics);
        }

        /// <summary>
        /// Verifies an XML-documented enum member reports a diagnostic.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        [Test]
        public async Task EnumMember_XmlDocumentation_ReportsDiagnosticAsync()
        {
            const string source =
                @"
enum Example
{
    /// <summary>Represents the first value.</summary>
    First,
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            AssertDiagnostic(diagnostics);
        }

        /// <summary>
        /// Verifies an XML-documented property reports no placement diagnostic.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        [Test]
        public async Task Property_XmlDocumentation_DoesNotReportDiagnosticAsync()
        {
            const string source =
                @"
class Example
{
    /// <summary>Gets the value.</summary>
    public int Value { get; }
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            Assert.IsEmpty(diagnostics);
        }

        /// <summary>
        /// Verifies an ordinary comment on a field reports no placement diagnostic.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        [Test]
        public async Task Field_OrdinaryComment_DoesNotReportDiagnosticAsync()
        {
            const string source =
                @"
class Example
{
    // Stores a protocol-defined sentinel.
    private int _value;
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            Assert.IsEmpty(diagnostics);
        }

        /// <summary>
        /// Runs the documentation-placement analyzer against one source document.
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
                ImmutableArray.Create<DiagnosticAnalyzer>(new DocumentationPlacementAnalyzer());

            return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        }

        /// <summary>
        /// Verifies the placement diagnostic was reported once.
        /// </summary>
        /// <param name="diagnostics">The reported diagnostics.</param>
        private static void AssertDiagnostic(ImmutableArray<Diagnostic> diagnostics)
        {
            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(DocumentationPlacementAnalyzer.DiagnosticId, diagnostics[0].Id);
        }
    }
}
