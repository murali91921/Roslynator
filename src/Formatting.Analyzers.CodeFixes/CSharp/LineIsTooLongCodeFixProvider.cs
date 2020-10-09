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

            Dictionary<SyntaxKind, SyntaxNode> spans = null;

            while (position >= span.Start)
            {
                SyntaxToken token = root.FindToken(position);

                SyntaxNode node = token.Parent;

                for (; node?.SpanStart >= span.Start; node = node.Parent)
                {
                    SyntaxKind kind = node.Kind();

                    if (spans != null
                        && spans.TryGetValue(kind, out SyntaxNode node2)
                        && object.ReferenceEquals(node, node2))
                    {
                        continue;
                    }

                    if (kind == SyntaxKind.ArrowExpressionClause)
                    {
                        var expressionBody = (ArrowExpressionClauseSyntax)node;

                        SyntaxToken arrowToken = expressionBody.ArrowToken;
                        SyntaxToken previousToken = arrowToken.GetPreviousToken();

                        if (previousToken.SpanStart < span.Start)
                            continue;

                        bool addNewLineAfter = document.IsAnalyzerOptionEnabled(AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt);

                        int wrapPosition = (addNewLineAfter) ? arrowToken.Span.End : previousToken.Span.End;
                        int start = (addNewLineAfter) ? expressionBody.Expression.SpanStart : arrowToken.SpanStart;
                        int longestLength = expressionBody.GetLastToken().GetNextToken().Span.End - start;

                        if (!CanWrapNode(expressionBody, wrapPosition, longestLength))
                            continue;

                        AddSpan(expressionBody);
                        break;
                    }
                    else if (kind == SyntaxKind.ParameterList)
                    {
                        var parameterList = (ParameterListSyntax)node;

                        if (!CanWrapSeparatedList(parameterList.Parameters, parameterList.OpenParenToken.Span.End))
                            continue;

                        AddSpan(parameterList);
                        break;
                    }
                    else if (kind == SyntaxKind.BracketedParameterList)
                    {
                        var parameterList = (BracketedParameterListSyntax)node;

                        if (!CanWrapSeparatedList(parameterList.Parameters, parameterList.OpenBracketToken.Span.End))
                            continue;

                        AddSpan(parameterList);
                        break;
                    }
                    else if (kind == SyntaxKind.ArgumentList)
                    {
                        var argumentList = (ArgumentListSyntax)node;

                        if (!CanWrapSeparatedList(argumentList.Arguments, argumentList.OpenParenToken.Span.End))
                            continue;

                        if (!CanWrapLine(argumentList.Parent))
                            continue;

                        AddSpan(argumentList);
                        break;
                    }
                    else if (kind == SyntaxKind.SimpleMemberAccessExpression)
                    {
                        var memberAccessExpression = (MemberAccessExpressionSyntax)node;
                        SyntaxToken dotToken = memberAccessExpression.OperatorToken;

                        if (!CanWrapNode(memberAccessExpression, dotToken.SpanStart, span.End - dotToken.SpanStart))
                            continue;

                        if (!CanWrapLine(memberAccessExpression))
                            continue;

                        AddSpan(memberAccessExpression);
                        break;
                    }
                }

                position = Math.Min(position, token.FullSpan.Start) - 1;
            }

            if (spans == null)
                return;

            foreach (KeyValuePair<SyntaxKind, SyntaxNode> kvp in spans)
            {
                SyntaxKind kind = kvp.Key;
                SyntaxNode node = kvp.Value;

                if (kind == SyntaxKind.ArrowExpressionClause)
                {
                    var expressionBody = (ArrowExpressionClauseSyntax)node;

                    CodeAction codeAction = CodeAction.Create(
                        Title,
                        ct => AddNewLineBeforeOrAfterEpressionBodyArrowAsync(document, expressionBody.ArrowToken, ct),
                        GetEquivalenceKey(diagnostic));

                    context.RegisterCodeFix(codeAction, diagnostic);
                    return;
                }
                else if (kind == SyntaxKind.ParameterList)
                {
                    var parameterList = (ParameterListSyntax)node;

                    CodeAction codeAction = CodeAction.Create(
                        Title,
                        ct => SyntaxFormatter.WrapParametersAsync(document, parameterList, ct),
                        GetEquivalenceKey(diagnostic));

                    context.RegisterCodeFix(codeAction, diagnostic);
                    return;
                }
                else if (kind == SyntaxKind.BracketedParameterList)
                {
                    var parameterList = (BracketedParameterListSyntax)node;

                    CodeAction codeAction = CodeAction.Create(
                        Title,
                        ct => SyntaxFormatter.WrapParametersAsync(document, parameterList, ct),
                        GetEquivalenceKey(diagnostic));

                    context.RegisterCodeFix(codeAction, diagnostic);
                    return;
                }
                else if (kind == SyntaxKind.SimpleMemberAccessExpression)
                {
                    var memberAccessExpression = (MemberAccessExpressionSyntax)node;

                    CodeAction codeAction = CodeAction.Create(
                        Title,
                        ct => SyntaxFormatter.WrapCallChainAsync(document, memberAccessExpression, ct),
                        GetEquivalenceKey(diagnostic));

                    context.RegisterCodeFix(codeAction, diagnostic);
                    return;
                }
                else if (kind == SyntaxKind.ArgumentList)
                {
                    var argumentList = (ArgumentListSyntax)node;

                    CodeAction codeAction = CodeAction.Create(
                        Title,
                        ct => SyntaxFormatter.WrapArgumentsAsync(document, argumentList, ct),
                        GetEquivalenceKey(diagnostic));

                    context.RegisterCodeFix(codeAction, diagnostic);
                    return;
                }
            }

            void AddSpan(SyntaxNode node)
            {
                SyntaxKind kind = node.Kind();

                if (spans == null)
                    spans = new Dictionary<SyntaxKind, SyntaxNode>();

                if (!spans.ContainsKey(kind))
                    spans[kind] = node;
            }

            bool CanWrapSeparatedList<TNode>(
                SeparatedSyntaxList<TNode> nodes,
                int wrapPosition) where TNode : SyntaxNode
            {
                if (!nodes.Any())
                    return false;

                int longestLength = nodes.Max(f => f.Span.Length);

                return CanWrapNode(nodes.First(), wrapPosition, longestLength);
            }

            bool CanWrapNode(
                SyntaxNode node,
                int wrapPosition,
                int longestLength)
            {
                if (wrapPosition - span.Start > maxLength)
                    return false;

                indentation = SyntaxTriviaAnalysis.GetIncreasedIndentation(node);

                return indentation.Length + longestLength <= maxLength;
            }

            static bool CanWrapLine(SyntaxNode node)
            {
                for (SyntaxNode n = node; n != null; n = n.Parent)
                {
                    switch (n)
                    {
                        case MemberDeclarationSyntax _:
                        case StatementSyntax _:
                        case AccessorDeclarationSyntax _:
                            return true;
                        case InterpolationSyntax _:
                            return false;
                    }
                }

                return true;
            }
        }

        private static Task<Document> AddNewLineBeforeOrAfterEpressionBodyArrowAsync(
            Document document,
            SyntaxToken token,
            CancellationToken cancellationToken = default)
        {
            bool addNewLineAfter = document.IsAnalyzerOptionEnabled(AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt);

            string indentation = SyntaxTriviaAnalysis.GetIncreasedIndentation(token.Parent, cancellationToken);

            return (addNewLineAfter)
                ? CodeFixHelpers.AddNewLineAfterAsync(document, token, indentation, cancellationToken)
                : CodeFixHelpers.AddNewLineBeforeAsync(document, token, indentation, cancellationToken);
        }
    }
}

