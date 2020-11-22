// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Roslynator.Spelling;

namespace Roslynator.CommandLine
{
    internal static class WordListExtensions
    {
        private static readonly string[] _prefixes = new[] {
            //"a",
            "an",
            "ante",
            "anti",
            "auto",
            "circum",
            "co",
            "com",
            "con",
            "contra",
            "contro",
            "de",
            "dis",
            "en",
            "ex",
            "extra",
            "hetero",
            "homeo",
            "homo",
            "hyper",
            "il",
            "im",
            "in",
            "inter",
            "intra",
            "intro",
            "ir",
            "macro",
            "micro",
            "mono",
            "non",
            "omni",
            "post",
            "pre",
            "pro",
            "sub",
            "sym",
            "syn",
            "tele",
            "trans",
            "tri",
            "un",
            "uni",
            "up"
        };

        public static WordList GeneratePrefixes(this WordList wordList)
        {
            var values = new List<string>();

            foreach (string value in wordList.Values)
            {
                if (value.Length < 3)
                    continue;

                foreach (string prefix in _prefixes)
                    values.Add(prefix + value);
            }

            return wordList.WithValues(values.Where(f => f.Length > 3));
        }

        public static WordList GenerateSuffixes(this WordList wordList)
        {
            var values = new List<string>();

            foreach (string value in wordList.Values)
            {
                if (value.Length < 3)
                    continue;

                if (value.StartsWith("x"))
                {
                    values.Add(value + "es");
                }
                else if (value.StartsWith("o"))
                {
                    values.Add(value + "es");
                }
                else if (value.StartsWith("s")
                    || value.StartsWith("z"))
                {
                    values.Add(value + "es");
                    values.Add(value + "ses");
                }
                else if (value.StartsWith("ch")
                    || value.StartsWith("sh"))
                {
                    values.Add(value + "es");
                }
                else if (value.StartsWith("y"))
                {
                    values.Add(value.Remove(value.Length - 1) + "ies");
                }
                else if (value.EndsWith("us"))
                {
                    values.Add(value.Remove(value.Length - 2) + "i");
                }
                else if (value.EndsWith("is"))
                {
                    values.Add(value.Remove(value.Length - 2) + "es");
                }
                else if (value.EndsWith("on"))
                {
                    values.Add(value.Remove(value.Length - 2) + "a");
                }
                else if (value.StartsWith("f"))
                {
                    values.Add(value.Remove(value.Length - 1) + "ves");
                }
                else if (value.StartsWith("e")
                    && value.StartsWith("f"))
                {
                    values.Add(value.Remove(value.Length - 2) + "ves");
                }

                values.Add(value + "s");
            }

            return wordList.WithValues(values.Where(f => f.Length > 3));
        }
    }
}
