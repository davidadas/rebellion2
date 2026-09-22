using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Rebellion.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TestMethodNameAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "REB0008";

        private static readonly ImmutableHashSet<string> _testAttributeNames =
            ImmutableHashSet.Create("Test", "TestCase", "TestCaseSource", "Theory");

        private static readonly DiagnosticDescriptor _rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Test names identify the member, scenario, and expected behavior",
            "Test method '{0}' must contain at least two underscores to separate <MemberUnderTest>_<Scenario>_<ExpectedBehavior>",
            "Naming",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(_rule);

        /// <summary>
        /// Registers test-method name analysis.
        /// </summary>
        /// <param name="context">The analyzer initialization context.</param>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        /// <summary>
        /// Reports test methods whose names contain fewer than two underscores.
        /// </summary>
        /// <param name="context">The syntax-node analysis context.</param>
        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            MethodDeclarationSyntax method = (MethodDeclarationSyntax)context.Node;
            if (!IsTestMethod(method))
                return;

            if (method.Identifier.ValueText.Count(character => character == '_') >= 2)
                return;

            context.ReportDiagnostic(
                Diagnostic.Create(
                    _rule,
                    method.Identifier.GetLocation(),
                    method.Identifier.ValueText
                )
            );
        }

        /// <summary>
        /// Returns whether a method carries a recognized test attribute.
        /// </summary>
        /// <param name="method">The method to inspect.</param>
        /// <returns>True when the method is a test.</returns>
        private static bool IsTestMethod(MethodDeclarationSyntax method)
        {
            return method
                .AttributeLists.SelectMany(list => list.Attributes)
                .Select(attribute => attribute.Name.ToString().Split('.').Last())
                .Select(name =>
                    name.EndsWith("Attribute")
                        ? name.Substring(0, name.Length - "Attribute".Length)
                        : name
                )
                .Any(_testAttributeNames.Contains);
        }
    }
}
