// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
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

            switch (node)
            {
                case ParameterListSyntax parameterList:
                    {
                        CodeAction codeAction = CodeAction.Create(
                            Title,
                            ct => FixAsync(document, parameterList, parameterList.Parameters, parameterList.OpenParenToken, ct),
                            GetEquivalenceKey(diagnostic));

                        context.RegisterCodeFix(codeAction, diagnostic);
                        break;
                    }
                case BracketedParameterListSyntax bracketedParameterList:
                    {
                        CodeAction codeAction = CodeAction.Create(
                            Title,
                            ct => FixAsync(document, bracketedParameterList, bracketedParameterList.Parameters, bracketedParameterList.OpenBracketToken, ct),
                            GetEquivalenceKey(diagnostic));

                        context.RegisterCodeFix(codeAction, diagnostic);
                        break;
                    }
                case TypeParameterListSyntax typeParameterList:
                    {
                        CodeAction codeAction = CodeAction.Create(
                            Title,
                            ct => FixAsync(document, typeParameterList, typeParameterList.Parameters, typeParameterList.LessThanToken, ct),
                            GetEquivalenceKey(diagnostic));

                        context.RegisterCodeFix(codeAction, diagnostic);
                        break;
                    }
                case ArgumentListSyntax argumentList:
                    {
                        CodeAction codeAction = CodeAction.Create(
                            Title,
                            ct => FixAsync(document, argumentList, argumentList.Arguments, argumentList.OpenParenToken, ct),
                            GetEquivalenceKey(diagnostic));

                        context.RegisterCodeFix(codeAction, diagnostic);
                        break;
                    }
                case BracketedArgumentListSyntax bracketedArgumentList:
                    {
                        CodeAction codeAction = CodeAction.Create(
                            Title,
                            ct => FixAsync(document, bracketedArgumentList, bracketedArgumentList.Arguments, bracketedArgumentList.OpenBracketToken, ct),
                            GetEquivalenceKey(diagnostic));

                        context.RegisterCodeFix(codeAction, diagnostic);
                        break;
                    }
                case AttributeArgumentListSyntax attributeArgumentList:
                    {
                        CodeAction codeAction = CodeAction.Create(
                            Title,
                            ct => FixAsync(document, attributeArgumentList, attributeArgumentList.Arguments, attributeArgumentList.OpenParenToken, ct),
                            GetEquivalenceKey(diagnostic));

                        context.RegisterCodeFix(codeAction, diagnostic);
                        break;
                    }
                case TypeArgumentListSyntax typeArgumentList:
                    {
                        CodeAction codeAction = CodeAction.Create(
                            Title,
                            ct => FixAsync(document, typeArgumentList, typeArgumentList.Arguments, typeArgumentList.LessThanToken, ct),
                            GetEquivalenceKey(diagnostic));

                        context.RegisterCodeFix(codeAction, diagnostic);
                        break;
                    }
            }
        }

        private static Task<Document> FixAsync<TNode>(
            Document document,
            SyntaxNode list,
            SeparatedSyntaxList<TNode> nodes,
            SyntaxToken openToken,
            CancellationToken cancellationToken) where TNode : SyntaxNode
        {
            IndentationAnalysis indentationAnalysis = AnalyzeIndentation(list, cancellationToken);

            string increasedIndentation = indentationAnalysis.GetIncreasedIndentation();

            if (nodes.IsSingleLine(includeExteriorTrivia: false, cancellationToken:  cancellationToken))
            {
                TNode first = nodes.First();

                SyntaxTriviaList leading = first.GetLeadingTrivia();

                TextSpan span = (leading.Any() && leading.Last().IsWhitespaceTrivia())
                    ? leading.Last().Span
                    : new TextSpan(first.SpanStart, 0);

                var textChange = new TextChange(span, increasedIndentation);

                return document.WithTextChangeAsync(textChange, cancellationToken);
            }
            else
            {
                var textChanges = new List<TextChange>();

                string endOfLineAndIndentation = DetermineEndOfLine(list).ToString()
                    + increasedIndentation;

                for (int i = 0; i < nodes.Count; i++)
                {
                    SyntaxToken token = (i == 0)
                        ? openToken
                        : nodes.GetSeparator(i - 1);

                    SyntaxTriviaList trailing = token.TrailingTrivia;

                    TNode node = nodes[i];

                    if (!IsOptionalWhitespaceThenOptionalSingleLineCommentThenEndOfLineTrivia(trailing))
                    {
                        TextSpan span = (trailing.Any() && trailing.Last().IsWhitespaceTrivia())
                            ? trailing.Last().Span
                            : new TextSpan(token.FullSpan.End, 0);

                        textChanges.Add(new TextChange(span, endOfLineAndIndentation));
                    }
                    else
                    {
                        SyntaxTriviaList leading = node.GetLeadingTrivia();

                        SyntaxTrivia last = (leading.Any() && leading.Last().IsWhitespaceTrivia())
                            ? leading.Last()
                            : default;

                        if (increasedIndentation.Length != last.Span.Length)
                        {
                            TextSpan span = (last.Span.Length > 0)
                                ? last.Span
                                : new TextSpan(node.SpanStart, 0);

                            textChanges.Add(new TextChange(span, increasedIndentation));
                        }
                    }

                    IndentationChange indentationChange = GetIndentationChange(node, list);

                    if (!indentationChange.IsEmpty)
                    {
                        int length = indentationChange.Indentations[0].Span.Length;

                        foreach (IndentationInfo indentationInfo in indentationChange.Indentations)
                        {
                            string replacement = indentationChange.Replacement;

                            if (indentationInfo.Span.Length > length)
                                replacement += indentationInfo.ToString().Substring(length);

                            textChanges.Add(new TextChange(indentationInfo.Span, replacement));
                        }
                    }
                }

                return document.WithTextChangesAsync(textChanges, cancellationToken);
            }
        }
    }
}
