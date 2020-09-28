// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.CSharp;
using static Roslynator.CSharp.SyntaxTriviaAnalysis;

namespace Roslynator.Formatting.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class FixFormattingOfListAnalyzer : BaseDiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(DiagnosticDescriptors.FixFormattingOfList); }
        }

        public override void Initialize(AnalysisContext context)
        {
            base.Initialize(context);

            context.RegisterSyntaxNodeAction(f => AnalyzeParameterList(f), SyntaxKind.ParameterList);
            context.RegisterSyntaxNodeAction(f => AnalyzeBracketedParameterList(f), SyntaxKind.BracketedParameterList);
            context.RegisterSyntaxNodeAction(f => AnalyzeTypeParameterList(f), SyntaxKind.TypeParameterList);

            context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.ArgumentList);
            context.RegisterSyntaxNodeAction(f => AnalyzeBracketedArgumentList(f), SyntaxKind.BracketedArgumentList);
            context.RegisterSyntaxNodeAction(f => AnalyzeAttributeArgumentList(f), SyntaxKind.AttributeArgumentList);
            context.RegisterSyntaxNodeAction(f => AnalyzeTypeArgumentList(f), SyntaxKind.TypeArgumentList);
        }

        private static void AnalyzeParameterList(SyntaxNodeAnalysisContext context)
        {
            var parameterList = (ParameterListSyntax)context.Node;

            Analyze(context, parameterList.OpenParenToken, parameterList.Parameters);
        }

        private static void AnalyzeBracketedParameterList(SyntaxNodeAnalysisContext context)
        {
            var parameterList = (BracketedParameterListSyntax)context.Node;

            Analyze(context, parameterList.OpenBracketToken, parameterList.Parameters);
        }

        private static void AnalyzeTypeParameterList(SyntaxNodeAnalysisContext context)
        {
            var parameterList = (TypeParameterListSyntax)context.Node;

            Analyze(context, parameterList.LessThanToken, parameterList.Parameters);
        }

        private static void AnalyzeArgumentList(SyntaxNodeAnalysisContext context)
        {
            var argumentList = (ArgumentListSyntax)context.Node;

            Analyze(context, argumentList.OpenParenToken, argumentList.Arguments);
        }

        private static void AnalyzeBracketedArgumentList(SyntaxNodeAnalysisContext context)
        {
            var argumentList = (BracketedArgumentListSyntax)context.Node;

            Analyze(context, argumentList.OpenBracketToken, argumentList.Arguments);
        }

        private static void AnalyzeAttributeArgumentList(SyntaxNodeAnalysisContext context)
        {
            var argumentList = (AttributeArgumentListSyntax)context.Node;

            Analyze(context, argumentList.OpenParenToken, argumentList.Arguments);
        }

        private static void AnalyzeTypeArgumentList(SyntaxNodeAnalysisContext context)
        {
            var argumentList = (TypeArgumentListSyntax)context.Node;

            Analyze(context, argumentList.LessThanToken, argumentList.Arguments);
        }

        private static void Analyze<TNode>(
            SyntaxNodeAnalysisContext context,
            SyntaxToken openToken,
            SeparatedSyntaxList<TNode> nodes) where TNode : SyntaxNode
        {
            TNode first = nodes.FirstOrDefault();

            if (first == null)
                return;

            if (nodes.IsSingleLine(includeExteriorTrivia: false))
            {
                SyntaxTriviaList trailing = openToken.TrailingTrivia;

                if (!IsOptionalWhitespaceThenOptionalSingleLineCommentThenEndOfLineTrivia(trailing))
                    return;

                int indentationLength = GetIncreasedIndentationLength(openToken.Parent);

                if (indentationLength == 0)
                    return;

                SyntaxTriviaList leading = first.GetLeadingTrivia();

                if (leading.Any())
                {
                    SyntaxTrivia last = leading.Last();

                    if (last.IsWhitespaceTrivia()
                        && last.Span.Length == indentationLength)
                    {
                        return;
                    }
                }

                ReportDiagnostic();
            }
            else
            {
                int indentationLength = GetIncreasedIndentationLength(openToken.Parent);

                if (indentationLength == 0)
                    return;

                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    SyntaxTriviaList trailing = (i == 0)
                        ? openToken.TrailingTrivia
                        : nodes.GetSeparator(i - 1).TrailingTrivia;

                    if (!IsOptionalWhitespaceThenOptionalSingleLineCommentThenEndOfLineTrivia(trailing))
                    {
                        if (i == nodes.Count - 1
                            && nodes[i].IsKind(SyntaxKind.Argument))
                        {
                            var argument = (ArgumentSyntax)(SyntaxNode)nodes[i];

                            if (CSharpFacts.IsAnonymousFunctionExpression(argument.Expression.Kind()))
                                break;
                        }

                        ReportDiagnostic();
                        break;
                    }

                    SyntaxTriviaList leading = nodes[i].GetLeadingTrivia();

                    if (!leading.Any())
                    {
                        ReportDiagnostic();
                        break;
                    }
                    else
                    {
                        SyntaxTrivia last = leading.Last();

                        if (!last.IsWhitespaceTrivia()
                            || indentationLength != last.Span.Length)
                        {
                            ReportDiagnostic();
                            break;
                        }
                    }
                }
            }

            void ReportDiagnostic()
            {
                DiagnosticHelpers.ReportDiagnostic(
                    context,
                    DiagnosticDescriptors.FixFormattingOfList,
                    Location.Create(first.SyntaxTree, nodes.Span));
            }
        }
    }
}
