// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;
using Roslynator.Formatting.CSharp;
using static Roslynator.CSharp.SyntaxTriviaAnalysis;

namespace Roslynator.Formatting.CodeFixes.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FixFormattingOfListCodeFixProvider))]
    [Shared]
    internal class FixFormattingOfListCodeFixProvider : BaseCodeFixProvider
    {
        private const string Title = "Fix formatting";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DiagnosticIdentifiers.FixFormattingOfList); }
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.GetSyntaxRootAsync().ConfigureAwait(false);

            if (!TryFindFirstAncestorOrSelf(
                root,
                context.Span,
                out SyntaxNode node,
                predicate: f =>
                {
                    switch (f.Kind())
                    {
                        case SyntaxKind.ParameterList:
                        case SyntaxKind.BracketedParameterList:
                        case SyntaxKind.TypeParameterList:
                        case SyntaxKind.ArgumentList:
                        case SyntaxKind.BracketedArgumentList:
                        case SyntaxKind.AttributeArgumentList:
                        case SyntaxKind.TypeArgumentList:
                        case SyntaxKind.AttributeList:
                        case SyntaxKind.BaseList:
                        case SyntaxKind.TupleType:
                        case SyntaxKind.TupleExpression:
                        case SyntaxKind.VariableDeclaration:
                        case SyntaxKind.ArrayInitializerExpression:
                        case SyntaxKind.CollectionInitializerExpression:
                        case SyntaxKind.ComplexElementInitializerExpression:
                        case SyntaxKind.ObjectInitializerExpression:
                            return true;
                        default:
                            return false;
                    }
                }))
            {
                return;
            }

            Document document = context.Document;
            Diagnostic diagnostic = context.Diagnostics[0];

            CodeAction codeAction = CreateCodeAction();

            context.RegisterCodeFix(codeAction, diagnostic);

            CodeAction CreateCodeAction()
            {
                switch (node)
                {
                    case ParameterListSyntax parameterList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, parameterList, parameterList.Parameters, parameterList.OpenParenToken, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case BracketedParameterListSyntax bracketedParameterList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, bracketedParameterList, bracketedParameterList.Parameters, bracketedParameterList.OpenBracketToken, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case TypeParameterListSyntax typeParameterList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, typeParameterList, typeParameterList.Parameters, typeParameterList.LessThanToken, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case ArgumentListSyntax argumentList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, argumentList, argumentList.Arguments, argumentList.OpenParenToken, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case BracketedArgumentListSyntax bracketedArgumentList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, bracketedArgumentList, bracketedArgumentList.Arguments, bracketedArgumentList.OpenBracketToken, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case AttributeArgumentListSyntax attributeArgumentList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, attributeArgumentList, attributeArgumentList.Arguments, attributeArgumentList.OpenParenToken, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case TypeArgumentListSyntax typeArgumentList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, typeArgumentList, typeArgumentList.Arguments, typeArgumentList.LessThanToken, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case AttributeListSyntax attributeList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, attributeList, attributeList.Attributes, attributeList.OpenBracketToken, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case BaseListSyntax baseList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, baseList, baseList.Types, baseList.ColonToken, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case TupleTypeSyntax tupleType:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, tupleType, tupleType.Elements, tupleType.OpenParenToken, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case TupleExpressionSyntax tupleExpression:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, tupleExpression, tupleExpression.Arguments, tupleExpression.OpenParenToken, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case VariableDeclarationSyntax variableDeclaration:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, variableDeclaration, variableDeclaration.Variables, variableDeclaration.Type, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case InitializerExpressionSyntax initializerExpression:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, initializerExpression, initializerExpression.Expressions, initializerExpression.OpenBraceToken, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    default:
                        {
                            throw new InvalidOperationException();
                        }
                }
            }
        }

        private static Task<Document> FixAsync<TNode>(
            Document document,
            SyntaxNode list,
            SeparatedSyntaxList<TNode> nodes,
            SyntaxNodeOrToken openNodeOrToken,
            CancellationToken cancellationToken) where TNode : SyntaxNode
        {
            IndentationAnalysis indentationAnalysis = AnalyzeIndentation(list, cancellationToken);

            string increasedIndentation = indentationAnalysis.GetIncreasedIndentation();

            if (nodes.IsSingleLine(includeExteriorTrivia: false, cancellationToken: cancellationToken))
            {
                TNode first = nodes.First();

                SyntaxTriviaList leading = first.GetLeadingTrivia();

                TextSpan span = (leading.Any() && leading.Last().IsWhitespaceTrivia())
                    ? leading.Last().Span
                    : new TextSpan(first.SpanStart, 0);

                var textChange = new TextChange(span, increasedIndentation);

                return document.WithTextChangeAsync(textChange, cancellationToken);
            }

            var textChanges = new List<TextChange>();

            string endOfLineAndIndentation = DetermineEndOfLine(list).ToString()
                + increasedIndentation;

            for (int i = 0; i < nodes.Count; i++)
            {
                SyntaxToken token;
                if (i == 0)
                {
                    token = (openNodeOrToken.IsNode)
                        ? openNodeOrToken.AsNode().GetLastToken()
                        : openNodeOrToken.AsToken();
                }
                else
                {
                    token = nodes.GetSeparator(i - 1);
                }

                SyntaxTriviaList trailing = token.TrailingTrivia;

                TNode node = nodes[i];

                var indentationAdded = false;

                if (!IsOptionalWhitespaceThenOptionalSingleLineCommentThenEndOfLineTrivia(trailing))
                {
                    if (nodes.Count > 1
                        && (i > 0 || !list.IsKind(SyntaxKind.AttributeList)))
                    {
                        TextSpan span = (trailing.Any() && trailing.Last().IsWhitespaceTrivia())
                            ? trailing.Last().Span
                            : new TextSpan(token.FullSpan.End, 0);

                        textChanges.Add(new TextChange(span, endOfLineAndIndentation));
                        indentationAdded = true;
                    }
                }
                else
                {
                    TextSpan span;

                    SyntaxTrivia last = node.GetLeadingTrivia().LastOrDefault();

                    if (last.IsWhitespaceTrivia())
                    {
                        if (last.Span.Length == increasedIndentation.Length)
                            continue;

                        span = last.Span;
                    }
                    else
                    {
                        span = new TextSpan(node.SpanStart, 0);
                    }

                    textChanges.Add(new TextChange(span, increasedIndentation));
                    indentationAdded = true;
                }

                ImmutableArray<IndentationInfo> indentations = FindIndentations(node, node.Span).ToImmutableArray();

                if (indentations.Any())
                {
                    int firstIndentationLength = indentations[0].Span.Length;

                    for (int j = 0; j < indentations.Length; j++)
                    {
                        IndentationInfo indentationInfo = indentations[j];

                        if (indentationAdded
                            && node is ArgumentSyntax argument
                            && CSharpFacts.IsAnonymousFunctionExpression(argument.Expression.Kind()))
                        {
                            indentationAdded = false;
                        }

                        string replacement = increasedIndentation;

                        if (indentationAdded)
                            replacement += indentationAnalysis.GetSingleIndentation();

                        if (j > 0
                            && indentationInfo.Span.Length > firstIndentationLength)
                        {
                            replacement += indentationInfo.ToString().Substring(firstIndentationLength);
                        }

                        if (indentationInfo.Span.Length != replacement.Length)
                            textChanges.Add(new TextChange(indentationInfo.Span, replacement));
                    }
                }
            }

            return document.WithTextChangesAsync(textChanges, cancellationToken);
        }
    }
}
