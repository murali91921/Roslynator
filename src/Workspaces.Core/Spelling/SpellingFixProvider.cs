// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Roslynator.Spelling
{
    internal static class SpellingFixProvider
    {
        public static ImmutableArray<string> FuzzyMatches(
            string value,
            SpellingData spellingData,
            CancellationToken cancellationToken = default)
        {
            int length = value.Length;

            if (length < 4)
                return ImmutableArray<string>.Empty;

            ImmutableDictionary<string, (string, int, int)>.Builder fixes = null;
            ImmutableDictionary<char, int> charMap = null;
            WordCharMap map = spellingData.List.CharIndexMap;
            WordCharMap reversedMap = spellingData.List.ReversedCharIndexMap;
            var intersects = new ImmutableHashSet<string>[length];

            int i = 0;
            while (i < length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!map.TryGetValue(value, i, out ImmutableHashSet<string> values))
                    break;

                ImmutableHashSet<string> intersect = (i == 0)
                    ? values
                    : intersects[i - 1].Intersect(values);

                if (intersect.Count == 0)
                    break;

                intersects[i] = intersect;
                i++;
            }

            if (i >= length - 1)
                ProcessValues(intersects[length - 2]);

            if (i == length)
                ProcessValues(intersects[length - 1]);

            int j = length - 1;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!reversedMap.TryGetValue(value[j], length - j - 1, out ImmutableHashSet<string> values))
                    break;

                ImmutableHashSet<string> intersect = (j == length - 1)
                    ? values
                    : values.Intersect(intersects[j + 1]);

                if (intersect.Count == 0)
                    break;

                intersects[j] = intersect;

                if (j <= 1)
                {
                    ImmutableHashSet<string> values3 = intersects[j];
                    ProcessValues(values3);

                    if (j == 0)
                        break;
                }

                int diff = j - i;

                if (diff <= 0)
                {
                    ProcessValues(intersects[j - 1].Intersect(intersects[j]));

                    if (j > 1)
                        ProcessValues(intersects[j - 2].Intersect(intersects[j]));
                }
                else if (diff == 1)
                {
                    ProcessValues(intersects[j - 2].Intersect(intersects[j]));
                }

                j--;
            }

            if (j <= 0)
                ProcessValues(intersects[1]);

            if (i == -1)
                ProcessValues(intersects[0]);

            if (fixes == null)
                return ImmutableArray<string>.Empty;

            List<(string, int, int)> fixes2 = fixes.Select(f => f.Value).ToList();

            fixes2.Sort((x, y) => Sort(x, y));

            return fixes2
                .Select(f => f.Item1)
                .ToImmutableArray();

            void ProcessValues(ImmutableHashSet<string> values)
            {
                foreach (string value2 in values)
                {
                    int lengthDiff = length - value2.Length;

                    if ((Math.Abs(lengthDiff) <= 1 || (lengthDiff == -2 && length >= 6))
                        && fixes?.ContainsKey(value2) != true)
                    {
                        int charDiff = GetCharDiff(value2);

                        if ((lengthDiff == -1 && charDiff == 0)
                            || (lengthDiff == -1 && charDiff == 1 && length >= 6)
                            || (lengthDiff == 0 && charDiff == 1)
                            || (lengthDiff == 1 && charDiff == 1)
                            || (lengthDiff == -2 && charDiff == 0))
                        {
                            (fixes ??= ImmutableDictionary.CreateBuilder<string, (string, int, int)>(WordList.DefaultComparer))
                                .Add(value2, (value2, lengthDiff, charDiff));
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

                return length - count;
            }

            static int Sort((string, int, int) x, (string, int, int) y)
            {
                int a = (x.Item2 < 0) ? -x.Item2 : x.Item2;
                int b = (y.Item2 < 0) ? -y.Item2 : y.Item2;

                int diff = a.CompareTo(b);

                if (diff != 0)
                    return diff;

                diff = x.Item3.CompareTo(y.Item3);

                if (diff! != 0)
                    return diff;

                return StringComparer.CurrentCulture.Compare(x.Item1, y.Item1);
            }
        }

        public static ImmutableArray<string> SwapMatches(
            string value,
            SpellingData spellingData,
            CancellationToken cancellationToken = default)
        {
            int length = value.Length;

            if (length < 4)
                return ImmutableArray<string>.Empty;

            ImmutableArray<string>.Builder fixes = null;

            ImmutableHashSet<string> values = ImmutableHashSet<string>.Empty;

            foreach (WordChar wordChar in value
                .GroupBy(ch => ch)
                .Select(g => new WordChar(g.Key, g.Count())))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!spellingData.List.CharMap.TryGetValue(wordChar, out ImmutableHashSet<string> values2))
                    break;

                values = (values.Count == 0)
                    ? values2
                    : values.Intersect(values2);

                if (values.Count == 0)
                    break;
            }

            int maxDiff = (length <= 6) ? 2 : 3;

            foreach (string value2 in values)
            {
                if (length == value2.Length)
                {
                    int diff = 0;

                    for (int i = 0; i < length; i++)
                    {
                        if (value[i] != value2[i])
                            diff++;

                        if (i > 1
                            && diff > maxDiff)
                        {
                            break;
                        }
                    }

                    if (diff <= maxDiff)
                        (fixes ??= ImmutableArray.CreateBuilder<string>()).Add(value2);
                }
            }

            return fixes?.ToImmutableArray() ?? ImmutableArray<string>.Empty;
        }

        public static ImmutableArray<int> GetSplitIndexes(
            SpellingError spellingError,
            SpellingData spellingData,
            CancellationToken cancellationToken = default)
        {
            string value = spellingError.Value;
            int length = value.Length;

            ImmutableArray<int>.Builder splitIndexes = null;

            if (length >= 4)
            {
                char ch = value[0];

                // Tvalue > TValue
                // Ienumerable > IEnumerable
                if ((ch == 'I' || ch == 'T')
                    && spellingError.Casing == TextCasing.FirstUpper
                    && spellingData.List.Contains(value.Substring(1)))
                {
                    (splitIndexes ??= ImmutableArray.CreateBuilder<int>()).Add(1);
                }
            }

            if (length < 6)
                return splitIndexes?.ToImmutableArray() ?? ImmutableArray<int>.Empty;

            value = spellingError.ValueLower;

            WordCharMap map = spellingData.List.CharIndexMap;

            ImmutableHashSet<string> values = ImmutableHashSet<string>.Empty;

            for (int i = 0; i < length - 3; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

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

                    for (int j = i + 1; j < length; j++)
                    {
                        if (!map.TryGetValue(value[j], j - i - 1, out ImmutableHashSet<string> values4))
                            break;

                        values3 = (j == i + 1) ? values4 : values3.Intersect(values4);

                        if (values3.Count == 0)
                            break;
                    }

                    foreach (string value3 in values3)
                    {
                        if (value3.Length != length - i - 1)
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
