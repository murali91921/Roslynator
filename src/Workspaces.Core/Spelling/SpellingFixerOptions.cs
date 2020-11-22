// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Roslynator.Spelling
{
    internal class SpellingFixerOptions
    {
        public static SpellingFixerOptions Default { get; } = new SpellingFixerOptions();

        public SpellingFixerOptions(
            bool includeLocal = true,
            bool includeGeneratedCode = false,
            bool interactive = false)
        {
            IncludeLocal = includeLocal;
            IncludeGeneratedCode = includeGeneratedCode;
            Interactive = interactive;
        }

        public bool IncludeLocal { get; }

        public bool IncludeGeneratedCode { get; }

        public bool Interactive { get; }
    }
}
