using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Rebellion.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DocumentationPlacementAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "REB0006";

        private static readonly DiagnosticDescriptor _rule = new DiagnosticDescriptor(
            DiagnosticId,
            "XML documentation is not allowed on fields or enum members",
            "'{0}' must not have XML documentation",
            "Documentation",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(_rule);

        /// <summary>
        /// Registers documentation-placement analysis for fields and enum members.
        /// </summary>
        /// <param name="context">The analyzer initialization context.</param>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeDeclaration,
                SyntaxKind.FieldDeclaration,
                SyntaxKind.EnumMemberDeclaration
            );
        }

        /// <summary>
        /// Reports XML documentation attached to a field or enum member.
        /// </summary>
        /// <param name="context">The syntax-node analysis context.</param>
        private static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context)
        {
            MemberDeclarationSyntax declaration = (MemberDeclarationSyntax)context.Node;
            bool hasDocumentation = declaration
                .GetLeadingTrivia()
                .Select(trivia => trivia.GetStructure())
                .OfType<DocumentationCommentTriviaSyntax>()
                .Any();
            if (!hasDocumentation)
                return;

            string name = declaration is EnumMemberDeclarationSyntax enumMember
                ? enumMember.Identifier.ValueText
                : ((FieldDeclarationSyntax)declaration)
                    .Declaration.Variables.First()
                    .Identifier.ValueText;
            context.ReportDiagnostic(Diagnostic.Create(_rule, declaration.GetLocation(), name));
        }
    }
}
