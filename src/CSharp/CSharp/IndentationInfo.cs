// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Roslynator.CSharp
{
    internal readonly struct IndentationInfo
    {
        public IndentationInfo(SyntaxToken token, TextSpan span)
        {
            Token = token;
            Span = span;
        }

        public SyntaxToken Token { get; }

        public TextSpan Span { get; }

        public override string ToString() => (Span.IsEmpty) ? "" : Token.ToString(Span);

        public SyntaxTrivia GetTrivia()
        {
            if (!Span.IsEmpty)
            {
                foreach (SyntaxTrivia trivia in Token.LeadingTrivia)
                {
                    if (trivia.Span == Span)
                        return trivia;
                }
            }

            return default;
        }
    }
}
