// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace Roslynator.Spelling
{
    internal readonly struct SpellingError
    {
        public SpellingError(string text, Location location, SyntaxToken identifier = default)
        {
            Text = text;
            Location = location;
            Identifier = identifier;
        }

        public string Text { get; }

        public Location Location { get; }

        public SyntaxToken Identifier { get; }
    }
}
