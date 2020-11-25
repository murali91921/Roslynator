// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;

namespace Roslynator.Spelling
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal readonly struct SpellingFix
    {
        public SpellingFix(string value, SpellingFixKind kind)
        {
            Value = value;
            Kind = kind;
        }

        public string Value { get; }

        public SpellingFixKind Kind { get; }

        public SpellingFix WithValue(string value) => new SpellingFix(value, Kind);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"{Kind}  {Value}";
    }
}
