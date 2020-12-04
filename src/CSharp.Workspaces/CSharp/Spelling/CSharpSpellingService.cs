// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host;
using Roslynator.Spelling;
using Microsoft.CodeAnalysis.Text;

namespace Roslynator.CSharp.Spelling
{
    [Export(typeof(ILanguageService))]
    [ExportMetadata("Language", LanguageNames.CSharp)]
    [ExportMetadata("ServiceType", "Roslynator.Spelling.ISpellingService")]
    internal class CSharpSpellingService : SpellingService
    {
        public override ISyntaxFactsService SyntaxFacts => CSharpSyntaxFactsService.Instance;

        public override SpellingAnalysisResult AnalyzeSpelling(
            SyntaxNode node,
            SpellingData spellingData,
            SpellingFixerOptions options,
            CancellationToken cancellationToken)
        {
            var walker = new CSharpSpellingWalker(spellingData, options, cancellationToken);

            walker.Visit(node);

            return new SpellingAnalysisResult(walker.Errors);
        }

        public override SpellingError CreateErrorFromDiagnostic(Diagnostic diagnostic)
        {
            Location location = diagnostic.Location;

            SyntaxTree syntaxTree = location.SourceTree;

            SyntaxNode root = syntaxTree.GetRoot();

            TextSpan span = location.SourceSpan;

            SyntaxTrivia trivia = root.FindTrivia(span.Start, findInsideTrivia: true);

            if (trivia.IsKind(
                SyntaxKind.SingleLineCommentTrivia,
                SyntaxKind.MultiLineCommentTrivia,
                SyntaxKind.PreprocessingMessageTrivia))
            {
                string triviaText = trivia.ToString();

                string value = triviaText.Substring(span.Start - trivia.SpanStart, span.Length);

                return new CSharpSpellingError(value, value, location, 0);
            }

            SyntaxToken token = root.FindToken(span.Start, findInsideTrivia: true);

            if (token.IsKind(
                SyntaxKind.IdentifierToken,
                SyntaxKind.XmlTextLiteralToken))
            {
                string text = token.ToString();

                int index = span.Start - token.SpanStart;

                string value = text.Substring(index, span.Length);

                return new CSharpSpellingError(value, value, location, index, (token.IsKind(SyntaxKind.IdentifierToken)) ? token : default);
            }

            return null;
        }
    }
}
