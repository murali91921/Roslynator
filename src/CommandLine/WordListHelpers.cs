// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        private const string _wordListDirPath = @"..\..\..\_words";
        private const string _fixListDirPath = @"..\..\..\_fixes";

        public static void ProcessWordLists()
        {
            foreach (string filePath in Directory.EnumerateFiles(_wordListDirPath, "*.txt", SearchOption.AllDirectories))
            {
                WordList.Normalize(filePath);
            }

            _ = WordList.LoadFile(_wordListDirPath + @"\ignore.txt");
            WordList abbreviations = WordList.LoadFile(_wordListDirPath + @"\abbreviations.txt");
            WordList acronyms = WordList.LoadFile(_wordListDirPath + @"\acronyms.txt");
            WordList br = WordList.LoadFile(_wordListDirPath + @"\br.txt");
            WordList us = WordList.LoadFile(_wordListDirPath + @"\us.txt");
            WordList fonts = WordList.LoadFile(_wordListDirPath + @"\it\fonts.txt");
            WordList languages = WordList.LoadFile(_wordListDirPath + @"\it\languages.txt");
            WordList names = WordList.LoadFile(_wordListDirPath + @"\names.txt");
            WordList plural = WordList.LoadFile(_wordListDirPath + @"\plural.txt");
            WordList science = WordList.LoadFile(_wordListDirPath + @"\science.txt");

            WordList geography = WordList.LoadFiles(
                Directory.EnumerateFiles(
                    _wordListDirPath + @"\geography",
                    "*.*",
                    SearchOption.AllDirectories));

            WordList it = WordList.LoadFiles(
                Directory.EnumerateFiles(
                    _wordListDirPath + @"\it",
                    "*.*",
                    SearchOption.AllDirectories));

            WordList math = WordList.LoadFile(_wordListDirPath + @"\math.txt")
                .Except(abbreviations, acronyms, fonts);

            WordList @default = WordList.LoadFile(_wordListDirPath + @"\default.txt")
                .Except(br, us, geography);

            WordList custom = WordList.LoadFile(_wordListDirPath + @"\custom.txt")
                .Except(
                    abbreviations,
                    acronyms,
                    br,
                    us,
                    geography,
                    math,
                    names,
                    plural,
                    science,
                    it);

            WordList all = @default.AddValues(
                custom,
                br,
                us,
                languages,
                math,
                plural,
                abbreviations,
                acronyms,
                names,
                geography,
                WordList.LoadFile(_wordListDirPath + @"\it\main.txt"),
                WordList.LoadFile(_wordListDirPath + @"\it\names.txt"));

            ProcessFixList(all);
        }

        private static void ProcessFixList(WordList wordList)
        {
            const string path = _fixListDirPath + @"\fixes.txt";

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
            const string fixListPath = _fixListDirPath + @"\fixes.txt";
            const string fixListNewPath = _fixListDirPath + @"\fixes.tmp";
            const string wordListNewPath = _wordListDirPath + @"\custom.tmp";

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

                        return f;
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
