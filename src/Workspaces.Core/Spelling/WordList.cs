// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Roslynator.Spelling
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal class WordList
    {
        private WordCharMap _charIndexMap;
        private WordCharMap _reversedCharIndexMap;
        private WordCharMap _charMap;

        public static StringComparer DefaultComparer { get; } = StringComparer.CurrentCultureIgnoreCase;

        public static WordList Default { get; } = new WordList(null, DefaultComparer, null);

        public static WordList Default_CurrentCulture { get; } = new WordList(
            null,
            StringComparer.CurrentCulture,
            null);

        public WordList(string path, StringComparer comparer, IEnumerable<string> values)
        {
            Path = path;
            Comparer = comparer ?? DefaultComparer;
            Values = values?.ToImmutableHashSet(comparer) ?? ImmutableHashSet<string>.Empty;
        }

        public string Path { get; }

        public StringComparer Comparer { get; }

        public ImmutableHashSet<string> Values { get; }

        public int Count => Values.Count;

        public WordCharMap CharIndexMap
        {
            get
            {
                if (_charIndexMap == null)
                    Interlocked.CompareExchange(ref _charIndexMap, WordCharMap.CreateCharIndexMap(this), null);

                return _charIndexMap;
            }
        }

        public WordCharMap ReversedCharIndexMap
        {
            get
            {
                if (_reversedCharIndexMap == null)
                    Interlocked.CompareExchange(ref _reversedCharIndexMap, WordCharMap.CreateCharIndexMap(this, reverse: true), null);

                return _reversedCharIndexMap;
            }
        }

        public WordCharMap CharMap
        {
            get
            {
                if (_charMap == null)
                    Interlocked.CompareExchange(ref _charMap, WordCharMap.CreateCharMap(this), null);

                return _charMap;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"Count = {Values.Count}  {Path}";

        public static WordList Load(string path, StringComparer comparer = null)
        {
            IEnumerable<string> values = ReadWords(path);

            return new WordList(path, comparer, values);
        }

        public static WordList LoadText(string text, StringComparer comparer = null)
        {
            IEnumerable<string> values = ReadLines();

            values = ReadWords(values);

            return new WordList(null, comparer, values);

            IEnumerable<string> ReadLines()
            {
                using (var sr = new StringReader(text))
                {
                    string line;

                    while ((line = sr.ReadLine()) != null)
                        yield return line;
                }
            }
        }

        private static IEnumerable<string> ReadWords(string path)
        {
            return ReadWords(File.ReadLines(path));
        }

        private static IEnumerable<string> ReadWords(IEnumerable<string> values)
        {
            return values
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => f.Trim())
                .Where(f => !f.StartsWith("#"));
        }

        public WordList Intersect(WordList wordList, params WordList[] additionalWordList)
        {
            IEnumerable<string> intersect = Values
                .Intersect(wordList.Values, Comparer);

            if (additionalWordList?.Length > 0)
            {
                intersect = intersect
                    .Intersect(additionalWordList.SelectMany(f => f.Values), Comparer);
            }

            return WithValues(intersect);
        }

        public WordList Except(WordList wordList, params WordList[] additionalWordList)
        {
            IEnumerable<string> except = Values
                .Except(wordList.Values, Comparer);

            if (additionalWordList?.Length > 0)
            {
                except = except
                    .Except(additionalWordList.SelectMany(f => f.Values), Comparer);
            }

            return WithValues(except);
        }

        public bool Contains(string value)
        {
            return Values.Contains(value);
        }

        public WordList AddValue(string value)
        {
            return new WordList(Path, Comparer, Values.Add(value));
        }

        public WordList AddValues(IEnumerable<string> values)
        {
            values = Values.Concat(values).Distinct(Comparer);

            return new WordList(Path, Comparer, values);
        }

        public WordList AddValues(WordList wordList)
        {
            IEnumerable<string> values = Values.Concat(wordList.Values).Distinct(Comparer);

            return new WordList(Path, Comparer, values);
        }

        public WordList WithValues(IEnumerable<string> values)
        {
            return new WordList(Path, Comparer, values);
        }

        public static void Save(
            string path,
            WordList wordList)
        {
            IEnumerable<string> values = wordList.Values
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => f.Trim().ToLower())
                .Distinct(wordList.Comparer)
                .OrderBy(f => f, StringComparer.InvariantCulture);

            File.WriteAllText(path, string.Join(Environment.NewLine, values));
        }

        public void Save(string path = null)
        {
            Save(path ?? Path, this);
        }

        public WordList SaveAndLoad()
        {
            Save();

            return Load(Path, Comparer);
        }
    }
}
