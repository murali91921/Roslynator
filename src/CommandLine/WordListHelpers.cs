﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Roslynator.Spelling;

namespace Roslynator.CommandLine
{
    internal static class WordListHelpers
    {
        private static readonly Regex _splitRegex = new Regex(" +");

        private const string _wordListDirPath = @"..\..\..\_words\";
        private const string _fixListDirPath = @"..\..\..\_fixes\";

        public static void ProcessWordLists()
        {
            _ = WordList.LoadFile(_wordListDirPath + "exclude.txt")
                .SaveAndLoad();

            WordList abbreviations = WordList.LoadFile(_wordListDirPath + "abbreviations.txt")
                .SaveAndLoad();

            WordList acronyms = WordList.LoadFile(_wordListDirPath + "acronyms.txt")
                .SaveAndLoad();

            WordList br = WordList.LoadFile(_wordListDirPath + "br.txt")
                .SaveAndLoad();

            WordList fonts = WordList.LoadFile(_wordListDirPath + @"\it\fonts.txt")
                .SaveAndLoad();

            WordList geography = WordList.LoadFiles(Directory.EnumerateFiles(_wordListDirPath + @"\geography", "*.*", SearchOption.AllDirectories));

            WordList languages = WordList.LoadFile(_wordListDirPath + @"\it\languages.txt")
                .SaveAndLoad();

            WordList names = WordList.LoadFile(_wordListDirPath + "names.txt")
                .Except(languages)
                .SaveAndLoad();

            WordList plural = WordList.LoadFile(_wordListDirPath + "plural.txt")
                .SaveAndLoad();

            WordList rare = WordList.LoadFile(_wordListDirPath + "rare.txt")
                .SaveAndLoad();

            WordList math = WordList.LoadFile(_wordListDirPath + "math.txt")
                .Except(
                    abbreviations,
                    acronyms,
                    fonts)
                .SaveAndLoad();

            WordList it = WordList.LoadFile(_wordListDirPath + "it.txt")
                .Except(
                    abbreviations,
                    acronyms,
                    fonts,
                    languages)
                .SaveAndLoad();

            WordList main2 = WordList.LoadFile(_wordListDirPath + "main2.txt")
                .Except(
                    abbreviations,
                    acronyms,
                    br,
                    geography,
                    math,
                    names,
                    plural,
                    rare,
                    it)
                .SaveAndLoad();

            WordList main = WordList.LoadFile(_wordListDirPath + "main.txt")
                .Except(
                    br,
                    geography)
                .SaveAndLoad();

            WordList.LoadFile(_wordListDirPath + "hyphen.txt")
                .Except(main2)
                .SaveAndLoad();

            WordList all = main.AddValues(
                main2,
                br,
                languages,
                math,
                plural,
                abbreviations,
                names);

            ProcessFixList(all);
        }

        private static void ProcessFixList(WordList wordList)
        {
            const string path = _fixListDirPath + "fixes.txt";

            FixList fixList = FixList.LoadFile(path);

            foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in fixList.Items)
            {
                if (wordList.Contains(kvp.Key))
                    Debug.Fail(kvp.Key);

                foreach (SpellingFix fix in kvp.Value)
                {
                    string value = fix.Value;

                    foreach (string value2 in _splitRegex.Split(value))
                    {
                        if (!wordList.Contains(value2))
                            Debug.Fail($"{value}: {value2}");
                    }
                }
            }

            fixList.SaveAndLoad(path);
        }

        public static void SaveNewValues(
            SpellingData spellingData,
            CancellationToken cancellationToken)
        {
            const string fixListPath = _fixListDirPath + "main.txt";
            const string fixListNewPath = _fixListDirPath + "fixes.tmp";
            const string wordListNewPath = _wordListDirPath + "main.tmp";

            Dictionary<string, List<SpellingFix>> dic = spellingData.FixList.Items.ToDictionary(
                f => f.Key,
                f => f.Value.ToList(),
                WordList.DefaultComparer);

            if (dic.Count > 0)
            {
                if (File.Exists(fixListNewPath))
                {
                    foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in FixList.LoadFile(fixListNewPath).Items)
                    {
                        if (dic.TryGetValue(kvp.Key, out List<SpellingFix> list))
                        {
                            list.AddRange(kvp.Value);
                        }
                        else
                        {
                            dic[kvp.Key] = kvp.Value.ToList();
                        }
                    }
                }

                foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in FixList.LoadFile(fixListPath).Items)
                {
                    if (dic.TryGetValue(kvp.Key, out List<SpellingFix> list))
                    {
                        list.RemoveAll(f => kvp.Value.Contains(f, SpellingFixComparer.CurrentCultureIgnoreCase));

                        if (list.Count == 0)
                            dic.Remove(kvp.Key);
                    }
                }
            }

            StringComparer comparer = StringComparer.CurrentCulture;

            HashSet<string> values = spellingData.IgnoreList.Values.ToHashSet(comparer);

            if (values.Count > 0)
            {
                if (File.Exists(wordListNewPath))
                    values.UnionWith(WordList.LoadFile(wordListNewPath, comparer).Values);

                IEnumerable<string> newValues = values
                    .Except(spellingData.FixList.Items.Select(f => f.Key), WordList.DefaultComparer)
                    .Distinct(StringComparer.CurrentCulture)
                    .OrderBy(f => f)
                    .Select(f =>
                    {
                        string value = f.ToLowerInvariant();

                        var fixes = new List<string>();

                        fixes.AddRange(SpellingFixProvider.SwapMatches(
                            value,
                            spellingData));

                        if (fixes.Count == 0
                            && value.Length >= 8)
                        {
                            fixes.AddRange(SpellingFixProvider.FuzzyMatches(
                                value,
                                spellingData,
                                cancellationToken));
                        }

                        if (fixes.Count > 0)
                        {
                            IEnumerable<SpellingFix> spellingFixes = fixes
                                .Select(fix => new SpellingFix(fix, SpellingFixKind.None));

                            if (dic.TryGetValue(value, out List<SpellingFix> list))
                            {
                                list.AddRange(spellingFixes);
                            }
                            else
                            {
                                dic[value] = new List<SpellingFix>(spellingFixes);
                            }

                            return null;
                        }

                        return value;
                    })
                    .Where(f => f != null);

                WordList.Save(wordListNewPath, newValues, comparer);
            }

            ImmutableDictionary<string, ImmutableHashSet<SpellingFix>> fixes = dic.ToImmutableDictionary(
                f => f.Key.ToLowerInvariant(),
                f => f.Value
                    .Select(f => f.WithValue(f.Value.ToLowerInvariant()))
                    .Distinct(SpellingFixComparer.CurrentCultureIgnoreCase)
                    .ToImmutableHashSet(SpellingFixComparer.CurrentCultureIgnoreCase));

            if (fixes.Count > 0)
                FixList.Save(fixListNewPath, fixes);
        }
    }
}
