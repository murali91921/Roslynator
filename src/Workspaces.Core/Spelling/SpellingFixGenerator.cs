// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Roslynator.Spelling
{
    internal static class SpellingFixGenerator
    {
        public static IEnumerable<string> GeneratePossibleFixes(
            SpellingError spellingError,
            SpellingData spellingData)
        {
            string value = spellingError.Value;

            int length = value.Length;

            Debug.WriteLine($"find auto fix for '{value}'");

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
                else
                {
                    string s = value.Remove(value.Length - 2);

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

            WordCharMap map = spellingData.List.CharIndexMap;
            WordCharMap reversedMap = spellingData.List.ReversedCharIndexMap;

            int max = length;
            int i;
            ImmutableHashSet<string> values;

            //TODO: parallel
            for (;max >= 0; max = i - 1)
            {
                Debug.WriteLine($"\r\nmax: {max}");

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

                    Debug.WriteLine($"left  {i,2}  {value[i]} {intersect.Count,5}");

                    values = intersect;
                    min = i;
                    i++;
                }

                int j = length - 1;
                while (j > min)
                {
                    if (!reversedMap.TryGetValue(value[j], length - j - 1, out ImmutableHashSet<string> values2))
                        break;

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

                    Debug.WriteLine($"right {j,2}  {value[j]} {intersect.Count,5}");

                    values = intersect;
                    j--;
                }

                int diff = j - i;

                if (diff == 0
                    || diff == -1)
                {
                    string value2 = values.SingleOrDefault(shouldThrow: false);

                    // ambda
                    // lmbda
                    // llambda
                    // lambdaa
                    if (value2 != null
                        && Math.Abs(value2.Length - length) == 1)
                    {
                        yield return value2;
                    }
                }
                else if (diff == 1)
                {
                    string value2 = values.SingleOrDefault(shouldThrow: false);

                    if (value2 != null)
                    {
                        int lengthDiff = value2.Length - length;

                        // lanbda
                        // laambda
                        if (lengthDiff == 0
                            || lengthDiff == 1)
                        {
                            yield return value2;
                        }
                    }
                }
            }
        }

        private static IEnumerable<string> MatchSwappedLetters(
            string value,
            SpellingData spellingData)
        {
            ImmutableHashSet<string> values = ImmutableHashSet<string>.Empty;

            //TODO: parallel
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

            return values.Where(f => f.Length == value.Length);
        }

        //TODO: del
        //private static string FuzzyMatch(SpellingError spellingError, SpellingData spellingData)
        //{
        //    IEnumerable<(string value, int count)> fuzzyMatches = spellingData.List.Map.FuzzyMatches(spellingError);

        //    using (IEnumerator<(string value, int count)> en = fuzzyMatches.GetEnumerator())
        //    {
        //        if (en.MoveNext())
        //        {
        //            int length = spellingError.Value.Length;
        //            string fix = null;

        //            do
        //            {
        //                int diff = length - en.Current.count;
        //                int count = en.Current.count;
        //                string value2 = en.Current.value;
        //                int length2 = value2.Length;

        //                var shouldSkip = false;

        //                // lambda > lamdba
        //                if (diff == 2)
        //                {
        //                    if (length2 != length)
        //                        shouldSkip = true;
        //                }
        //                //lambda > lumbda
        //                //lambda > lamda
        //                else if (diff == 1)
        //                {
        //                    if (length2 > length
        //                        || length2 < length - 1)
        //                    {
        //                        shouldSkip = true;
        //                    }
        //                }
        //                // laambda
        //                else if (diff == 0)
        //                {
        //                    if (length2 <= length)
        //                        shouldSkip = true;
        //                }
        //                else
        //                {
        //                    Debug.Fail(diff.ToString());
        //                }

        //                if (!shouldSkip)
        //                {
        //                    IEnumerable<WordChar> first = spellingError.Value
        //                        .GroupBy(f => f)
        //                        .Select(f => new WordChar(f.Key, f.Count()));

        //                    IEnumerable<WordChar> second = en.Current.value
        //                        .GroupBy(f => f)
        //                        .Select(f => new WordChar(f.Key, f.Count()));

        //                    int sum = spellingError.Value.GroupBy(f => f)
        //                        .Join(
        //                            en.Current.value.GroupBy(f => f),
        //                            f => f.Key,
        //                            f => f.Key,
        //                            (f, g) => System.Math.Min(f.Count(), g.Count()))
        //                        .Sum();

        //                    if (sum == ((diff == 1) ? length - 1 : length))
        //                    {
        //                        if (fix == null)
        //                        {
        //                            fix = en.Current.value;
        //                        }
        //                        else
        //                        {
        //                            return null;
        //                        }
        //                    }
        //                }

        //            } while (en.MoveNext());

        //            return fix;
        //        }
        //    }

        //    return null;
        //}

        //private static string AddMissingApostrophe(string value)
        //{
        //    switch (value)
        //    {
        //        case "aint": // ain't
        //        case "arent": // aren't
        //        case "cant": // can't
        //        case "couldnt": // couldn't
        //        case "didnt": // didn't
        //        case "doesnt": // doesn't
        //        case "dont": // don't
        //        case "hadnt": // hadn't
        //        case "hasnt": // hasn't
        //        case "havent": // haven't
        //        case "hed": // he'd
        //        case "hell": // he'll
        //        case "hes": // he's
        //        case "id": // i'd
        //        case "ill": // i'll
        //        case "im": // i'm
        //        case "is": // i's
        //        case "isnt": // isn't
        //        case "itd": // it'd
        //        case "itll": // it'll
        //        case "its": // it's
        //        case "ive": // i've
        //        case "mustnt": // mustn't
        //        case "mustve": // must've
        //        case "neednt": // needn't
        //        case "oughtnt": // oughtn't
        //        case "shant": // shan't
        //        case "shed": // she'd
        //        case "shell": // she'll
        //        case "shes": // she's
        //        case "shouldnt": // shouldn't
        //        case "thatd": // that'd
        //        case "thatll": // that'll
        //        case "thats": // that's
        //        case "thered": // there'd
        //        case "therell": // there'll
        //        case "theres": // there's
        //        case "theyd": // they'd
        //        case "theyll": // they'll
        //        case "theyre": // they're
        //        case "theyve": // they've
        //        case "wasnt": // wasn't
        //        case "wed": // we'd
        //        case "well": // we'll
        //        case "were": // we're
        //        case "werent": // weren't
        //        case "weve": // we've
        //        case "whatd": // what'd
        //        case "whatll": // what'll
        //        case "whats": // what's
        //        case "whens": // when's
        //        case "whered": // where'd
        //        case "wheres": // where's
        //        case "whod": // who'd
        //        case "wholl": // who'll
        //        case "whore": // who're
        //        case "whos": // who's
        //        case "whove": // who've
        //        case "whys": // why's
        //        case "wont": // won't
        //        case "wouldnt": // wouldn't
        //        case "youd": // you'd
        //        case "youll": // you'll
        //        case "youre": // you're
        //        case "youve": // you've
        //            {
        //                switch (value[value.Length - 1])
        //                {
        //                    case 'e':
        //                    case 'l':
        //                        return value.Insert(value.Length - 2, "'");
        //                    default:
        //                        return value.Insert(value.Length - 1, "'");
        //                }
        //            }
        //    }

        //    return null;
        //}

        //private static bool IsVowel(char ch)
        //{
        //    switch (ch)
        //    {
        //        case 'a':
        //        case 'e':
        //        case 'i':
        //        case 'o':
        //        case 'u':
        //        case 'A':
        //        case 'E':
        //        case 'I':
        //        case 'O':
        //        case 'U':
        //        case 'y':
        //        case 'Y':
        //            return true;
        //        default:
        //            return false;
        //    }
        //}
    }
}
