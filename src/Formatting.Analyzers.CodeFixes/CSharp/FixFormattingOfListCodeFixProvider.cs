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
using Roslynator.CSharp;
using Roslynator.Formatting.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
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
                                ct => FixAsync(document, parameterList, parameterList.OpenParenToken, parameterList.Parameters, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case BracketedParameterListSyntax bracketedParameterList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, bracketedParameterList, bracketedParameterList.OpenBracketToken, bracketedParameterList.Parameters, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case TypeParameterListSyntax typeParameterList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, typeParameterList, typeParameterList.LessThanToken, typeParameterList.Parameters, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case ArgumentListSyntax argumentList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, argumentList, argumentList.OpenParenToken, argumentList.Arguments, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case BracketedArgumentListSyntax bracketedArgumentList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, bracketedArgumentList, bracketedArgumentList.OpenBracketToken, bracketedArgumentList.Arguments, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case AttributeArgumentListSyntax attributeArgumentList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, attributeArgumentList, attributeArgumentList.OpenParenToken, attributeArgumentList.Arguments, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case TypeArgumentListSyntax typeArgumentList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, typeArgumentList, typeArgumentList.LessThanToken, typeArgumentList.Arguments, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case AttributeListSyntax attributeList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, attributeList, attributeList.OpenBracketToken, attributeList.Attributes, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case BaseListSyntax baseList:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, baseList, baseList.ColonToken, baseList.Types, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case TupleTypeSyntax tupleType:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, tupleType, tupleType.OpenParenToken, tupleType.Elements, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case TupleExpressionSyntax tupleExpression:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, tupleExpression, tupleExpression.OpenParenToken, tupleExpression.Arguments, ct),
                                GetEquivalenceKey(diagnostic));
                        }
                    case InitializerExpressionSyntax initializerExpression:
                        {
                            return CodeAction.Create(
                                Title,
                                ct => FixAsync(document, initializerExpression, initializerExpression.OpenBraceToken, initializerExpression.Expressions, ct),
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
            SyntaxNode containingNode,
            SyntaxNodeOrToken openNodeOrToken,
            SeparatedSyntaxList<TNode> nodes,
            CancellationToken cancellationToken) where TNode : SyntaxNode
        {
            IndentationAnalysis indentationAnalysis = AnalyzeIndentation(containingNode, cancellationToken);

            string increasedIndentation = indentationAnalysis.GetIncreasedIndentation();

            SyntaxTrivia increasedIndentationTrivia = Whitespace(increasedIndentation);

            if (nodes.IsSingleLine(includeExteriorTrivia: false, cancellationToken: cancellationToken))
            {
                TNode first = nodes[0];

                SyntaxTriviaList leading = first.GetLeadingTrivia();

                SyntaxTriviaList newLeading = (leading.Any() && leading.Last().IsWhitespaceTrivia())
                    ? leading.Replace(leading.Last(), increasedIndentationTrivia)
                    : leading.Add(increasedIndentationTrivia);

                TNode newFirst = first.WithLeadingTrivia(newLeading);

                return document.ReplaceNodeAsync(first, newFirst, cancellationToken);
            }

            Dictionary<SyntaxNode, SyntaxNode> newNodes = null;
            Dictionary<SyntaxToken, SyntaxToken> newTokens = null;

            SyntaxTrivia endOfLineTrivia = DetermineEndOfLine(containingNode);

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
                TNode newNode = node;
                SyntaxTriviaList leading = node.GetLeadingTrivia();
                SyntaxTriviaList newLeading = default;
                var indentationAdded = false;

                if (IsOptionalWhitespaceThenOptionalSingleLineCommentThenEndOfLineTrivia(trailing))
                {
                    SyntaxTriviaList leadingTrivia = node.GetLeadingTrivia();
                    SyntaxTrivia last = leadingTrivia.LastOrDefault();

                    if (last.IsWhitespaceTrivia())
                    {
                        if (last.Span.Length == increasedIndentation.Length)
                            continue;

                        newLeading = leadingTrivia.Replace(leadingTrivia.Last(), increasedIndentationTrivia);
                    }
                    else
                    {
                        newLeading = leadingTrivia.Add(increasedIndentationTrivia);
                    }

                    indentationAdded = true;
                }
                else if (nodes.Count > 1
                    && (i > 0 || !containingNode.IsKind(SyntaxKind.AttributeList)))
                {
                    SyntaxTriviaList newTrailing = (trailing.Any() && trailing.Last().IsWhitespaceTrivia())
                        ? trailing.Replace(trailing.Last(), endOfLineTrivia)
                        : trailing.Add(endOfLineTrivia);

                    (newTokens ??= new Dictionary<SyntaxToken, SyntaxToken>()).Add(token, token.WithTrailingTrivia(newTrailing));

                    newLeading = leading.Insert(0, increasedIndentationTrivia);

                    indentationAdded = true;
                }

                Dictionary<SyntaxToken, SyntaxToken> newTokens2 = SetIndentation(node, indentationAdded);

                if (newTokens2 != null)
                    newNode = newNode.ReplaceTokens(newTokens2.Keys, (n, _) => newTokens2[n]);

                if (newLeading != default)
                    newNode = newNode.WithLeadingTrivia(newLeading);

                if (!object.ReferenceEquals(node, newNode))
                    (newNodes ??= new Dictionary<SyntaxNode, SyntaxNode>()).Add(node, newNode);
            }

            SyntaxNode newContainingNode = containingNode.ReplaceSyntax(
                newNodes?.Keys,
                (n, _) => newNodes[n],
                newTokens?.Keys,
                (t, _) => newTokens[t],
                default(IEnumerable<SyntaxTrivia>),
                default(Func<SyntaxTrivia, SyntaxTrivia, SyntaxTrivia>));

            return document.ReplaceNodeAsync(containingNode, newContainingNode, cancellationToken);

            Dictionary<SyntaxToken, SyntaxToken> SetIndentation(SyntaxNode node, bool indentationAdded)
            {
                ImmutableArray<IndentationInfo> indentations = FindIndentations(node, node.Span).ToImmutableArray();

                if (!indentations.Any())
                    return null;

                Dictionary<SyntaxToken, SyntaxToken> newTokens = null;

                int firstIndentationLength = indentations[0].Span.Length;

                for (int j = indentations.Length - 1; j >= 0; j--)
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
                    {
                        SyntaxTrivia newTrivia = Whitespace(replacement);

                        int spanStart = indentationInfo.Span.Start;

                        if (newTokens != null
                            && newTokens.TryGetValue(indentationInfo.Token, out SyntaxToken token))
                        {
                            spanStart -= indentationInfo.Token.FullSpan.Start;
                        }
                        else
                        {
                            token = indentationInfo.Token;
                        }

                        SyntaxTriviaList leading = token.LeadingTrivia;

                        int index = leading.IndexOf(f => f.SpanStart == spanStart);

                        SyntaxTriviaList newLeading;
                        if (indentationInfo.Span.Length > 0)
                        {
                            newLeading = leading.ReplaceAt(index, newTrivia);
                        }
                        else if (index >= 0)
                        {
                            newLeading = leading.Insert(index, newTrivia);
                        }
                        else
                        {
                            newLeading = leading.Add(newTrivia);
                        }

                        (newTokens ??= new Dictionary<SyntaxToken, SyntaxToken>())[indentationInfo.Token] = token.WithLeadingTrivia(newLeading);
                    }
                }

                return newTokens;
            }
        }
    }
}
