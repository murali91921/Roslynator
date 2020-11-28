// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Roslynator.Spelling;

namespace Roslynator.CommandLine
{
    internal static class WordListHelpers
    {
        public static void CompareWordList()
        {
            string basePath = @"E:\Projects\Roslynator\src\CommandLine\WordLists\roslynator.spelling.";

            WordList big = WordList.Load(basePath + "big.wordlist");
            WordList core = WordList.Load(basePath + "core.wordlist").SaveAndLoad();
            WordList core2 = WordList.Load(basePath + "core2.wordlist").SaveAndLoad();
            WordList it = WordList.Load(basePath + "it.wordlist").Except(core).Except(core2).SaveAndLoad();
            WordList misc = WordList.Load(basePath + "misc.wordlist").Except(core).Except(core2).Except(it).SaveAndLoad();
            WordList acronyms = WordList.Load(basePath + "acronyms.wordlist").Except(core).Except(core2).Except(it).SaveAndLoad();
            WordList names = WordList.Load(basePath + "names.wordlist").Except(core).Except(core2).Except(it).SaveAndLoad();

            WordList ignore2 = WordList.Load(basePath + "core.ignorelist2")
                .Except(big)
                .Except(core)
                .Except(core2)
                .Except(it)
                .Except(misc)
                .Except(acronyms)
                .SaveAndLoad();

            WordList all = big.AddValues(core).AddValues(core2).AddValues(it).AddValues(misc).AddValues(acronyms);

            string fixListPath = basePath + "core.fixlist";

            FixList fixList = FixList.Load(fixListPath);

            foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in fixList.Items)
            {
                if (all.Contains(kvp.Key))
                    Debug.Fail(kvp.Key);

                foreach (SpellingFix fix in kvp.Value)
                {
                    if (!all.Contains(fix.Value))
                        Debug.Fail(fix.Value);
                }
            }

            fixList.SaveAndLoad(fixListPath);
        }
    }
}
