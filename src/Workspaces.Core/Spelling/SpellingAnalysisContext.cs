// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Roslynator.RegularExpressions;

namespace Roslynator.Spelling
{
    //TODO: decode html entity?
    //TODO: parse email address
    internal class SpellingAnalysisContext
    {
        private const string _splitCasePattern = @"
    (?<=
        \p{Lu}
    )
    (?=
        \p{Lu}\p{Ll}
    )
|
    (?<=
        \p{Ll}
    )
    (?=
        \p{Lu}
    )
";

        private static readonly Regex _splitCaseRegex = new Regex(
            _splitCasePattern,
            RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _splitIdentifierRegex = new Regex(
            @"\P{L}+|" + _splitCasePattern,
            RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _splitHyphenRegex = new Regex("-");

        private static readonly Regex _splitCaseAndHyphenRegex = new Regex(
            "-|" + _splitCasePattern,
            RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _wordInCommentRegex = new Regex(
            @"
\b
\p{L}{2,}
(-\p{L}{2,})*
\p{L}*
(
    (?='s\b)
|
    ('(d|ll|m|re|t|ve)\b)
|
    ('(?!\p{L})\b)
|
    \b
)",
            RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

        // NaN, IDs, GACed, JSONify, AND'd
        private static readonly Regex _specialWordRegex = new Regex(
            @"
\A
(?:
    (?:
        (?<g>\p{Lu}\p{Ll}\p{Lu})
    )
    |
    (?:
        (?<g>\p{Lu}{2,})
        (?:s|ed|ify|'d)
    )
)
\z
",
            RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _urlRegex = new Regex(
            @"\bhttps?://[^\s]+(?=\s|\z)", RegexOptions.IgnoreCase);

        private readonly Action<Diagnostic> _reportDiagnostic;

        public SpellingData SpellingData { get; }

        public SpellingFixerOptions Options { get; }

        public CancellationToken CancellationToken { get; }

        public SpellingAnalysisContext(
            Action<Diagnostic> reportDiagnostic,
            SpellingData spellingData,
            SpellingFixerOptions options,
            CancellationToken cancellationToken)
        {
            SpellingData = spellingData;
            Options = options;
            CancellationToken = cancellationToken;

            _reportDiagnostic = reportDiagnostic;
        }

        public void AnalyzeText(string value, TextSpan textSpan, SyntaxTree syntaxTree)
        {
            int prevEnd = 0;

            Match match = _urlRegex.Match(value, prevEnd);

            while (match.Success)
            {
                AnalyzeText(value, prevEnd, match.Index - prevEnd, textSpan, syntaxTree);

                prevEnd = match.Index + match.Length;

                match = match.NextMatch();
            }

            AnalyzeText(value, prevEnd, value.Length - prevEnd, textSpan, syntaxTree);
        }

        private void AnalyzeText(
            string value,
            int startIndex,
            int length,
            TextSpan textSpan,
            SyntaxTree syntaxTree)
        {
            Regex splitRegex = GetSplitRegex();

            Match match = _wordInCommentRegex.Match(value, startIndex, length);

            //TODO: convert to 'for'
            while (match.Success)
            {
                if (match.Length >= Options.MinWordLength)
                {
                    if (splitRegex == null)
                    {
                        AnalyzeValue(
                            match.Value,
                            new TextSpan(textSpan.Start + match.Index, match.Length),
                            syntaxTree);
                    }
                    else
                    {
                        Match match2 = _specialWordRegex.Match(match.Value);

                        if (match2.Success)
                        {
                            Group group = match2.Groups["g"];

                            AnalyzeValue(
                                group.Value,
                                new TextSpan(textSpan.Start + group.Index, group.Length),
                                syntaxTree);
                        }
                        else
                        {
                            foreach (SplitItem splitItem in SplitItemCollection.Create(splitRegex, match.Value))
                            {
                                AnalyzeValue(
                                    splitItem.Value,
                                    new TextSpan(textSpan.Start + match.Index + splitItem.Index, splitItem.Value.Length),
                                    syntaxTree);
                            }
                        }
                    }
                }

                match = match.NextMatch();
            }

            Regex GetSplitRegex()
            {
                return Options.SplitMode switch
                {
                    SplitMode.None => null,
                    SplitMode.Case => _splitCaseRegex,
                    SplitMode.Hyphen => _splitHyphenRegex,
                    SplitMode.CaseAndHyphen => _splitCaseAndHyphenRegex,
                    _ => throw new InvalidOperationException(),
                };
            }
        }

        public void AnalyzeIdentifier(
            SyntaxToken identifier,
            int prefixLength = 0)
        {
            string value = identifier.ValueText;

            if (value.Length < Options.MinWordLength)
                return;

            if (prefixLength > 0)
            {
                if (SpellingData.IgnoreList.Contains(value))
                    return;

                if (SpellingData.List.Contains(value))
                    return;
            }

            string value2 = (prefixLength > 0) ? value.Substring(prefixLength) : value;

            Match match = _specialWordRegex.Match(value2);

            if (match.Success)
            {
                Group group = match.Groups["g"];

                AnalyzeValue(
                    group.Value,
                    new TextSpan(identifier.SpanStart + prefixLength, group.Length),
                    identifier.SyntaxTree);
            }
            else
            {
                SplitItemCollection splitItems = SplitItemCollection.Create(_splitIdentifierRegex, value2);

                if (splitItems.Count > 1)
                {
                    if (SpellingData.IgnoreList.Contains(value2))
                        return;

                    if (SpellingData.List.Contains(value2))
                        return;
                }

                foreach (SplitItem splitItem in splitItems)
                {
                    Debug.Assert(splitItem.Value.All(f => char.IsLetter(f)), splitItem.Value);

                    AnalyzeValue(
                        splitItem.Value,
                        new TextSpan(identifier.SpanStart + splitItem.Index + prefixLength, splitItem.Length),
                        identifier.SyntaxTree);
                }
            }
        }

        private void AnalyzeValue(
            string value,
            TextSpan textSpan,
            SyntaxTree syntaxTree)
        {
            Debug.Assert(value.All(f => char.IsLetter(f) || f == '\''), value);

            if (value.Length < Options.MinWordLength)
                return;

            if (IsAllowedNonsensicalWord(value))
                return;

            if (SpellingData.IgnoreList.Contains(value))
                return;

            if (SpellingData.List.Contains(value))
                return;

            Diagnostic diagnostic = Diagnostic.Create(
                SpellingAnalyzer.DiagnosticDescriptor,
                Location.Create(syntaxTree, textSpan),
                value);

            _reportDiagnostic(diagnostic);
        }

        public static bool IsAllowedNonsensicalWord(string value)
        {
            if (value.Length < 3)
                return false;

            switch (value)
            {
                case "xyz":
                case "Xyz":
                case "XYZ":
                case "asdfgh":
                case "Asdfgh":
                case "ASDFGH":
                case "qwerty":
                case "Qwerty":
                case "QWERTY":
                case "qwertz":
                case "Qwertz":
                case "QWERTZ":
                    return true;
            }

            if (IsAbcSequence())
                return true;

            if (IsAaaSequence())
                return true;

            if (IsAaaBbbCccSequence())
                return true;

            return false;

            bool IsAbcSequence()
            {
                int num = 0;

                if (value[0] == 'a')
                {
                    if (value[1] == 'b')
                    {
                        num = 'c';
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (value[0] == 'A')
                {
                    if (value[1] == 'B')
                    {
                        num = 'C';
                    }
                    else if (value[1] == 'b')
                    {
                        num = 'c';
                    }
                    else
                    {
                        return false;
                    }
                }

                for (int i = 2; i < value.Length; i++)
                {
                    if (value[i] != num)
                        return false;

                    num++;
                }

                return true;
            }

            bool IsAaaSequence()
            {
                char ch = value[0];
                int i = 1;

                if (ch >= 65
                    && ch <= 90
                    && value[1] == ch + 32)
                {
                    ch = (char)(ch + 32);
                    i++;
                }

                while (i < value.Length)
                {
                    if (value[i] != ch)
                        return false;

                    i++;
                }

                return true;
            }

            // aabbcc
            bool IsAaaBbbCccSequence()
            {
                char ch = value[0];
                int i = 1;

                while (i < value.Length
                    && value[i] == ch)
                {
                    i++;
                }

                if (i > 1
                    && (ch == 'a' || ch == 'A')
                    && value.Length >= 6
                    && value.Length % i == 0)
                {
                    int length = i;
                    int count = value.Length / i;

                    for (int j = 0; j < count - 1; j++)
                    {
                        var ch2 = (char)(ch + j + 1);

                        int start = i + (j * length);
                        int end = start + length;

                        for (int k = i + (j * length); k < end; k++)
                        {
                            if (ch2 != value[k])
                                return false;
                        }
                    }

                    return true;
                }

                return false;
            }
        }
    }
}
