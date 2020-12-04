// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using Microsoft.CodeAnalysis;

namespace Roslynator.Spelling
{
    internal abstract class SpellingService : ISpellingService
    {
        public abstract ISyntaxFactsService SyntaxFacts { get; }

        public abstract SpellingAnalysisResult AnalyzeSpelling(
            SyntaxNode node,
            SpellingData spellingData,
            SpellingFixerOptions options,
            CancellationToken cancellationToken);

        public abstract SpellingError CreateErrorFromDiagnostic(Diagnostic diagnostic);
    }
}
