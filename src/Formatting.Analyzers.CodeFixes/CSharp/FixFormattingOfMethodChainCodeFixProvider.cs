// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FixFormattingOfMethodChainCodeFixProvider))]
    [Shared]
    internal class FixFormattingOfMethodChainCodeFixProvider : BaseCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DiagnosticIdentifiers.FixFormattingOfMethodChain); }
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.GetSyntaxRootAsync().ConfigureAwait(false);

            if (!TryFindFirstAncestorOrSelf(
                root,
                context.Span,
                out SyntaxNode node,
                predicate: f => f.IsKind(
                    SyntaxKind.InvocationExpression,
                    SyntaxKind.ElementAccessExpression,
                    SyntaxKind.ConditionalAccessExpression)))
            {
                return;
            }

            Document document = context.Document;
            Diagnostic diagnostic = context.Diagnostics[0];

            CodeAction codeAction = CodeAction.Create(
                "Fix formatting",
                ct => FixAsync(document, (ExpressionSyntax)node, ct),
                GetEquivalenceKey(diagnostic));

            context.RegisterCodeFix(codeAction, diagnostic);
        }

        private static Task<Document> FixAsync(
            Document document,
            ExpressionSyntax expression,
            CancellationToken cancellationToken)
        {
            IndentationAnalysis indentationAnalysis = AnalyzeIndentation(expression, cancellationToken);

            string indentation = indentationAnalysis.GetIncreasedIndentation();

            var textChanges = new List<TextChange>();

            foreach (SyntaxNode node in CSharpUtility.EnumerateExpressionChain(expression))
            {
                switch (node.Kind())
                {
                    case SyntaxKind.SimpleMemberAccessExpression:
                        {
                            var memberAccess = (MemberAccessExpressionSyntax)node;

                            AnalyzeToken(node, memberAccess.OperatorToken);
                            break;
                        }
                    case SyntaxKind.MemberBindingExpression:
                        {
                            var memberBinding = (MemberBindingExpressionSyntax)node;

                            AnalyzeToken(node, memberBinding.OperatorToken);
                            break;
                        }
                }
            }

            return document.WithTextChangesAsync(textChanges, cancellationToken);

            void AnalyzeToken(SyntaxNode node, SyntaxToken token)
            {
                int start = 0;
                int end = 0;

                SyntaxTriviaList leading = token.LeadingTrivia;

                if (leading.Any())
                {
                    SyntaxTrivia trivia = leading.Last();

                    if (trivia.IsWhitespaceTrivia())
                    {
                        if (leading.Count == 1
                            && trivia.Span.Length == indentation.Length
                            && node.FindTrivia(trivia.SpanStart - 1).IsEndOfLineTrivia())
                        {
                            return;
                        }

                        end = trivia.Span.End;
                    }
                    else
                    {
                        end = token.SpanStart;
                    }

                    if (leading.IsEmptyOrWhitespace())
                    {
                        start = leading.Span.Start;
                    }
                    else
                    {
                        start = trivia.SpanStart;
                    }
                }
                else
                {
                    start = token.SpanStart;
                    end = start;
                }

                textChanges.Add(new TextChange(TextSpan.FromBounds(start, end), indentation));
            }
        }
    }
}
