// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Roslynator.CSharp
{
    internal static class FormattingHelpers
    {
        [Conditional("DEBUG")]
        public static void VerifyChangedSpansAreWhitespace(SyntaxNode node, List<TextChange> textChanges)
        {
            Debug.Assert(textChanges.Count > 0, $"'{nameof(textChanges)}' is empty\r\n\r\n{node}");

            foreach (TextChange textChange in textChanges)
            {
                if (!VerifyTextChange(textChange))
                {
                    Debug.Fail($"Cannot find matching trivia for TextChange {textChange}\r\n\r\n{node}");
                    break;
                }
            }

            bool VerifyTextChange(TextChange textChange)
            {
                TextSpan span = textChange.Span;
                int start = span.Start;
                int end = span.End;

                while (!node.FullSpan.Contains(start)
                    || !node.FullSpan.Contains(end))
                {
                    node = node.Parent;
                }

                SyntaxToken token = node.FindToken(start);

                if (span.IsEmpty)
                {
                    return start == token.SpanStart
                        || start == token.Span.End;
                }

                SyntaxTriviaList leading = token.LeadingTrivia;

                if (leading.Span.Contains(start))
                {
                    return end <= leading.Span.End
                        && VerifySpan(span, leading);
                }

                SyntaxTriviaList trailing = token.TrailingTrivia;

                if (!trailing.Span.Contains(start))
                    return false;

                if (end <= trailing.Span.End)
                    return VerifySpan(span, trailing);

                span = TextSpan.FromBounds(start, trailing.Span.End);

                if (!VerifySpan(span, trailing))
                    return false;

                token = node.FindToken(end);

                leading = token.LeadingTrivia;

                if (trailing.Span.End != leading.Span.End)
                    return false;

                span = TextSpan.FromBounds(leading.Span.Start, end);

                return VerifySpan(span, leading);
            }

            static bool VerifySpan(TextSpan span, SyntaxTriviaList leading)
            {
                for (int i = 0; i < leading.Count; i++)
                {
                    if (!leading[i].IsWhitespaceOrEndOfLineTrivia())
                        continue;

                    if (leading[i].Span.Contains(span.Start))
                    {
                        if (span.IsEmpty)
                            return true;

                        if (leading[i].Span.End == span.End)
                            return true;

                        span = span.TrimFromStart(leading[i].Span.Length);
                        i++;

                        while (i < leading.Count)
                        {
                            if (leading[i].Span.End == span.End)
                                return true;

                            if (span.End < leading[i].Span.End)
                                break;

                            span = span.TrimFromStart(leading[i].Span.Length);
                            i++;
                        }

                        break;
                    }
                }

                return false;
            }
        }
    }
}

