// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
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

                                if (arrowToken.GetPreviousToken().SpanStart >= span.Start)
                                {
                                    bool addNewLineAfterArrow = document.IsAnalyzerOptionEnabled(
                                        AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt);

                                    if (RegisterCodeFix(token.Parent, expressionBody.Expression, arrowToken, token, addNewLineAfterArrow))
                                        return;
                                }

                                break;
                            }
                    }

                    node = node.Parent;
                }

                position--;
            }

            bool RegisterCodeFix(
                SyntaxNode declaration,
                ExpressionSyntax expression,
                SyntaxToken token,
                SyntaxToken semicolonToken,
                bool addNewLineAfter)
            {
                int end = (addNewLineAfter) ? token.Span.End : token.SpanStart;

                if (end - span.Start > maxLength)
                    return false;

                IndentationAnalysis analysis = SyntaxTriviaAnalysis.AnalyzeIndentation(declaration);

                string indentation = analysis.GetIncreasedIndentation();

                int start = (addNewLineAfter) ? expression.SpanStart : token.SpanStart;

                int newLength = indentation.Length + semicolonToken.Span.End - start;

                if (newLength > maxLength)
                    return false;

                CodeAction codeAction = CodeAction.Create(
                    Title,
                    ct =>
                    {
                        return (addNewLineAfter)
                            ? CodeFixHelpers.AddNewLineAfterAsync(document, token, indentation, ct)
                            : CodeFixHelpers.AddNewLineBeforeAsync(document, token, indentation, ct);
                    },
                    GetEquivalenceKey(diagnostic));

                context.RegisterCodeFix(codeAction, diagnostic);
                return true;
            }
        }
    }
}

