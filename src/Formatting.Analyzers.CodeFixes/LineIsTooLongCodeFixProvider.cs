// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
using Roslynator.Formatting.CodeFixes.CSharp;
using Roslynator.Formatting.CSharp;

namespace Roslynator.Formatting.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LineIsTooLongCodeFixProvider))]
    [Shared]
    internal class LineIsTooLongCodeFixProvider : BaseCodeFixProvider
    {
        private const string Title = "Wrap line";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DiagnosticIdentifiers.LineIsTooLong); }
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.GetSyntaxRootAsync().ConfigureAwait(false);

            TextSpan span = context.Span;
            Document document = context.Document;
            Diagnostic diagnostic = context.Diagnostics[0];

            int maxLength = AnalyzerSettings.Current.MaxLineLength;

            SyntaxToken token = root.FindToken(span.End);

            switch (token.Kind())
            {
                case SyntaxKind.SemicolonToken:
                    {
                        ArrowExpressionClauseSyntax expressionBody = CSharpUtility.GetExpressionBody(token.Parent);

                        if (expressionBody != null)
                        {
                            SyntaxToken arrowToken = expressionBody.ArrowToken;

                            if (span.Contains(TextSpan.FromBounds(arrowToken.GetPreviousToken().SpanStart, token.SpanStart)))
                            {
                                bool addNewLineAfterArrow = !document.Project.CompilationOptions.IsAnalyzerSuppressed(DiagnosticDescriptors.AddNewLineBeforeExpressionBodyArrowInsteadOfAfterItOrViceVersa)
                                    && !document.Project.CompilationOptions.IsAnalyzerSuppressed(AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt);

                                RegisterCodeFix(token.Parent, expressionBody.Expression, arrowToken, token, addNewLineAfterArrow);
                            }
                        }

                        break;
                    }
            }

            SyntaxNode baseNode = root.FindNode(new TextSpan(span.End - 1, 0), findInsideTrivia: true, getInnermostNodeForTie: true);

            foreach (SyntaxNode node in baseNode.AncestorsAndSelf())
            {
            }

            void RegisterCodeFix(
                SyntaxNode declaration,
                ExpressionSyntax expression,
                SyntaxToken token,
                SyntaxToken semicolonToken,
                bool addNewLineAfter)
            {
                int end = (addNewLineAfter) ? token.Span.End : token.SpanStart;

                if (end - span.Start <= maxLength)
                {
                    IndentationAnalysis analysis = SyntaxTriviaAnalysis.AnalyzeIndentation(declaration);

                    string indentation = analysis.GetIncreasedIndentation();

                    int start = (addNewLineAfter) ? expression.SpanStart : token.SpanStart;

                    var newLength = indentation.Length + semicolonToken.Span.End - start;

                    if (newLength <= maxLength)
                    {
                        CodeAction codeAction = CodeAction.Create(
                            Title,
                            ct => WrapLineBeforeOrAfterTokenAsync(document, token, addNewLineAfter, indentation, ct),
                            GetEquivalenceKey(diagnostic));

                        context.RegisterCodeFix(codeAction, diagnostic);
                    }

                }
            }
        }

        private Task<Document> WrapLineBeforeOrAfterTokenAsync(
            Document document,
            SyntaxToken token,
            bool addNewLineAfter,
            string indentation,
            CancellationToken cancellationToken)
        {
            if (addNewLineAfter)
            {
                return CodeFixHelpers.AddNewLineAfterAsync(document, token, indentation, cancellationToken);
            }
            else
            {
                return CodeFixHelpers.AddNewLineBeforeAsync(document, token, indentation, cancellationToken);
            }
        }
    }
}

