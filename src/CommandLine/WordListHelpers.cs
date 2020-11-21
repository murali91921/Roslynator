// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Roslynator.CommandLine
{
    internal static class WordListHelpers
    {
        public static void CompareWordList()
        {
            WordList big1 = WordList.Load(@"E:\Projects\Roslynator\src\CommandLine\wordlist\big1.txt");

            WordList big2 = WordList.Load(@"E:\Projects\Roslynator\src\CommandLine\wordlist\big2.txt")
                .SaveAndLoad();

            WordList core = WordList.Load(@"E:\Projects\Roslynator\src\CommandLine\wordlist\core.txt")
                .SaveAndLoad();

            WordList it = WordList.Load(@"E:\Projects\Roslynator\src\CommandLine\roslynator.spelling.it.dictionary")
                .Except(core)
                .SaveAndLoad();

            //WordList core2 = WordList.Load(@"E:\Projects\Roslynator\src\CommandLine\wordlist\core2.txt").SaveAndLoad();

            WordList big = big1.AddValues(big2);

            WordList prefixes = core.GeneratePrefixes().Except(core).Intersect(big);
            WordList suffixes = core.GenerateSuffixes().Except(core).Intersect(big);

            prefixes.Save(core.Path + ".prefixes");
            suffixes.Save(core.Path + ".suffixes");

            WordList except = core.Except(big);
            Debug.Assert(except.Values.Count == 0, except.Values.Count.ToString());
            WordList.Save(big2.Path, except, append: true);
        }
    }
}
