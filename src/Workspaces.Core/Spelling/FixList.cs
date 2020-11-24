// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Roslynator.Spelling
{
    internal class FixList
    {
        public static FixList Empty { get; }
            = new FixList(ImmutableDictionary.Create<string, ImmutableHashSet<string>>(
                WordList.DefaultComparer));

        public FixList(ImmutableDictionary<string, ImmutableHashSet<string>> values)
        {
            Items = values;
        }

        public ImmutableDictionary<string, ImmutableHashSet<string>> Items { get; }

        public int Count => Items.Count;

        public bool ContainsKey(string key) => Items.ContainsKey(key);

        public bool TryGetValue(string key, out ImmutableHashSet<string> value)
        {
            return Items.TryGetValue(key, out value);
        }

        public FixList Add(string key, string fix)
        {
            if (Items.TryGetValue(key, out ImmutableHashSet<string> fixes))
            {
                if (fixes.Contains(fix))
                    return this;

                fixes = fixes.Add(fix);

                ImmutableDictionary<string, ImmutableHashSet<string>> values = Items.SetItem(key, fixes);

                return new FixList(values);
            }
            else
            {
                fixes = ImmutableHashSet.Create<string>(fix);

                ImmutableDictionary<string, ImmutableHashSet<string>> map = Items.Add(key, fixes);

                return new FixList(map);
            }
        }

        public static FixList Load(string path)
        {
            var dic = new Dictionary<string, HashSet<string>>();

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
                if (dic.TryGetValue(key, out HashSet<string> fixes))
                {
                    fixes.Add(value);
                    dic[key] = fixes;
                }
                else
                {
                    dic[key] = new HashSet<string>(StringComparer.CurrentCulture) { value };
                }
            }

            ImmutableDictionary<string, ImmutableHashSet<string>> items
                = dic.ToImmutableDictionary(
                    f => f.Key,
                    f => f.Value
                        .ToImmutableHashSet(StringComparer.CurrentCulture),
                    WordList.DefaultComparer);

            return new FixList(items);
        }

        public void Save(string path)
        {
            Save(path, Items);
        }

        public static void Save(
            string path,
            IEnumerable<KeyValuePair<string, ImmutableHashSet<string>>> values)
        {
            File.WriteAllText(
                path,
                string.Join(
                    Environment.NewLine,
                    values
                        .SelectMany(f => f.Value.Select(g => (key: f.Key, fix: g)))
                        .OrderBy(f => f.key)
                        .ThenBy(f => f.fix)
                        .Select(f => $"{f.key}={f.fix}")));
        }
    }
}
