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

        private const string _wordListDirPath = @"..\..\..\_WordLists\";
        private const string _fixListDirPath = @"..\..\..\_FixLists\";

        public static void ProcessWordLists()
        {
            WordList core_br = WordList.Load(_wordListDirPath + "core.br.wordlist").SaveAndLoad();
            WordList fonts = WordList.Load(_wordListDirPath + "fonts.wordlist").Except(core_br).SaveAndLoad();
            WordList tech = WordList.Load(_wordListDirPath + "tech.wordlist").Except(core_br).Except(fonts).SaveAndLoad();
            WordList abbr = WordList.Load(_wordListDirPath + "abbr.wordlist").Except(core_br).Except(tech).SaveAndLoad();
            WordList core = WordList.Load(_wordListDirPath + "core.wordlist").Except(core_br).SaveAndLoad();
            WordList core2 = WordList.Load(_wordListDirPath + "core2.wordlist").Except(core_br).Except(tech).Except(abbr).SaveAndLoad();

            WordList.Load(_wordListDirPath + "names.wordlist").Except(core_br).Except(tech).SaveAndLoad();
            WordList.Load(_wordListDirPath + "hyphen.wordlist").Except(core2).SaveAndLoad();

            WordList all = core.AddValues(core2).AddValues(core_br).AddValues(tech).AddValues(abbr);
            ProcessFixList(all);
        }

        private static void ProcessFixList(WordList wordList)
        {
            const string path = _fixListDirPath + "core.fixlist";

            FixList fixList = FixList.Load(path);

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
            const string fixListPath = _fixListDirPath + "core.fixlist";
            const string fixListNewPath = fixListPath + ".new";
            const string wordListNewPath = _wordListDirPath + "core2.wordlist.new";

            Dictionary<string, List<SpellingFix>> dic = spellingData.FixList.Items.ToDictionary(
                f => f.Key,
                f => f.Value.ToList(),
                WordList.DefaultComparer);

            if (dic.Count > 0)
            {
                if (File.Exists(fixListNewPath))
                {
                    foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in FixList.Load(fixListNewPath).Items)
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

                foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in FixList.Load(fixListPath).Items)
                {
                    if (dic.TryGetValue(kvp.Key, out List<SpellingFix> list))
                    {
                        list.RemoveAll(f => kvp.Value.Contains(f, SpellingFixComparer.Default));

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
                    values.UnionWith(WordList.Load(wordListNewPath, comparer).Values);

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
                    .Distinct(SpellingFixComparer.Default)
                    .ToImmutableHashSet(SpellingFixComparer.Default));

            if (fixes.Count > 0)
                FixList.Save(fixListNewPath, fixes);
        }
    }
}
