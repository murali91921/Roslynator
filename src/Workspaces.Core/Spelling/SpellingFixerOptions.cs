// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Roslynator.Spelling
{
    internal class SpellingFixerOptions
    {
        public static SpellingFixerOptions Default { get; } = new SpellingFixerOptions();

        public SpellingFixerOptions(
            int codeContext = 1,
            bool includeComments = true,
            bool includeLocal = true,
            bool includeGeneratedCode = false,
            bool autoFix = true,
            bool interactive = true,
            bool enableCompoundWords = false)
        {
            if (codeContext < 0)
                throw new ArgumentOutOfRangeException(nameof(codeContext), codeContext, "");

            CodeContext = codeContext;
            IncludeComments = includeComments;
            IncludeLocal = includeLocal;
            IncludeGeneratedCode = includeGeneratedCode;
            AutoFix = autoFix;
            Interactive = interactive;
            EnableCompoundWords = enableCompoundWords;
        }

        public int CodeContext { get; }

        public bool IncludeComments { get; }

        public bool IncludeLocal { get; }

        public bool IncludeGeneratedCode { get; }

        public bool AutoFix { get; }

        public bool Interactive { get; }

        public bool EnableCompoundWords { get; }
    }
}
