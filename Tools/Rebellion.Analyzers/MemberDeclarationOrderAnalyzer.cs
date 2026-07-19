using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Rebellion.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MemberDeclarationOrderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "REB0001";

        private static readonly DiagnosticDescriptor _rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Fields must precede behavior members",
            "Field declaration must appear before constructors and methods",
            "Layout",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(_rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeType,
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.InterfaceDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration
            );
        }

        private static void AnalyzeType(SyntaxNodeAnalysisContext context)
        {
            TypeDeclarationSyntax type = (TypeDeclarationSyntax)context.Node;
            bool behaviorSeen = false;

            foreach (MemberDeclarationSyntax member in type.Members)
            {
                if (member is BaseMethodDeclarationSyntax)
                {
                    behaviorSeen = true;
                    continue;
                }

                if (behaviorSeen && member is BaseFieldDeclarationSyntax field)
                {
                    context.ReportDiagnostic(Diagnostic.Create(_rule, field.GetLocation()));
                }
            }
        }
    }
}
