// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Roslynator.Spelling
{
    internal static class SpellingFixProvider
    {
        public static IEnumerable<string> GetFixes(
            SpellingError spellingError,
            SpellingData spellingData)
        {
            string value = spellingError.Value;

            int length = value.Length;

            Debug.WriteLine($"find fix for '{value}'");

            if (length >= 4)
            {
                string valueLower = value.ToLowerInvariant();

                foreach (string match in MatchSwappedLetters(valueLower, spellingData))
                {
                    Debug.WriteLine($"match: {match}");
                    yield return match;
                }

                foreach (string match in FuzzyMatch(valueLower, spellingData))
                {
                    Debug.WriteLine($"match: {match}");
                    yield return match;
                }
            }

            if (value.EndsWith("ed", StringComparison.OrdinalIgnoreCase))
            {
                if (value.EndsWith("tted", StringComparison.OrdinalIgnoreCase))
                {
                    string s = value.Remove(value.Length - 3);

                    if (IsValidFix(s))
                        yield return s;
                }
            }
            else if (value.EndsWith("ial", StringComparison.OrdinalIgnoreCase))
            {
                string s = value.Remove(value.Length - 3);

                if (IsValidFix(s))
                    yield return s;
            }
            else if (value.EndsWith("ical", StringComparison.OrdinalIgnoreCase))
            {
                string s = value.Remove(value.Length - 2);

                if (IsValidFix(s))
                    yield return s;
            }

            int i = 0;
            for (; i < length; i++)
            {
                char ch = value[i];

                // readonlyly > readonly
                // sensititive > sensitive
                if (i < length - 3)
                {
                    char ch2 = Peek();

                    if (ch2 != ch
                        && ch == Peek(2)
                        && ch2 == Peek(3))
                    {
                        string s = value.Remove(i, 2);

                        if (IsValidFix(s))
                            yield return s;
                    }
                }
            }

            char Peek(int offset = 1)
            {
                if (i < length - offset)
                    return value[i + offset];

                return default;
            }

            bool IsValidFix(string value)
            {
                return spellingData.List.Contains(value)
                    && !spellingData.IgnoreList.Contains(value);
            }
        }

        private static IEnumerable<string> FuzzyMatch(
            string value,
            SpellingData spellingData)
        {
            int length = value.Length;

            ImmutableDictionary<char, int> charMap = null;

            WordCharMap map = spellingData.List.CharIndexMap;
            WordCharMap reversedMap = spellingData.List.ReversedCharIndexMap;

            int max = length;
            int i;
            ImmutableHashSet<string> values;

            for (; max >= 0; max = i - 1)
            {
                //Debug.WriteLine($"\r\nmax: {max}");

                values = ImmutableHashSet<string>.Empty;

                int min = -1;
                i = 0;
                while (i < max)
                {
                    if (!map.TryGetValue(value, i, out ImmutableHashSet<string> values2))
                        break;

                    ImmutableHashSet<string> intersect = (i == 0)
                        ? values2
                        : values.Intersect(values2);

                    if (intersect.Count == 0)
                        break;

                    //Debug.WriteLine($"left  {i,2}  {value[i]} {intersect.Count,5}");

                    values = intersect;
                    min = i;
                    i++;
                }

                int j = length - 1;
                while (j > min)
                {
                    if (!reversedMap.TryGetValue(
                        value[j],
                        length - j - 1,
                        out ImmutableHashSet<string> values2))
                    {
                        break;
                    }

                    ImmutableHashSet<string> intersect;
                    if (max == 0
                        && j == length - 1)
                    {
                        intersect = values2;
                    }
                    else
                    {
                        intersect = values.Intersect(values2);
                    }

                    if (intersect.Count == 0)
                        break;

                    //Debug.WriteLine($"right {j,2}  {value[j]} {intersect.Count,5}");

                    values = intersect;
                    j--;
                }

                int diff = j - i;

                if (Math.Abs(diff) <= 1)
                {
                    foreach (string value2 in values)
                    {
                        int lengthDiff = value2.Length - length;

                        if (Math.Abs(lengthDiff) <= 1)
                        {
                            int charDiff = GetCharDiff(value2);

                            if ((lengthDiff == 1 && charDiff == 0)
                                || (lengthDiff <= 0 && charDiff == 1))
                            {
                                yield return value2;
                            }
                        }
                    }
                }

                int GetCharDiff(string value2)
                {
                    if (charMap == null)
                    {
                        charMap = value
                            .GroupBy(f => f)
                            .ToImmutableDictionary(f => f.Key, f => f.Count());
                    }

                    int count = value2.GroupBy(f => f)
                        .Join(charMap, f => f.Key, f => f.Key, (f, g) => Math.Min(f.Count(), g.Value))
                        .Sum();

                    return value.Length - count;
                }
            }
        }

        private static IEnumerable<string> MatchSwappedLetters(
            string value,
            SpellingData spellingData)
        {
            ImmutableHashSet<string> values = ImmutableHashSet<string>.Empty;

            foreach (WordChar wordChar in value
                .GroupBy(f => f)
                .Select(f => new WordChar(f.Key, f.Count())))
            {
                if (!spellingData.List.CharMap.TryGetValue(wordChar, out ImmutableHashSet<string> values2))
                    break;

                values = (values.Count == 0)
                    ? values2
                    : values.Intersect(values2);

                if (values.Count == 0)
                    break;
            }

            foreach (string value2 in values)
            {
                if (value.Length == value2.Length)
                {
                    int matchCount = 0;

                    for (int i = 0; i < value.Length; i++)
                    {
                        if (value[i] == value2[i])
                            matchCount++;
                    }

                    if (matchCount >= value.Length - 3)
                        yield return value2;
                }
            }
        }

        public static IEnumerable<int> GetSplitIndex(string value, SpellingData spellingData)
        {
            if (value.Length < 6)
                yield break;

            WordCharMap map = spellingData.List.CharIndexMap;

            ImmutableHashSet<string> values = ImmutableHashSet<string>.Empty;

            for (int i = 0; i < value.Length - 3; i++)
            {
                if (!map.TryGetValue(value, i, out ImmutableHashSet<string> values2))
                    break;

                values = (i == 0) ? values2 : values.Intersect(values2);

                if (values.Count == 0)
                    break;

                if (i < 2)
                    continue;

                foreach (string value2 in values)
                {
                    if (value2.Length != i + 1)
                        continue;

                    ImmutableHashSet<string> values3 = ImmutableHashSet<string>.Empty;

                    for (int j = i + 1; j < value.Length; j++)
                    {
                        if (!map.TryGetValue(value[j], j - i - 1, out ImmutableHashSet<string> values4))
                            break;

                        values3 = (j == i + 1) ? values4 : values3.Intersect(values4);

                        if (values3.Count == 0)
                            break;
                    }

                    foreach (string value3 in values3)
                    {
                        if (value3.Length != value.Length - i - 1)
                            continue;

                        yield return i + 1;
                        break;
                    }

                    break;
                }
            }
        }
    }
}
