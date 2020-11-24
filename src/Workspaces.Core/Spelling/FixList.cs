// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Roslynator.Spelling
{
    internal static class FixList
    {
        public static ImmutableDictionary<string, SpellingFix> Load(string path)
        {
            ImmutableDictionary<string, SpellingFix>.Builder fixList = ImmutableDictionary.CreateBuilder<string, SpellingFix>();

            foreach ((string key, string value) in File.ReadLines(path)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f =>
                {
                    string value = f.Trim();
                    int index = value.IndexOf("=");

                    return (value, index);
                })
                .Where(f => f.index >= 0)
                .Select(f => (key: f.value.Remove(f.index), value: f.value.Substring(f.index + 1))))
            {
                fixList[key] = new SpellingFix(key, value);
            }

            return fixList.ToImmutableDictionary();
        }

        public static void Save(string path, ImmutableDictionary<string, SpellingFix> fixes)
        {
            File.WriteAllText(
                path,
                string.Join(
                    Environment.NewLine,
                    fixes
                        .OrderBy(f => f.Key)
                        .Select(f => $"{f.Key}={f.Value}")));
        }
    }
}
