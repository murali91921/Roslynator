// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;

namespace Roslynator.Spelling
{
    internal static class SpellingFixProvider
    {
        public static ImmutableArray<string> FuzzyMatches(
            string value,
            SpellingData spellingData)
        {
            int length = value.Length;

            ImmutableArray<(string, int, int)>.Builder fixes = null;
            ImmutableHashSet<string> values;
            ImmutableHashSet<string> valuesAllButLast = ImmutableHashSet<string>.Empty;
            ImmutableDictionary<char, int> charMap = null;

            WordCharMap map = spellingData.List.CharIndexMap;
            WordCharMap reversedMap = spellingData.List.ReversedCharIndexMap;

            int max = length;
            int i;

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

                    if (i == length - 2
                        && max == length)
                    {
                        valuesAllButLast = intersect;
                    }

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
                    ProcessValues(values);
            }

            ProcessValues(valuesAllButLast);

            return (fixes == null)
                ? ImmutableArray<string>.Empty
                : fixes
                    .OrderBy(f => f.Item2)
                    .ThenBy(f => f.Item3)
                    .Select(f => f.Item1)
                    .Distinct(WordList.DefaultComparer)
                    .ToImmutableArray();

            void ProcessValues(ImmutableHashSet<string> values)
            {
                foreach (string value2 in values)
                {
                    int lengthDiff = length - value2.Length;

                    if (Math.Abs(lengthDiff) <= 1
                        || (lengthDiff == -2 && length >= 6))
                    {
                        int charDiff = GetCharDiff(value2);

                        if ((lengthDiff == -1 && charDiff == 0)
                            || (lengthDiff == -1 && charDiff == 1 && length >= 6)
                            || (lengthDiff == 0 && charDiff == 1)
                            || (lengthDiff == 1 && charDiff == 1)
                            || (lengthDiff == -2 && charDiff == 0))
                        {
                            (fixes ??= ImmutableArray.CreateBuilder<(string, int, int)>())
                                .Add((value2, lengthDiff, charDiff));
                        }
                    }
                }
            }

            int GetCharDiff(string value2)
            {
                if (charMap == null)
                {
                    charMap = value
                        .GroupBy(ch => ch)
                        .ToImmutableDictionary(g => g.Key, g => g.Count());
                }

                int count = value2.GroupBy(ch => ch)
                    .Join(charMap, g => g.Key, kvp => kvp.Key, (g, kvp) => Math.Min(g.Count(), kvp.Value))
                    .Sum();

                return value.Length - count;
            }
        }

        public static ImmutableArray<string> SwapMatches(
            string value,
            SpellingData spellingData)
        {
            ImmutableArray<string>.Builder fixes = null;

            ImmutableHashSet<string> values = ImmutableHashSet<string>.Empty;

            foreach (WordChar wordChar in value
                .GroupBy(ch => ch)
                .Select(g => new WordChar(g.Key, g.Count())))
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

                    if (matchCount >= Math.Max(2, value.Length - 3))
                        (fixes ??= ImmutableArray.CreateBuilder<string>()).Add(value2);
                }
            }

            return fixes?.ToImmutableArray() ?? ImmutableArray<string>.Empty;
        }

        public static ImmutableArray<int> GetSplitIndexes(SpellingError spellingError, SpellingData spellingData)
        {
            string value = spellingError.Value;

            ImmutableArray<int>.Builder splitIndexes = null;

            if (value.Length >= 4)
            {
                char ch = value[0];

                // Tvalue > TValue
                // Ienumerable > IEnumerable
                if ((ch == 'I' || ch == 'T')
                    && TextUtility.GetTextCasing(value) == TextCasing.FirstUpper
                    && spellingData.List.Contains(value.Substring(1)))
                {
                    (splitIndexes ??= ImmutableArray.CreateBuilder<int>()).Add(1);
                }
            }

            if (value.Length < 6)
                return splitIndexes?.ToImmutableArray() ?? ImmutableArray<int>.Empty;

            value = spellingError.ValueLower;

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

                        (splitIndexes ??= ImmutableArray.CreateBuilder<int>()).Add(i + 1);
                        break;
                    }

                    break;
                }
            }

            return splitIndexes?.ToImmutableArray() ?? ImmutableArray<int>.Empty;
        }
    }
}
