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
                                bool addNewLineAfterArrow = AnalyzerOptions.IsEnabled(
                                    document.Project.CompilationOptions,
                                    AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt);

                                if (RegisterCodeFix(token.Parent, expressionBody.Expression, arrowToken, token, addNewLineAfterArrow))
                                    return;
                            }
                        }

                        break;
                    }
            }

            SyntaxNode baseNode = root.FindNode(new TextSpan(span.End - 1, 0), findInsideTrivia: true, getInnermostNodeForTie: true);

            foreach (SyntaxNode node in baseNode.AncestorsAndSelf())
            {
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

