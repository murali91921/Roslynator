// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;
using Roslynator.Formatting.CodeFixes.CSharp;
using Roslynator.Formatting.CSharp;

namespace Roslynator.Formatting.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LineIsTooLongCodeFixProvider))]
    [Shared]
    internal class LineIsTooLongCodeFixProvider : BaseCodeFixProvider
    {
        private const string Title = "Wrap line";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIdentifiers.LineIsTooLong);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.GetSyntaxRootAsync().ConfigureAwait(false);

            TextSpan span = context.Span;
            Document document = context.Document;
            Diagnostic diagnostic = context.Diagnostics[0];
            string indentation = null;
            int maxLength = AnalyzerSettings.Current.MaxLineLength;
            int position = span.End;

            while (position >= span.Start)
            {
                SyntaxToken token = root.FindToken(position);

                SyntaxNode node = token.Parent;

                while (node?.SpanStart >= span.Start)
                {
                    switch (node.Kind())
                    {
                        case SyntaxKind.ArrowExpressionClause:
                            {
                                var expressionBody = (ArrowExpressionClauseSyntax)node;

                                SyntaxToken arrowToken = expressionBody.ArrowToken;
                                SyntaxToken previousToken = arrowToken.GetPreviousToken();

                                if (previousToken.SpanStart < span.Start)
                                    break;

                                bool addNewLineAfter = document.IsAnalyzerOptionEnabled(AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt);

                                int wrapPosition = (addNewLineAfter) ? arrowToken.Span.End : previousToken.Span.End;
                                int start = (addNewLineAfter) ? expressionBody.Expression.SpanStart : arrowToken.SpanStart;
                                int longestLength = expressionBody.GetLastToken().GetNextToken().Span.End - start;

                                if (!CanWrapLine(expressionBody, wrapPosition, longestLength))
                                    break;

                                CodeAction codeAction = CodeAction.Create(
                                    Title,
                                    ct =>
                                    {
                                        return (addNewLineAfter)
                                            ? CodeFixHelpers.AddNewLineAfterAsync(document, arrowToken, indentation, ct)
                                            : CodeFixHelpers.AddNewLineBeforeAsync(document, arrowToken, indentation, ct);
                                    },
                                    GetEquivalenceKey(diagnostic));

                                context.RegisterCodeFix(codeAction, diagnostic);
                                return;
                            }
                        case SyntaxKind.ParameterList:
                            {
                                var parameterList = (ParameterListSyntax)node;

                                if (!CanWrapSeparatedList(parameterList.Parameters, parameterList.OpenParenToken.Span.End))
                                    break;

                                CodeAction codeAction = CodeAction.Create(
                                    Title,
                                    ct => SyntaxFormatter.WrapParametersAsync(document, parameterList, ct),
                                    GetEquivalenceKey(diagnostic));

                                context.RegisterCodeFix(codeAction, diagnostic);
                                return;
                            }
                        case SyntaxKind.BracketedParameterList:
                            {
                                var parameterList = (BracketedParameterListSyntax)node;

                                if (!CanWrapSeparatedList(parameterList.Parameters, parameterList.OpenBracketToken.Span.End))
                                    break;

                                CodeAction codeAction = CodeAction.Create(
                                    Title,
                                    ct => SyntaxFormatter.WrapParametersAsync(document, parameterList, ct),
                                    GetEquivalenceKey(diagnostic));

                                context.RegisterCodeFix(codeAction, diagnostic);
                                return;
                            }
                    }

                    node = node.Parent;
                }

                position = Math.Min(position, token.FullSpan.Start) - 1;
            }

            bool CanWrapSeparatedList<TNode>(
                SeparatedSyntaxList<TNode> nodes,
                int wrapPosition) where TNode : SyntaxNode
            {
                if (!nodes.Any())
                    return false;

                int longestLength = nodes.Max(f => f.Span.Length);

                return CanWrapLine(nodes.First(), wrapPosition, longestLength);
            }

            bool CanWrapLine(
                SyntaxNode node,
                int wrapPosition,
                int longestLength)
            {
                if (wrapPosition - span.Start > maxLength)
                    return false;

                indentation = SyntaxTriviaAnalysis.GetIncreasedIndentation(node);

                return indentation.Length + longestLength <= maxLength;
            }
        }
    }
}

