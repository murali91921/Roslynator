﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Roslynator.Spelling
{
    internal class FixList
    {
        public static FixList Empty { get; }
            = new FixList(ImmutableDictionary.Create<string, ImmutableHashSet<SpellingFix>>(
                WordList.DefaultComparer));

        public FixList(ImmutableDictionary<string, ImmutableHashSet<SpellingFix>> values)
        {
            Items = values;
        }

        public ImmutableDictionary<string, ImmutableHashSet<SpellingFix>> Items { get; }

        public int Count => Items.Count;

        public bool ContainsKey(string key) => Items.ContainsKey(key);

        public bool TryGetValue(string key, out ImmutableHashSet<SpellingFix> value)
        {
            return Items.TryGetValue(key, out value);
        }

        public bool TryGetKey(string equalKey, out string actualKey)
        {
            return Items.TryGetKey(equalKey, out actualKey);
        }

        public FixList Add(string key, SpellingFix spellingFix)
        {
            if (Items.TryGetValue(key, out ImmutableHashSet<SpellingFix> fixes))
            {
                if (fixes.Contains(spellingFix))
                    return this;

                fixes = fixes.Add(spellingFix);

                ImmutableDictionary<string, ImmutableHashSet<SpellingFix>> values = Items.SetItem(key, fixes);

                return new FixList(values);
            }
            else
            {
                fixes = ImmutableHashSet.Create(SpellingFixComparer.CurrentCultureIgnoreCase, spellingFix);

                ImmutableDictionary<string, ImmutableHashSet<SpellingFix>> dic = Items.Add(key, fixes);

                return new FixList(dic);
            }
        }

        public static FixList LoadFiles(IEnumerable<string> filePaths)
        {
            var fixes = new Dictionary<string, List<string>>();

            foreach (string filePath in filePaths)
            {
                string input = Path.GetFileNameWithoutExtension(filePath);

                foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in LoadFile(filePath).Items)
                {
                    if (fixes.TryGetValue(kvp.Key, out List<string> values))
                    {
                        values.AddRange(kvp.Value.Select(f => f.Value));
                    }
                    else
                    {
                        fixes[kvp.Key] = kvp.Value.Select(f => f.Value).ToList();
                    }
                }
            }

            ImmutableDictionary<string, ImmutableHashSet<SpellingFix>> fixes2 = fixes.ToImmutableDictionary(
                f => f.Key,
                f => f.Value
                    .Distinct(StringComparer.CurrentCulture)
                    .Select(f => new SpellingFix(f, SpellingFixKind.Predefined))
                    .ToImmutableHashSet(SpellingFixComparer.CurrentCulture),
                WordList.DefaultComparer);

            return new FixList(fixes2);
        }

        public static FixList LoadFile(string path)
        {
            var dic = new Dictionary<string, HashSet<string>>(WordList.DefaultComparer);

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
                Debug.Assert(!string.Equals(key, value, StringComparison.Ordinal), $"{key} {value}");

                if (string.Equals(key, value, StringComparison.Ordinal))
                    continue;

                if (dic.TryGetValue(key, out HashSet<string> fixes))
                {
                    Debug.Assert(!fixes.Contains(value), $"Fix list already contains {key}={value}");

                    fixes.Add(value);
                    dic[key] = fixes;
                }
                else
                {
                    dic[key] = new HashSet<string>(WordList.DefaultComparer) { value };
                }
            }

            ImmutableDictionary<string, ImmutableHashSet<SpellingFix>> items
                = dic.ToImmutableDictionary(
                    f => f.Key,
                    f => f.Value
                        .Select(f => new SpellingFix(f, SpellingFixKind.Predefined))
                        .ToImmutableHashSet(SpellingFixComparer.CurrentCultureIgnoreCase),
                    WordList.DefaultComparer);

            return new FixList(items);
        }

        public void Save(string path)
        {
            Save(path, Items);
        }

        public static void Save(
            string path,
            IEnumerable<KeyValuePair<string, ImmutableHashSet<SpellingFix>>> values)
        {
            File.WriteAllText(
                path,
                string.Join(
                    Environment.NewLine,
                    values
                        .SelectMany(f => f.Value.Select(g => (key: f.Key, fix: g)))
                        .OrderBy(f => f.key)
                        .ThenBy(f => f.fix, SpellingFixComparer.CurrentCulture)
                        .Select(f => $"{f.key}={f.fix.Value}")));
        }

        public FixList SaveAndLoad(string path)
        {
            Save(path);

            return LoadFile(path);
        }
    }
}
