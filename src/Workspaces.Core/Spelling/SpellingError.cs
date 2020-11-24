// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Roslynator.Spelling
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal readonly struct SpellingError
    {
        public SpellingError(
            string value,
            string containingValue,
            Location location,
            int index,
            SyntaxToken identifier = default)
        {
            Value = value;
            ContainingValue = containingValue;
            Location = location;
            Identifier = identifier;
            Index = index;
        }

        public string Value { get; }

        public string ContainingValue { get; }

        public Location Location { get; }

        public SyntaxToken Identifier { get; }

        public int Index { get; }

        public int EndIndex => Index + Value.Length;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                string value2 = (string.Equals(Value, ContainingValue, StringComparison.Ordinal))
                    ? null
                    : ContainingValue;

                return $"{Value}  {value2}";
            }
        }
    }
}
