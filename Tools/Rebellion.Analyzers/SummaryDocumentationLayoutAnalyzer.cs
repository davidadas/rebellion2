using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Rebellion.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SummaryDocumentationLayoutAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "REB0011";

        private static readonly DiagnosticDescriptor _rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Summary documentation must use multiline layout",
            "Summary documentation must place its opening tag, content, and closing tag on separate lines",
            "Documentation",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(_rule);

        /// <summary>
        /// Registers summary-documentation layout analysis.
        /// </summary>
        /// <param name="context">The analyzer initialization context.</param>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeSummary, SyntaxKind.XmlElement);
        }

        /// <summary>
        /// Reports summary elements that do not place their tags and content on separate lines.
        /// </summary>
        /// <param name="context">The syntax-node analysis context.</param>
        private static void AnalyzeSummary(SyntaxNodeAnalysisContext context)
        {
            XmlElementSyntax element = (XmlElementSyntax)context.Node;
            if (element.StartTag.Name.LocalName.ValueText != "summary")
                return;

            int openingLine = GetStartLine(element.StartTag);
            int closingLine = GetStartLine(element.EndTag);
            SyntaxToken[] contentTokens = element
                .Content.SelectMany(content => content.DescendantTokens())
                .Where(token => !string.IsNullOrWhiteSpace(token.ValueText))
                .ToArray();
            bool contentSharesTagLine =
                contentTokens.Length > 0
                && (
                    GetStartLine(contentTokens[0]) <= openingLine
                    || GetEndLine(contentTokens[contentTokens.Length - 1]) >= closingLine
                );
            if (openingLine < closingLine && !contentSharesTagLine)
                return;

            context.ReportDiagnostic(Diagnostic.Create(_rule, element.GetLocation()));
        }

        /// <summary>
        /// Returns the zero-based source line where a syntax node begins.
        /// </summary>
        /// <param name="node">The syntax node to inspect.</param>
        /// <returns>The node start line.</returns>
        private static int GetStartLine(SyntaxNode node)
        {
            return node.GetLocation().GetLineSpan().StartLinePosition.Line;
        }

        /// <summary>
        /// Returns the zero-based source line where a syntax token begins.
        /// </summary>
        /// <param name="token">The syntax token to inspect.</param>
        /// <returns>The token start line.</returns>
        private static int GetStartLine(SyntaxToken token)
        {
            return token.GetLocation().GetLineSpan().StartLinePosition.Line;
        }

        /// <summary>
        /// Returns the zero-based source line where a syntax token ends.
        /// </summary>
        /// <param name="token">The syntax token to inspect.</param>
        /// <returns>The token end line.</returns>
        private static int GetEndLine(SyntaxToken token)
        {
            return token.GetLocation().GetLineSpan().EndLinePosition.Line;
        }
    }
}
