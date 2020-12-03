// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Roslynator.Spelling;

namespace Roslynator.CommandLine
{
    internal static class WordListHelpers
    {
        private static readonly Regex _splitRegex = new Regex(" +");

        public static void CompareWordList()
        {
            string basePath = @"E:\Projects\Roslynator\src\CommandLine\WordLists\roslynator.spelling.";

            WordList br = WordList.Load(basePath + "core_br.wordlist").SaveAndLoad();
            WordList tech = WordList.Load(basePath + "it.wordlist").Except(br).SaveAndLoad();
            WordList abbr = WordList.Load(basePath + "abbr.wordlist").Except(br).Except(tech).SaveAndLoad();
            WordList names = WordList.Load(basePath + "names.wordlist").Except(br).Except(tech).SaveAndLoad();
            WordList big = WordList.Load(basePath + "big.wordlist").Except(br).SaveAndLoad();
            WordList core = WordList.Load(basePath + "core.wordlist").Except(br).Except(tech).Except(abbr).SaveAndLoad();
            WordList misc = WordList.Load(basePath + "misc.wordlist").Except(core).Except(br).Except(tech).SaveAndLoad();

            WordList all = big.AddValues(core).AddValues(br).AddValues(tech).AddValues(misc).AddValues(abbr);

            string fixListPath = basePath + "core.fixlist";

            FixList fixList = FixList.Load(fixListPath);

            foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in fixList.Items)
            {
                if (all.Contains(kvp.Key))
                    Debug.Fail(kvp.Key);

                foreach (SpellingFix fix in kvp.Value)
                {
                    string value = fix.Value;

                    foreach (string value2 in _splitRegex.Split(value))
                    {
                        if (!all.Contains(value2))
                            Debug.Fail($"{value}: {value2}");
                    }
                }
            }

            fixList.SaveAndLoad(fixListPath);
        }
    }
}
