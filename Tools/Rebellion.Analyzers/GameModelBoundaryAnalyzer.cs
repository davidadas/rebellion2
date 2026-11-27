using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Rebellion.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class GameModelBoundaryAnalyzer : DiagnosticAnalyzer
    {
        public const string GameRootDiagnosticId = "REB0013";
        public const string CallbackDiagnosticId = "REB0014";

        private const string _gameDirectory = "/Assets/Scripts/Game/";

        private static readonly DiagnosticDescriptor _gameRootRule = new(
            GameRootDiagnosticId,
            "Restrict GameRoot references in the game model",
            "Game model code may only reference GameRoot in missions or game events",
            "Architecture",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor _callbackRule = new(
            CallbackDiagnosticId,
            "Keep native events and delegates out of the game model",
            "Game model code must not use native C# events or delegate callbacks",
            "Architecture",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(_gameRootRule, _callbackRule);

        /// <summary>
        /// Registers analysis for game model source files.
        /// </summary>
        /// <param name="context">The analyzer initialization context.</param>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSemanticModelAction(AnalyzeFile);
        }

        /// <summary>
        /// Applies the architecture rules to files under the game model directory.
        /// </summary>
        /// <param name="context">The file and semantic model being analyzed.</param>
        private static void AnalyzeFile(SemanticModelAnalysisContext context)
        {
            SemanticModel model = context.SemanticModel;
            string path = "/" + model.SyntaxTree.FilePath.Replace('\\', '/');
            int directoryIndex = path.LastIndexOf(
                _gameDirectory,
                StringComparison.OrdinalIgnoreCase
            );
            if (directoryIndex < 0)
                return;

            string file = path.Substring(directoryIndex + _gameDirectory.Length);
            bool allowsGameRoot =
                file.StartsWith("Missions/", StringComparison.OrdinalIgnoreCase)
                || file.StartsWith("GameEvents/", StringComparison.OrdinalIgnoreCase)
                || file.StartsWith("Scripting/", StringComparison.OrdinalIgnoreCase);
            INamedTypeSymbol gameRoot = allowsGameRoot
                ? null
                : model.Compilation.GetTypeByMetadataName("Rebellion.Game.GameRoot");

            foreach (
                SyntaxNode node in model
                    .SyntaxTree.GetRoot(context.CancellationToken)
                    .DescendantNodes()
            )
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                switch (node)
                {
                    case EventDeclarationSyntax declaration:
                        ReportCallback(context, declaration.EventKeyword.GetLocation());
                        break;
                    case EventFieldDeclarationSyntax declaration:
                        ReportCallback(context, declaration.EventKeyword.GetLocation());
                        break;
                    case DelegateDeclarationSyntax declaration:
                        ReportCallback(context, declaration.DelegateKeyword.GetLocation());
                        break;
                    case AssignmentExpressionSyntax assignment
                        when assignment.IsKind(SyntaxKind.AddAssignmentExpression)
                            || assignment.IsKind(SyntaxKind.SubtractAssignmentExpression):
                        if (
                            model.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol
                            is IEventSymbol
                        )
                            ReportCallback(context, assignment.OperatorToken.GetLocation());
                        break;
                    case ArgumentSyntax argument
                        when argument.Expression
                            is AnonymousFunctionExpressionSyntax
                                or SimpleNameSyntax
                                or MemberAccessExpressionSyntax:
                        if (
                            IsNativeAction(
                                model
                                    .GetTypeInfo(argument.Expression, context.CancellationToken)
                                    .ConvertedType
                            )
                        )
                            ReportCallback(context, argument.Expression.GetLocation());
                        break;
                    case SimpleNameSyntax name when name.Parent is not NameEqualsSyntax:
                        AnalyzeName(context, name, gameRoot);
                        break;
                }
            }
        }

        /// <summary>
        /// Identifies framework actions and event handlers passed without a delegate type in source.
        /// </summary>
        /// <param name="type">The argument's converted type.</param>
        /// <returns>Whether the argument is a native action or event handler.</returns>
        private static bool IsNativeAction(ITypeSymbol type) =>
            type?.TypeKind == TypeKind.Delegate
            && type.Name is "Action" or "EventHandler"
            && type.ContainingNamespace
                is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };

        /// <summary>
        /// Reports forbidden types and references to GameRoot members.
        /// </summary>
        /// <param name="context">The file and semantic model being analyzed.</param>
        /// <param name="name">The source name to resolve.</param>
        /// <param name="gameRoot">The prohibited type, when the file is subject to the rule.</param>
        private static void AnalyzeName(
            SemanticModelAnalysisContext context,
            SimpleNameSyntax name,
            INamedTypeSymbol gameRoot
        )
        {
            SemanticModel model = context.SemanticModel;
            ISymbol symbol = model.GetSymbolInfo(name, context.CancellationToken).Symbol;
            ITypeSymbol type = symbol as ITypeSymbol;
            if (name is IdentifierNameSyntax { IsVar: true })
                type ??= model.GetTypeInfo(name, context.CancellationToken).Type;

            if (
                gameRoot != null
                && (
                    SymbolEqualityComparer.Default.Equals(type, gameRoot)
                    || ReferencesGameRootMember(context, name, symbol, gameRoot)
                )
            )
                context.ReportDiagnostic(Diagnostic.Create(_gameRootRule, name.GetLocation()));

            if (
                type?.TypeKind == TypeKind.Delegate
                || type?.SpecialType
                    is SpecialType.System_Delegate
                        or SpecialType.System_MulticastDelegate
            )
            {
                if (
                    !name.Ancestors()
                        .Any(node => node is EventFieldDeclarationSyntax or EventDeclarationSyntax)
                )
                    ReportCallback(context, name.GetLocation());
            }
        }

        /// <summary>
        /// Identifies uses of root members even when the receiver has an inferred type.
        /// </summary>
        /// <param name="context">The file and semantic model being analyzed.</param>
        /// <param name="name">The member name in source.</param>
        /// <param name="symbol">The symbol associated with the name.</param>
        /// <param name="gameRoot">The prohibited root type.</param>
        /// <returns>Whether the member introduces a reference to the root.</returns>
        private static bool ReferencesGameRootMember(
            SemanticModelAnalysisContext context,
            SimpleNameSyntax name,
            ISymbol symbol,
            INamedTypeSymbol gameRoot
        )
        {
            if (!SymbolEqualityComparer.Default.Equals(symbol?.ContainingType, gameRoot))
                return false;

            SemanticModel model = context.SemanticModel;
            ISymbol enclosing = model.GetEnclosingSymbol(name.SpanStart, context.CancellationToken);
            if (
                SymbolEqualityComparer.Default.Equals(
                    enclosing as INamedTypeSymbol ?? enclosing?.ContainingType,
                    gameRoot
                )
            )
                return false;

            return name.Parent is not MemberAccessExpressionSyntax access
                || !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol,
                    gameRoot
                );
        }

        /// <summary>
        /// Reports an event or delegate callback at its source location.
        /// </summary>
        /// <param name="context">The file and semantic model being analyzed.</param>
        /// <param name="location">The source location to report.</param>
        private static void ReportCallback(
            SemanticModelAnalysisContext context,
            Location location
        ) => context.ReportDiagnostic(Diagnostic.Create(_callbackRule, location));
    }
}
