// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;

namespace Roslynator.Spelling
{
    internal interface ISpellingService : ILanguageService
    {
        ISyntaxFactsService SyntaxFacts { get; }

        SpellingAnalysisResult AnalyzeSpelling(
            SyntaxNode node,
            SpellingData spellingData,
            SpellingAnalysisOptions options,
            CancellationToken cancellationToken);
    }
}