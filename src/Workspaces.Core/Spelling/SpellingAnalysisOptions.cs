// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Roslynator.Spelling
{
    internal class SpellingAnalysisOptions
    {
        public static SpellingAnalysisOptions Default { get; } = new SpellingAnalysisOptions();

        public SpellingAnalysisOptions(
            bool includeGeneratedCode = true)
        {
            IncludeGeneratedCode = includeGeneratedCode;
        }

        public bool IncludeGeneratedCode { get; }
    }
}
