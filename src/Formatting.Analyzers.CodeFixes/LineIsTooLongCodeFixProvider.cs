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

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIdentifiers.LineIsTooLong);

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

                        if (expressionBody != null
                            && span.Contains(expressionBody.Span))
                        {
                            SemanticModel semanticModel = await context.GetSemanticModelAsync().ConfigureAwait(false);

                            bool addNewLineAfterArrow = !semanticModel.Compilation.IsAnalyzerSuppressed(DiagnosticDescriptors.AddNewLineBeforeExpressionBodyArrowInsteadOfAfterItOrViceVersa)
                                && !semanticModel.Compilation.IsAnalyzerSuppressed(AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt);

                            int end = (addNewLineAfterArrow)
                                ? expressionBody.ArrowToken.Span.End
                                : expressionBody.ArrowToken.Span.Start;

                            if (end - span.Start <= maxLength)
                            {
                                CodeAction codeAction = CodeAction.Create(
                                    Title,
                                    ct => WrapExpressionBodyAsync(document, expressionBody, addNewLineAfterArrow, ct),
                                    GetEquivalenceKey(diagnostic));

                                context.RegisterCodeFix(codeAction, diagnostic);
                            }
                        }

                        break;
                    }
            }

            SyntaxNode baseNode = root.FindNode(new TextSpan(span.End - 1, 0), findInsideTrivia: true, getInnermostNodeForTie: true);

            foreach (SyntaxNode node in baseNode.AncestorsAndSelf())
            {
            }
        }

        private Task<Document> WrapExpressionBodyAsync(
            Document document,
            ArrowExpressionClauseSyntax expressionBody,
            bool addNewLineAfterArrow,
            CancellationToken cancellationToken)
        {
            if (addNewLineAfterArrow)
            {
                return CodeFixHelpers.AddNewLineAfterAndIncreaseIndentationAsync(document, expressionBody.ArrowToken, cancellationToken);
            }
            else
            {
                return CodeFixHelpers.AddNewLineBeforeAndIncreaseIndentationAsync(document, expressionBody.ArrowToken, cancellationToken);
            }
        }
    }
}

