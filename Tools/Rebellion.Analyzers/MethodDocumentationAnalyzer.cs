using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Rebellion.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MethodDocumentationAnalyzer : DiagnosticAnalyzer
    {
        public const string MissingSummaryDiagnosticId = "REB0002";
        public const string MissingParameterDiagnosticId = "REB0003";
        public const string MissingTypeParameterDiagnosticId = "REB0004";
        public const string MissingReturnsDiagnosticId = "REB0005";

        private static readonly DiagnosticDescriptor _missingSummaryRule = new DiagnosticDescriptor(
            MissingSummaryDiagnosticId,
            "Method documentation requires a summary",
            "'{0}' must have an XML documentation summary",
            "Documentation",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor _missingParameterRule =
            new DiagnosticDescriptor(
                MissingParameterDiagnosticId,
                "Method documentation requires parameter entries",
                "Parameter '{0}' on '{1}' must have an XML documentation entry",
                "Documentation",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true
            );

        private static readonly DiagnosticDescriptor _missingTypeParameterRule =
            new DiagnosticDescriptor(
                MissingTypeParameterDiagnosticId,
                "Method documentation requires type-parameter entries",
                "Type parameter '{0}' on '{1}' must have an XML documentation entry",
                "Documentation",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true
            );

        private static readonly DiagnosticDescriptor _missingReturnsRule = new DiagnosticDescriptor(
            MissingReturnsDiagnosticId,
            "Value-returning method documentation requires a returns entry",
            "'{0}' must have an XML documentation returns entry",
            "Documentation",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                _missingSummaryRule,
                _missingParameterRule,
                _missingTypeParameterRule,
                _missingReturnsRule
            );

        /// <summary>
        /// Registers method and constructor documentation analysis.
        /// </summary>
        /// <param name="context">The analyzer initialization context.</param>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeDeclaration,
                SyntaxKind.MethodDeclaration,
                SyntaxKind.ConstructorDeclaration
            );
        }

        /// <summary>
        /// Validates the XML documentation attached to one method or constructor.
        /// </summary>
        /// <param name="context">The syntax-node analysis context.</param>
        private static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context)
        {
            BaseMethodDeclarationSyntax declaration = (BaseMethodDeclarationSyntax)context.Node;
            DocumentationCommentTriviaSyntax documentation = declaration
                .GetLeadingTrivia()
                .Select(trivia => trivia.GetStructure())
                .OfType<DocumentationCommentTriviaSyntax>()
                .LastOrDefault();
            string name = GetName(declaration);

            if (documentation == null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(_missingSummaryRule, declaration.GetLocation(), name)
                );
                return;
            }

            if (HasInheritdoc(documentation))
                return;

            if (!HasElement(documentation, "summary"))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(_missingSummaryRule, declaration.GetLocation(), name)
                );
            }

            HashSet<string> documentedParameters = GetNamedElements(documentation, "param", "name");
            foreach (ParameterSyntax parameter in declaration.ParameterList.Parameters)
            {
                string parameterName = parameter.Identifier.ValueText;
                if (!documentedParameters.Contains(parameterName))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            _missingParameterRule,
                            parameter.GetLocation(),
                            parameterName,
                            name
                        )
                    );
                }
            }

            if (declaration is MethodDeclarationSyntax method)
            {
                HashSet<string> documentedTypeParameters = GetNamedElements(
                    documentation,
                    "typeparam",
                    "name"
                );
                foreach (
                    TypeParameterSyntax typeParameter in method.TypeParameterList?.Parameters
                        ?? default(SeparatedSyntaxList<TypeParameterSyntax>)
                )
                {
                    string typeParameterName = typeParameter.Identifier.ValueText;
                    if (!documentedTypeParameters.Contains(typeParameterName))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                _missingTypeParameterRule,
                                typeParameter.GetLocation(),
                                typeParameterName,
                                name
                            )
                        );
                    }
                }

                if (
                    !method.ReturnType.IsKind(SyntaxKind.PredefinedType)
                    || method.ReturnType.ToString() != "void"
                )
                {
                    if (!HasElement(documentation, "returns"))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(_missingReturnsRule, method.GetLocation(), name)
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Gets the declared method or constructor name.
        /// </summary>
        /// <param name="declaration">The method or constructor declaration.</param>
        /// <returns>The declared member name.</returns>
        private static string GetName(BaseMethodDeclarationSyntax declaration)
        {
            if (declaration is MethodDeclarationSyntax method)
                return method.Identifier.ValueText;
            return ((ConstructorDeclarationSyntax)declaration).Identifier.ValueText;
        }

        /// <summary>
        /// Checks whether documentation is inherited from another member.
        /// </summary>
        /// <param name="documentation">The documentation to inspect.</param>
        /// <returns>True when an inheritdoc element is present.</returns>
        private static bool HasInheritdoc(DocumentationCommentTriviaSyntax documentation)
        {
            return documentation
                .Content.OfType<XmlEmptyElementSyntax>()
                .Any(element => element.Name.LocalName.ValueText == "inheritdoc");
        }

        /// <summary>
        /// Checks whether documentation contains a named XML element.
        /// </summary>
        /// <param name="documentation">The documentation to inspect.</param>
        /// <param name="elementName">The XML element name.</param>
        /// <returns>True when the named element is present.</returns>
        private static bool HasElement(
            DocumentationCommentTriviaSyntax documentation,
            string elementName
        )
        {
            return documentation.Content.Any(node =>
                node is XmlElementSyntax element
                && element.StartTag.Name.LocalName.ValueText == elementName
            );
        }

        /// <summary>
        /// Gets attribute values from documentation elements of a requested kind.
        /// </summary>
        /// <param name="documentation">The documentation to inspect.</param>
        /// <param name="elementName">The XML element name.</param>
        /// <param name="attributeName">The XML attribute name.</param>
        /// <returns>The distinct documented names.</returns>
        private static HashSet<string> GetNamedElements(
            DocumentationCommentTriviaSyntax documentation,
            string elementName,
            string attributeName
        )
        {
            return new HashSet<string>(
                documentation
                    .Content.OfType<XmlElementSyntax>()
                    .Where(element => element.StartTag.Name.LocalName.ValueText == elementName)
                    .SelectMany(element =>
                        element.StartTag.Attributes.OfType<XmlNameAttributeSyntax>()
                    )
                    .Where(attribute => attribute.Name.LocalName.ValueText == attributeName)
                    .Select(attribute => attribute.Identifier.Identifier.ValueText)
            );
        }
    }
}
