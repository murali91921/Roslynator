// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;

namespace Roslynator.Spelling
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal readonly struct SpellingFix
    {
        public SpellingFix(string originalValue, string fixedValue)
        {
            OriginalValue = originalValue;
            FixedValue = fixedValue;
        }

        public string OriginalValue { get; }

        public string FixedValue { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"{OriginalValue}  {FixedValue}";
    }
}
