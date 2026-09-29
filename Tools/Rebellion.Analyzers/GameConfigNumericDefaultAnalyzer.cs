using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Rebellion.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class GameConfigNumericDefaultAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "REB0010";

        private static readonly DiagnosticDescriptor _rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Gameplay configuration must be authored in content",
            "GameConfig property '{0}' must not declare a numeric C# default",
            "Configuration",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(_rule);

        /// <summary>
        /// Registers property-initializer analysis.
        /// </summary>
        /// <param name="context">The analyzer initialization context.</param>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
        }

        /// <summary>
        /// Rejects numeric defaults declared by content-owned gameplay configuration sections.
        /// </summary>
        /// <param name="context">The syntax-node analysis context.</param>
        private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        {
            PropertyDeclarationSyntax property = (PropertyDeclarationSyntax)context.Node;
            if (property.Initializer == null)
                return;

            IPropertySymbol symbol = context.SemanticModel.GetDeclaredSymbol(property);
            if (!IsContentOwnedGameConfig(symbol?.ContainingType) || !IsNumeric(symbol.Type))
                return;

            Optional<object> constant = context.SemanticModel.GetConstantValue(
                property.Initializer.Value
            );
            if (!constant.HasValue)
                return;

            context.ReportDiagnostic(
                Diagnostic.Create(_rule, property.Initializer.GetLocation(), symbol.Name)
            );
        }

        /// <summary>
        /// Returns whether a type is a content-owned section nested under GameConfig.
        /// </summary>
        /// <param name="type">The containing type to inspect.</param>
        /// <returns>True when numeric defaults must come from content.</returns>
        private static bool IsContentOwnedGameConfig(INamedTypeSymbol type)
        {
            if (type?.ContainingType?.Name != "GameConfig")
                return false;

            return type.Name != "AIResponseCurveConfig" && type.Name != "AIConsiderationConfig";
        }

        /// <summary>
        /// Returns whether a property type is numeric.
        /// </summary>
        /// <param name="type">The property type.</param>
        /// <returns>True for integral, floating-point, and decimal types.</returns>
        private static bool IsNumeric(ITypeSymbol type)
        {
            return type?.SpecialType
                is SpecialType.System_SByte
                    or SpecialType.System_Byte
                    or SpecialType.System_Int16
                    or SpecialType.System_UInt16
                    or SpecialType.System_Int32
                    or SpecialType.System_UInt32
                    or SpecialType.System_Int64
                    or SpecialType.System_UInt64
                    or SpecialType.System_Single
                    or SpecialType.System_Double
                    or SpecialType.System_Decimal;
        }
    }
}
