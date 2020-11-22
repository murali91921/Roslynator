// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.IO;
using Roslynator.Spelling;

namespace Roslynator.CommandLine
{
    internal static class WordListHelpers
    {
        public static void CompareWordList()
        {
            var basePath = @"E:\Projects\Roslynator\src\CommandLine\WordLists\roslynator.spelling.";

            WordList big1 = WordList.Load(basePath + "big1.wordlist");
            WordList big2 = WordList.Load(basePath + "big2.wordlist").SaveAndLoad();
            WordList core = WordList.Load(basePath + "core.wordlist").SaveAndLoad();
            WordList it = WordList.Load(basePath + "it.wordlist").Except(core).SaveAndLoad();

            WordList big = big1.AddValues(big2);

            string core2Path = basePath + "core2.wordlist";

            if (File.Exists(core2Path))
            {
                WordList.Load(core2Path).Except(core).Save();
            }

            //WordList prefixes = core.GeneratePrefixes().Except(core).Intersect(big);
            //prefixes.Save(core.Path + ".prefixes");

            //WordList suffixes = core.GenerateSuffixes().Except(core).Intersect(big);
            //suffixes.Save(core.Path + ".suffixes");

            WordList except = core.Except(big);

            if (except.Count > 0)
                big2.AddValues(except).Save();
        }
    }
}
