// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        public Location Location { get; }

        public SyntaxToken Identifier { get; }

        public int Index { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"{Value}  {Identifier.ValueText}";

        public string ContainingValue { get; }
    }
}
