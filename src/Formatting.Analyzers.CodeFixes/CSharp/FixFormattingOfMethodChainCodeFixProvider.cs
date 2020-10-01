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
            string endOfLineAndIndentation = DetermineEndOfLine(expression).ToString() + indentation;

            var textChanges = new List<TextChange>();
            TextLineCollection lines = expression.SyntaxTree.GetText().Lines;
            int startLine = lines.IndexOf(expression.SpanStart);

            foreach (SyntaxNode node in new MethodChain(expression))
            {
                SyntaxKind kind = node.Kind();

                if (kind == SyntaxKind.SimpleMemberAccessExpression)
                {
                    var memberAccess = (MemberAccessExpressionSyntax)node;

                    if (!SetIndentation(node, memberAccess.OperatorToken))
                        break;
                }
                else if (kind == SyntaxKind.MemberBindingExpression)
                {
                    var memberBinding = (MemberBindingExpressionSyntax)node;

                    if (!SetIndentation(node, memberBinding.OperatorToken))
                        break;
                }
            }

            return document.WithTextChangesAsync(textChanges, cancellationToken);

            bool SetIndentation(SyntaxNode node, SyntaxToken token)
            {
                SyntaxTriviaList leading = token.LeadingTrivia;
                SyntaxTriviaList.Reversed.Enumerator en = leading.Reverse().GetEnumerator();

                if (!en.MoveNext())
                {
                    int endLine = lines.IndexOf(token.SpanStart);

                    if (startLine == endLine)
                        return false;

                    string newText = (expression.FindTrivia(token.SpanStart - 1).IsEndOfLineTrivia())
                        ? indentation
                        : endOfLineAndIndentation;

                    textChanges.Add(new TextChange(new TextSpan(token.SpanStart, 0), newText));

                    return true;
                }

                SyntaxTrivia last = en.Current;

                switch (en.Current.Kind())
                {
                    case SyntaxKind.WhitespaceTrivia:
                        {
                            if (en.Current.Span.Length == indentation.Length)
                                return true;

                            if (!en.MoveNext()
                                || en.Current.IsEndOfLineTrivia())
                            {
                                if (expression.FindTrivia(token.FullSpan.Start - 1).IsEndOfLineTrivia())
                                {
                                    if (leading.IsEmptyOrWhitespace())
                                    {
                                        textChanges.Add(new TextChange(leading.Span, indentation));
                                    }
                                    else
                                    {
                                        textChanges.Add(new TextChange(last.Span, indentation));
                                    }
                                }
                            }

                            break;
                        }
                    case SyntaxKind.EndOfLineTrivia:
                        {
                            if (expression.FindTrivia(token.FullSpan.Start - 1).IsEndOfLineTrivia())
                            {
                                if (leading.IsEmptyOrWhitespace())
                                {
                                    textChanges.Add(new TextChange(leading.Span, indentation));
                                }
                                else
                                {
                                    textChanges.Add(new TextChange(last.Span, indentation));
                                }
                            }

                            break;
                        }
                }

                return true;
            }
        }
    }
}
