// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
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

            context.RegisterSyntaxNodeAction(f => AnalyzeBaseList(f), SyntaxKind.BaseList);
            context.RegisterSyntaxNodeAction(f => AnalyzeAttributeList(f), SyntaxKind.AttributeList);
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

        private void AnalyzeBaseList(SyntaxNodeAnalysisContext context)
        {
            var baseList = (BaseListSyntax)context.Node;

            Analyze(context, baseList.ColonToken, baseList.Types);
        }

        private void AnalyzeAttributeList(SyntaxNodeAnalysisContext context)
        {
            var attributeList = (AttributeListSyntax)context.Node;

            Analyze(context, attributeList.OpenBracketToken, attributeList.Attributes);
        }

        private static void Analyze<TNode>(
            SyntaxNodeAnalysisContext context,
            SyntaxToken openToken,
            SeparatedSyntaxList<TNode> nodes) where TNode : SyntaxNode
        {
            TNode first = nodes.FirstOrDefault();

            if (first == null)
                return;

            TextSpan span = nodes.GetSpan(includeExteriorTrivia: false);

            FileLinePositionSpan lineSpan = first.SyntaxTree.GetLineSpan(span);

            if (lineSpan.IsSingleLine())
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

                        if (nodes.Count == 1
                            || (i == 0
                                && context.Node.IsKind(SyntaxKind.AttributeList)
                                && first.IsMultiLine(includeExteriorTrivia: false)))
                        {
                            TextSpan span2 = first.Span;

                            int lineStartIndex = span2.Start - first.SyntaxTree.GetLineSpan(span2).StartLinePosition.Character;

                            SyntaxToken token = first.FindToken(lineStartIndex);

                            if (!token.IsKind(SyntaxKind.None))
                            {
                                SyntaxTriviaList leading2 = token.LeadingTrivia;

                                if (leading2.Any())
                                {
                                    if (leading2.FullSpan.Contains(lineStartIndex))
                                    {
                                        SyntaxTrivia trivia = leading2.Last();

                                        if (trivia.IsWhitespaceTrivia()
                                            && trivia.SpanStart == lineStartIndex
                                            && trivia.Span.Length != indentationLength)
                                        {
                                            ReportDiagnostic();
                                            break;
                                        }
                                    }
                                }
                                else if (lineStartIndex == token.SpanStart)
                                {
                                    ReportDiagnostic();
                                    break;
                                }
                            }

                            continue;
                        }
                        else
                        {
                            ReportDiagnostic();
                            break;
                        }
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
