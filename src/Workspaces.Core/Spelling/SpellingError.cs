// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Roslynator.Spelling
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal readonly struct SpellingError
    {
        public SpellingError(string value, Location location)
            : this (value, location, default, -1)
        {
        }

        public SpellingError(string value, Location location, SyntaxToken identifier, int index)
        {
            Value = value;
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
    }
}
