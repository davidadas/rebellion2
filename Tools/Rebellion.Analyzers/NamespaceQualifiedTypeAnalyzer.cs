using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Rebellion.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NamespaceQualifiedTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "REB0012";

        private static readonly DiagnosticDescriptor _rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Avoid unnecessary namespace qualification",
            "Use '{0}' with an import or alias instead of namespace-qualifying this type",
            "Style",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(_rule);

        /// <summary>
        /// Registers analysis of type names in declarations and static member access.
        /// </summary>
        /// <param name="context">The analyzer initialization context.</param>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeName,
                SyntaxKind.QualifiedName,
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxKind.AliasQualifiedName
            );
        }

        /// <summary>
        /// Reports namespace-qualified types whose simple names can be used safely.
        /// </summary>
        /// <param name="context">The syntax-node analysis context.</param>
        private static void AnalyzeName(SyntaxNodeAnalysisContext context)
        {
            SyntaxNode node = context.Node;
            for (SyntaxNode ancestor = node.Parent; ancestor != null; ancestor = ancestor.Parent)
                if (ancestor is UsingDirectiveSyntax or DocumentationCommentTriviaSyntax)
                    return;

            SyntaxNode prefix;
            SimpleNameSyntax simpleName;
            switch (node)
            {
                case QualifiedNameSyntax qualified:
                    prefix = qualified.Left;
                    simpleName = qualified.Right;
                    break;
                case MemberAccessExpressionSyntax member:
                    prefix = member.Expression;
                    simpleName = member.Name;
                    break;
                case AliasQualifiedNameSyntax alias
                    when alias.Alias.Identifier.ValueText == "global":
                    prefix = null;
                    simpleName = alias.Name;
                    break;
                default:
                    return;
            }

            SemanticModel model = context.SemanticModel;
            if (
                prefix != null
                    && model.GetSymbolInfo(prefix, context.CancellationToken).Symbol
                        is not INamespaceSymbol
                || HasExternalAlias(node)
            )
                return;

            bool attribute =
                node.Parent is AttributeSyntax attributeSyntax && attributeSyntax.Name == node;
            ISymbol resolved = model.GetSymbolInfo(node, context.CancellationToken).Symbol;
            INamedTypeSymbol type = resolved as INamedTypeSymbol;
            if (attribute && resolved is IMethodSymbol constructor)
                type = constructor.ContainingType;
            if (type == null || type.TypeKind == TypeKind.Error)
                return;

            bool expression = node is MemberAccessExpressionSyntax;
            NameSyntax replacementName = SyntaxFactory.ParseName(
                simpleName.WithoutTrivia().ToString()
            );
            SymbolInfo replacement = attribute
                ? model.GetSpeculativeSymbolInfo(
                    node.SpanStart,
                    SyntaxFactory.Attribute(replacementName)
                )
                : model.GetSpeculativeSymbolInfo(
                    node.SpanStart,
                    replacementName,
                    expression
                        ? SpeculativeBindingOption.BindAsExpression
                        : SpeculativeBindingOption.BindAsTypeOrNamespace
                );
            ISymbol replacementSymbol = replacement.Symbol;
            if (attribute && replacementSymbol is IMethodSymbol replacementConstructor)
                replacementSymbol = replacementConstructor.ContainingType;
            if (!SymbolEqualityComparer.Default.Equals(type, replacementSymbol))
            {
                if (
                    replacement.Symbol != null
                    || !replacement.CandidateSymbols.IsEmpty
                    || (
                        expression
                            ? model.LookupSymbols(
                                node.SpanStart,
                                name: simpleName.Identifier.ValueText
                            )
                            : model.LookupNamespacesAndTypes(
                                node.SpanStart,
                                name: simpleName.Identifier.ValueText
                            )
                    ).Length != 0
                )
                    return;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(_rule, node.GetLocation(), simpleName.ToString())
            );
        }

        /// <summary>
        /// Preserves qualification that selects an externally aliased assembly.
        /// </summary>
        /// <param name="node">The qualified type name.</param>
        /// <returns>Whether the name includes an external assembly alias.</returns>
        private static bool HasExternalAlias(SyntaxNode node)
        {
            foreach (SyntaxNode descendant in node.DescendantNodesAndSelf())
                if (
                    descendant is AliasQualifiedNameSyntax alias
                    && alias.Alias.Identifier.ValueText != "global"
                )
                    return true;
            return false;
        }
    }
}
