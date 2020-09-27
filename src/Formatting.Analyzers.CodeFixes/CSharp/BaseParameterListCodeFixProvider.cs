// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;
using Roslynator.Formatting.CSharp;

namespace Roslynator.Formatting.CodeFixes.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BaseParameterListCodeFixProvider))]
    [Shared]
    internal class BaseParameterListCodeFixProvider : BaseCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DiagnosticIdentifiers.FixFormattingOfList); }
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.GetSyntaxRootAsync().ConfigureAwait(false);

            if (!TryFindFirstAncestorOrSelf(root, context.Span, out BaseParameterListSyntax baseParameterList))
                return;

            Document document = context.Document;
            Diagnostic diagnostic = context.Diagnostics[0];

            CodeAction codeAction = CodeAction.Create(
                "Fix formatting",
                ct => FixParameterListFormattingAsync(document, baseParameterList, ct),
                GetEquivalenceKey(diagnostic));

            context.RegisterCodeFix(codeAction, diagnostic);
        }

        private static Task<Document> FixParameterListFormattingAsync(
            Document document,
            BaseParameterListSyntax baseParameterList,
            CancellationToken cancellationToken)
        {
            SeparatedSyntaxList<ParameterSyntax> nodes = baseParameterList.Parameters;

            SyntaxToken openToken = (baseParameterList is ParameterListSyntax parameterList)
                ? parameterList.OpenParenToken
                : ((BracketedParameterListSyntax)baseParameterList).OpenBracketToken;

            IndentationAnalysis indentationAnalysis = SyntaxTriviaAnalysis.AnalyzeIndentation(baseParameterList, cancellationToken);

            string increasedIndentation = indentationAnalysis.GetIncreasedIndentation();

            if (baseParameterList.SyntaxTree.IsSingleLineSpan(nodes.Span, cancellationToken))
            {
                ParameterSyntax first = nodes.First();

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

                string endOfLineAndIndentation = SyntaxTriviaAnalysis.DetermineEndOfLine(baseParameterList).ToString()
                    + increasedIndentation;

                for (int i = 0; i < nodes.Count; i++)
                {
                    SyntaxToken token = (i == 0)
                        ? openToken
                        : nodes.GetSeparator(i - 1);

                    SyntaxTriviaList trailing = token.TrailingTrivia;

                    if (!SyntaxTriviaAnalysis.IsOptionalWhitespaceThenOptionalSingleLineCommentThenEndOfLineTrivia(trailing))
                    {
                        TextSpan span = (trailing.Any() && trailing.Last().IsWhitespaceTrivia())
                            ? trailing.Last().Span
                            : new TextSpan(token.FullSpan.End, 0);

                        textChanges.Add(new TextChange(span, endOfLineAndIndentation));
                        continue;
                    }

                    SyntaxTriviaList leading = nodes[i].GetLeadingTrivia();

                    SyntaxTrivia last = (leading.Any() && leading.Last().IsWhitespaceTrivia())
                        ? leading.Last()
                        : default;

                    if (increasedIndentation.Length != last.Span.Length)
                    {
                        TextSpan span = (last.Span.Length > 0)
                            ? last.Span
                            : new TextSpan(nodes[i].SpanStart, 0);

                        textChanges.Add(new TextChange(span, increasedIndentation));
                    }
                }

                return document.WithTextChangesAsync(textChanges, cancellationToken);
            }
        }
    }
}
