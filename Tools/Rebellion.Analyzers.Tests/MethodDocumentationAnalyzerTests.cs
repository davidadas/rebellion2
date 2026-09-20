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
    public sealed class MethodDocumentationAnalyzerTests
    {
        [Test]
        public async Task Method_PrivateAndUndocumented_ReportsMissingSummaryAsync()
        {
            const string source = "class Example { private void Run() { } }";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            AssertDiagnostic(diagnostics, MethodDocumentationAnalyzer.MissingSummaryDiagnosticId);
        }

        [Test]
        public async Task Constructor_Undocumented_ReportsMissingSummaryAsync()
        {
            const string source = "class Example { public Example() { } }";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            AssertDiagnostic(diagnostics, MethodDocumentationAnalyzer.MissingSummaryDiagnosticId);
        }

        [Test]
        public async Task Method_ParameterUndocumented_ReportsMissingParameterAsync()
        {
            const string source =
                @"
class Example
{
    /// <summary>Runs an operation.</summary>
    private void Run(int count) { }
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            AssertDiagnostic(diagnostics, MethodDocumentationAnalyzer.MissingParameterDiagnosticId);
        }

        [Test]
        public async Task Method_TypeParameterUndocumented_ReportsMissingTypeParameterAsync()
        {
            const string source =
                @"
class Example
{
    /// <summary>Returns a value.</summary>
    /// <returns>The value.</returns>
    private T Get<T>() { return default(T); }
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            AssertDiagnostic(
                diagnostics,
                MethodDocumentationAnalyzer.MissingTypeParameterDiagnosticId
            );
        }

        [Test]
        public async Task Method_ReturnValueUndocumented_ReportsMissingReturnsAsync()
        {
            const string source =
                @"
class Example
{
    /// <summary>Gets a value.</summary>
    private int GetValue() { return 1; }
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            AssertDiagnostic(diagnostics, MethodDocumentationAnalyzer.MissingReturnsDiagnosticId);
        }

        [Test]
        public async Task Method_CompleteDocumentation_DoesNotReportDiagnosticAsync()
        {
            const string source =
                @"
class Example
{
    /// <summary>Returns a value.</summary>
    /// <typeparam name=""T"">The value type.</typeparam>
    /// <param name=""value"">The value.</param>
    /// <returns>The value.</returns>
    private T Get<T>(T value) { return value; }
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task Method_Inheritdoc_ReportsInheritdocDiagnosticAsync()
        {
            const string source =
                @"
class Example
{
    /// <inheritdoc/>
    public override string ToString() { return ""Example""; }
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            AssertDiagnostic(diagnostics, MethodDocumentationAnalyzer.InheritdocDiagnosticId);
        }

        [Test]
        public async Task Method_NestedInheritdoc_ReportsInheritdocDiagnosticAsync()
        {
            const string source =
                @"
class Example
{
    /// <summary><inheritdoc cref=""object.ToString""/></summary>
    public override string ToString() { return ""Example""; }
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            AssertDiagnostic(diagnostics, MethodDocumentationAnalyzer.InheritdocDiagnosticId);
        }

        [Test]
        public async Task Method_UndocumentedNUnitTest_DoesNotReportDiagnosticAsync()
        {
            const string source =
                @"
class Example
{
    [NUnit.Framework.Test]
    public int Run() { return 1; }
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task Method_UndocumentedParameterizedNUnitTest_DoesNotReportDiagnosticAsync()
        {
            const string source =
                @"
class Example
{
    [NUnit.Framework.TestCase(1)]
    public int Run(int value) { return value; }
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task Method_UndocumentedSourceDrivenNUnitTest_DoesNotReportDiagnosticAsync()
        {
            const string source =
                @"
class Example
{
    [NUnit.Framework.TestCaseSource(""Cases"")]
    public int Run(int value) { return value; }
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public async Task Method_UndocumentedUnityTest_DoesNotReportDiagnosticAsync()
        {
            const string source =
                @"
namespace UnityEngine.TestTools
{
    public sealed class UnityTestAttribute : System.Attribute { }
}

class Example
{
    [UnityEngine.TestTools.UnityTest]
    public int Run() { return 1; }
}";

            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

            Assert.IsEmpty(diagnostics);
        }

        /// <summary>
        /// Runs the documentation analyzer against one source document.
        /// </summary>
        /// <param name="source">The C# source to analyze.</param>
        /// <returns>The analyzer diagnostics.</returns>
        private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
            MetadataReference coreLibrary = MetadataReference.CreateFromFile(
                typeof(object).Assembly.Location
            );
            MetadataReference nunitLibrary = MetadataReference.CreateFromFile(
                typeof(TestAttribute).Assembly.Location
            );
            CSharpCompilation compilation = CSharpCompilation.Create(
                "AnalyzerTests",
                new[] { syntaxTree },
                new[] { coreLibrary, nunitLibrary },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            ImmutableArray<DiagnosticAnalyzer> analyzers =
                ImmutableArray.Create<DiagnosticAnalyzer>(new MethodDocumentationAnalyzer());

            return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        }

        /// <summary>
        /// Verifies that one diagnostic with the expected identifier was reported.
        /// </summary>
        /// <param name="diagnostics">The reported diagnostics.</param>
        /// <param name="diagnosticID">The expected diagnostic identifier.</param>
        private static void AssertDiagnostic(
            ImmutableArray<Diagnostic> diagnostics,
            string diagnosticID
        )
        {
            Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Id == diagnosticID));
        }
    }
}
