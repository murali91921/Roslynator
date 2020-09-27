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

        //TODO: last argument is anonymous function
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

                for (int i = 0; i < nodes.Count; i++)
                {
                    SyntaxTriviaList trailing = (i == 0)
                        ? openToken.TrailingTrivia
                        : nodes.GetSeparator(i - 1).TrailingTrivia;

                    if (!IsOptionalWhitespaceThenOptionalSingleLineCommentThenEndOfLineTrivia(trailing))
                    {
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
