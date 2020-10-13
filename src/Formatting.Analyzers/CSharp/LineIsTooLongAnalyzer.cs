// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;
using Roslynator.Formatting.CSharp;

namespace Roslynator.Formatting
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class LineIsTooLongAnalyzer : BaseDiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(DiagnosticDescriptors.LineIsTooLong); }
        }

        public override void Initialize(AnalysisContext context)
        {
            base.Initialize(context);

            context.RegisterSyntaxTreeAction(f => AnalyzeSyntaxTree(f));
        }

        private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
        {
            SyntaxTree tree = context.Tree;

            if (!tree.TryGetText(out SourceText sourceText))
                return;

            SyntaxNode root = tree.GetRoot(context.CancellationToken);

            int i = 0;

            SyntaxTrivia trivia = root.FindTrivia(0);

            if (trivia.SpanStart == 0
                && trivia.IsKind(SyntaxKind.SingleLineCommentTrivia, SyntaxKind.MultiLineCommentTrivia))
            {
                SyntaxTriviaList leadingTrivia = trivia.Token.LeadingTrivia;

                int count = leadingTrivia.Count;

                if (count > 1)
                {
                    int j = 0;

                    while (j < leadingTrivia.Count - 1
                        && leadingTrivia[j].IsKind(SyntaxKind.SingleLineCommentTrivia, SyntaxKind.MultiLineCommentTrivia)
                        && leadingTrivia[j + 1].IsKind(SyntaxKind.EndOfLineTrivia))
                    {
                        i++;

                        j += 2;
                    }
                }
            }

            TextLineCollection lines = sourceText.Lines;
            int maxLength = AnalyzerSettings.Current.MaxLineLength;

            for (; i < lines.Count; i++)
            {
                if (lines[i].Span.Length <= maxLength)
                    continue;

                if (root.FindTrivia(lines[i].End).IsKind(
                    SyntaxKind.SingleLineDocumentationCommentTrivia,
                    SyntaxKind.MultiLineDocumentationCommentTrivia))
                {
                    continue;
                }

                DiagnosticHelpers.ReportDiagnostic(
                    context,
                    DiagnosticDescriptors.LineIsTooLong,
                    Location.Create(tree, lines[i].Span));
            }
        }
    }
}
