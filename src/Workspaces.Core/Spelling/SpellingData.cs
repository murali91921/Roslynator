// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Roslynator.Spelling
{
    internal class SpellingData
    {
        private static readonly Regex _wordListFileName = new Regex(
            @"\Aroslynator\.spelling(\.|\z)", RegexOptions.IgnoreCase);

        public static SpellingData Empty { get; } = new SpellingData(
            WordList.Default,
            WordList.Default_CurrentCulture,
            FixList.Empty);

        public SpellingData(
            WordList list,
            WordList ignoreList,
            FixList fixList = null)
        {
            List = list;
            IgnoreList = ignoreList;
            FixList = fixList ?? FixList.Empty;
        }

        public WordList List { get; }

        public WordList IgnoreList { get; }

        public FixList FixList { get; }

        public SpellingData AddWords(IEnumerable<string> values)
        {
            WordList newList = List.AddValues(values);

            return new SpellingData(newList, IgnoreList, FixList);
        }

        public SpellingData AddWord(string value)
        {
            return new SpellingData(List.AddValue(value), IgnoreList, FixList);
        }

        public SpellingData AddFix(string error, string fix, SpellingFixKind kind)
        {
            return AddFix(error, new SpellingFix(fix, kind));
        }

        public SpellingData AddFix(string error, SpellingFix spellingFix)
        {
            FixList fixList = FixList.Add(error, spellingFix);

            return new SpellingData(List, IgnoreList, fixList);
        }

        public SpellingData AddIgnoredValue(string value)
        {
            return new SpellingData(List, IgnoreList.AddValue(value), FixList);
        }

        public SpellingData AddIgnoredValues(IEnumerable<SpellingError> errors)
        {
            return AddIgnoredValues(errors.Select(f => f.Value));
        }

        public SpellingData AddIgnoredValues(IEnumerable<string> values)
        {
            return new SpellingData(List, IgnoreList.AddValues(values), FixList);
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

            var fixes = new Dictionary<string, List<string>>();

            foreach (string filePath in Directory.EnumerateFiles(directoryPath, "*.fixlist", SearchOption.TopDirectoryOnly))
            {
                string input = Path.GetFileNameWithoutExtension(filePath);

                if (!_wordListFileName.IsMatch(input))
                    continue;

                FixList fixList2 = FixList.Load(filePath);

                foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in fixList2.Items)
                {
                    if (fixes.TryGetValue(kvp.Key, out List<string> fixes2))
                    {
                        fixes2.AddRange(kvp.Value.Select(f => f.Value));
                    }
                    else
                    {
                        fixes[kvp.Key] = kvp.Value.Select(f => f.Value).ToList();
                    }
                }
            }

            ImmutableDictionary<string, ImmutableHashSet<SpellingFix>> fixList = fixes.ToImmutableDictionary(
                f => f.Key,
                f => f.Value
                    .Distinct(StringComparer.CurrentCulture)
                    .Select(f => new SpellingFix(f, SpellingFixKind.List))
                    .ToImmutableHashSet(SpellingFixComparer.CurrentCulture),
                WordList.DefaultComparer);

            return new SpellingData(wordList, ignoreList, new FixList(fixList));
        }
    }
}
