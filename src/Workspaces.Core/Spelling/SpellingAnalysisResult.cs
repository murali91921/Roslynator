// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;

namespace Roslynator.Spelling
{
    internal readonly struct SpellingAnalysisResult
    {
        public SpellingAnalysisResult(ImmutableArray<SpellingError> errors)
        {
            Errors = errors;
        }

        public ImmutableArray<SpellingError> Errors { get; }

        internal SpellingAnalysisResult Add(in SpellingAnalysisResult result)
        {
            return new SpellingAnalysisResult(Errors.AddRange(result.Errors));
        }
    }
}
