// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;

namespace Roslynator.Spelling
{
    internal class SpellingData
    {
        private static readonly Regex _wordListFileName = new Regex(@"\Aroslynator\.spelling(\.|\z)", RegexOptions.IgnoreCase);

        public static SpellingData Empty { get; } = new SpellingData(
            WordList.Default,
            WordList.Default_CurrentCulture,
            ImmutableDictionary.Create<string, SpellingFix>(WordList.DefaultComparer));

        public SpellingData(
            WordList list,
            WordList ignoreList,
            ImmutableDictionary<string, SpellingFix> fixes = null)
        {
            List = list;
            IgnoreList = ignoreList;
            Fixes = fixes ?? ImmutableDictionary.Create<string, SpellingFix>(WordList.DefaultComparer);
        }

        public WordList List { get; }

        public WordList IgnoreList { get; }

        public ImmutableDictionary<string, SpellingFix> Fixes { get; }

        public SpellingData AddWords(IEnumerable<string> values)
        {
            WordList newList = List.AddValues(values);

            return new SpellingData(newList, IgnoreList, Fixes);
        }

        public SpellingData AddWord(string value)
        {
            return new SpellingData(List.AddValue(value), IgnoreList, Fixes);
        }

        public SpellingData AddFix(string error, string fix)
        {
            return new SpellingData(List, IgnoreList, Fixes.Add(error, new SpellingFix(error, fix)));
        }

        public SpellingData AddIgnoredValue(string value)
        {
            return new SpellingData(List, IgnoreList.AddValue(value), Fixes);
        }

        public static SpellingData LoadFromDirectory(string directoryPath)
        {
            WordList wordList = WordList.Default;

            foreach (string filePath in Directory.EnumerateFiles(directoryPath, "*.wordlist", SearchOption.TopDirectoryOnly))
            {
                string input = Path.GetFileNameWithoutExtension(filePath);

                if (!_wordListFileName.IsMatch(input))
                    continue;

                wordList = wordList.AddValues(WordList.Load(filePath));
            }

            WordList ignoreList = WordList.Default_CurrentCulture;

            foreach (string filePath in Directory.EnumerateFiles(directoryPath, "*.ignorelist", SearchOption.TopDirectoryOnly))
            {
                string input = Path.GetFileNameWithoutExtension(filePath);

                if (!_wordListFileName.IsMatch(input))
                    continue;

                ignoreList = ignoreList.AddValues(WordList.Load(filePath));
            }

            return new SpellingData(wordList, ignoreList);
        }
    }
}
