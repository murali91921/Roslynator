// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Roslynator.CommandLine
{
    internal static class WordListHelpers
    {
        public static void CompareWordList()
        {
            WordList big1 = WordList.Load(@"E:\Projects\Roslynator\src\CommandLine\wordlist\big1.txt");
            WordList big2 = WordList.Load(@"E:\Projects\Roslynator\src\CommandLine\wordlist\big2.txt");
            WordList big3 = WordList.Load(@"E:\Projects\Roslynator\src\CommandLine\wordlist\big3.txt");
            WordList core = WordList.Load(@"E:\Projects\Roslynator\src\CommandLine\wordlist\core.txt");

            //WordList x = WordList.LoadText(@"");

            var derived = core.GeneratePrefixes().Except(core).Intersect(big3);

            derived.Save(core.Path + ".derived");
        }

        public static IEnumerable<string> Load(string path)
        {
            return File.ReadLines(path)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => f.Trim());
        }

        public static void Save(string path, IEnumerable<string> values)
        {
            Save(path, StringComparer.OrdinalIgnoreCase, values);
        }

        public static void Save(string path, StringComparer comparer, IEnumerable<string> values)
        {
            values = values
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => f.Trim().ToLower())
                .Distinct(comparer)
                .OrderBy(f => f, StringComparer.InvariantCulture);

            File.WriteAllText(path, string.Join(Environment.NewLine, values));
        }
    }
}
