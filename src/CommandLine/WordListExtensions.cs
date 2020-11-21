// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Roslynator.CommandLine
{
    internal static class WordListExtensions
    {
        private static readonly string[] _prefixes = new[] {
            "a",
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

            foreach (string prefix in _prefixes)
            {
                foreach (string value in wordList.Values)
                    values.Add(prefix + value);
            }

            return wordList.WithValues(values);
        }

        public static WordList GenerateSuffixes(this WordList wordList)
        {
            var values = new List<string>();

            foreach (string value in wordList.Values)
            {
                if (value[value.Length - 1] == 's'
                    || value[value.Length - 1] == 'x'
                    || value[value.Length - 1] == 'z')
                {
                    values.Add(value + "es");
                }
                else if (value[value.Length - 1] == 'h'
                    && (value[value.Length - 2] == 'c'
                        || value[value.Length - 2] == 's'))
                {
                    values.Add(value + "es");
                }
                else if (value[value.Length - 1] == 'y')
                {
                    values.Add(value.Remove(value.Length - 1) + "ies");
                }
                else
                {
                    values.Add(value + "s");
                }
            }

            return wordList.WithValues(values);
        }
    }
}
