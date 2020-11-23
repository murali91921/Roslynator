// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Roslynator.Spelling
{
    internal class WordCharMap
    {
        private WordCharMap(WordList list, ImmutableDictionary<WordChar, ImmutableHashSet<string>> map)
        {
            List = list;
            Map = map;
        }

        public WordList List { get; }

        public ImmutableDictionary<WordChar, ImmutableHashSet<string>> Map { get; }

        public ImmutableHashSet<string> this[string value, int index]
        {
            get { return Map[WordChar.Create(value, index)]; }
        }

        public ImmutableHashSet<string> this[char ch, int index]
        {
            get { return Map[new WordChar(ch, index)]; }
        }

        public bool TryGetValue(WordChar wordChar, out ImmutableHashSet<string> value)
        {
            return Map.TryGetValue(wordChar, out value);
        }

        public bool TryGetValue(string word, int index, out ImmutableHashSet<string> value)
        {
            return Map.TryGetValue(WordChar.Create(word, index), out value);
        }

        public bool TryGetValue(char ch, int index, out ImmutableHashSet<string> value)
        {
            return Map.TryGetValue(new WordChar(ch, index), out value);
        }

        public static WordCharMap Create(WordList wordList, bool reverse = false)
        {
            return new WordCharMap(wordList, CreateMap(wordList, reverse: reverse));
        }

        private static ImmutableDictionary<WordChar, ImmutableHashSet<string>> CreateMap(
            WordList wordList,
            bool reverse = false)
        {
            return wordList.Values
                .Select(f => (value: f, chars: ((reverse) ? f.Reverse() : f).Select((ch, i) => (ch, i))))
                .SelectMany(f => f.chars.Select(g => (f.value, g.ch, g.i, key: new WordChar(g.ch, g.i))))
                .GroupBy(f => f.key)
                .ToImmutableDictionary(
                    f => f.Key,
                    f => f.Select(f => f.value).ToImmutableHashSet(wordList.Comparer));
        }

        public IEnumerable<(string value, int count)> FuzzyMatches(SpellingError spellingError)
        {
            string value = spellingError.Value;

            return value.Select((ch, i) => (ch, i))
                .Join(Map, f => new WordChar(f.ch, f.i), f => f.Key, (_, kvp) => kvp.Value)
                .SelectMany(f => f)
                .Where(f => f.Length >= value.Length - 2 && f.Length <= value.Length + 1)
                .GroupBy(f => f)
                .Select(f => (value: f.Key, count: f.Count()))
                .Where(f => f.count >= value.Length - 2)
                .OrderByDescending(f => f.count);
        }

        public int GetSplitIndex(SpellingError spellingError)
        {
            string value = spellingError.Value;

            if (value.Length < 4)
                return -1;

            if (!value.All(f => char.IsLower(f)))
                return -1;

            int index = -1;

            ImmutableHashSet<string> values = this[value, 0];

            for (int i = 1; i < value.Length - 2; i++)
            {
                ImmutableHashSet<string> values2 = this[value, i];

                values = values.Intersect(values2, StringComparer.CurrentCultureIgnoreCase)
                    .ToImmutableHashSet(StringComparer.CurrentCultureIgnoreCase);

                if (values.Count == 0)
                    return -1;

                foreach (string s in values)
                {
                    if (s.Length != i)
                        continue;

                    int j = i + 1;
                    int k = 1;
                    ImmutableHashSet<string> values3 = this[value[j], 0];

                    while (j < value.Length - 1)
                    {
                        ImmutableHashSet<string> values4 = this[value[j], k];

                        values3 = values3.Intersect(values4, StringComparer.CurrentCultureIgnoreCase)
                            .ToImmutableHashSet(StringComparer.CurrentCultureIgnoreCase);

                        if (values3.Count == 0)
                            break;

                        j++;
                        k++;
                    }

                    if (values3.Count == 1)
                    {
                        if (index == -1)
                        {
                            index = i;
                        }
                        else
                        {
                            return -1;
                        }
                    }
                }
            }

            return index;
        }
    }
}
