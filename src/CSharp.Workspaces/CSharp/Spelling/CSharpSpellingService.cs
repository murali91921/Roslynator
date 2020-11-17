// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Roslynator.Spelling;

namespace Roslynator.CSharp.Spelling
{
    [Export(typeof(ILanguageService))]
    [ExportMetadata("Language", LanguageNames.CSharp)]
    [ExportMetadata("ServiceType", "Roslynator.Spelling.ISpellingService")]
    internal class CSharpSpellingService : SpellingService
    {
        public override ISyntaxFactsService SyntaxFacts => CSharpSyntaxFactsService.Instance;

        public override SpellingAnalysisResult AnalyzeSpelling(SyntaxNode node, SpellingData spellingData,  SpellingAnalysisOptions options, CancellationToken cancellationToken)
        {
            var walker = new CSharpSpellingWalker(spellingData, options, cancellationToken);

            walker.Visit(node);

            return new SpellingAnalysisResult(walker.Errors);
        }
    }
}
