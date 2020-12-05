﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#define NON_INTERACTIVE

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Roslynator.Spelling;
using static Roslynator.Logger;

namespace Roslynator.CommandLine
{
    internal class FixSpellingCommand : MSBuildWorkspaceCommand
    {
        public FixSpellingCommand(FixSpellingCommandLineOptions options, in ProjectFilter projectFilter) : base(projectFilter)
        {
            Options = options;
        }

        public FixSpellingCommandLineOptions Options { get; }

        public override async Task<CommandResult> ExecuteAsync(ProjectOrSolution projectOrSolution, CancellationToken cancellationToken = default)
        {
            AssemblyResolver.Register();

#if NON_INTERACTIVE
            var options = new SpellingFixerOptions(
                splitMode: SplitMode.CaseAndHyphen,
                includeLocal: true,
                includeGeneratedCode: Options.IncludeGeneratedCode,
                interactive: false,
                dryRun: true);
#else
            var options = new SpellingFixerOptions(
                splitMode: SplitMode.CaseAndHyphen,
                includeLocal: false,
                includeGeneratedCode: Options.IncludeGeneratedCode,
                interactive: true);
#endif
            CultureInfo culture = (Options.Culture != null) ? CultureInfo.GetCultureInfo(Options.Culture) : null;

            var projectFilter = new ProjectFilter(Options.Projects, Options.IgnoredProjects, Language);

            return await FixAsync(projectOrSolution, options, projectFilter, culture, cancellationToken);
        }

        internal static async Task<CommandResult> FixAsync(
            ProjectOrSolution projectOrSolution,
            SpellingFixerOptions options,
            ProjectFilter projectFilter,
            IFormatProvider formatProvider = null,
            CancellationToken cancellationToken = default)
        {
            SpellingFixer spellingFixer;

            if (projectOrSolution.IsProject)
            {
                Project project = projectOrSolution.AsProject();

                Solution solution = project.Solution;

                spellingFixer = GetSpellingFixer(solution);

                WriteLine($"Fix '{project.Name}'", ConsoleColor.Cyan, Verbosity.Minimal);

                Stopwatch stopwatch = Stopwatch.StartNew();

                ImmutableArray<SpellingFixResult> results = await spellingFixer.FixProjectAsync(project, cancellationToken);

                stopwatch.Stop();

                WriteLine($"Done fixing project '{project.FilePath}' in {stopwatch.Elapsed:mm\\:ss\\.ff}", Verbosity.Minimal);
            }
            else
            {
                Solution solution = projectOrSolution.AsSolution();

                spellingFixer = GetSpellingFixer(solution);

                await spellingFixer.FixSolutionAsync(f => projectFilter.IsMatch(f), cancellationToken);
            }

            SaveNewValues(spellingFixer.SpellingData, cancellationToken);

            return CommandResult.Success;

            SpellingFixer GetSpellingFixer(Solution solution)
            {
                SpellingData spellingData = SpellingData.Empty;

                string assemblyPath = typeof(FixCommand).Assembly.Location;

                if (!string.IsNullOrEmpty(assemblyPath))
                    spellingData = SpellingData.LoadFromDirectory(Path.Combine(Path.GetDirectoryName(assemblyPath), "WordLists"));

                return new SpellingFixer(
                    solution,
                    spellingData: spellingData,
                    formatProvider: formatProvider,
                    options: options);
            }
        }

        [Conditional("DEBUG")]
        private static void SaveNewValues(
            SpellingData spellingData,
            CancellationToken cancellationToken)
        {
            const string fixListPath = @"..\..\..\WordLists\roslynator.spelling.core.fixlist";
            const string fixListTmpPath = fixListPath + ".tmp";
            const string ignoreListPath = @"..\..\..\WordLists\roslynator.spelling.core.wordlist.tmp";

            Dictionary<string, List<SpellingFix>> dic = spellingData.FixList.Items.ToDictionary(
                f => f.Key,
                f => f.Value.ToList(),
                WordList.DefaultComparer);

            if (dic.Count > 0)
            {
                if (File.Exists(fixListTmpPath))
                {
                    foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in FixList.Load(fixListTmpPath).Items)
                    {
                        if (dic.TryGetValue(kvp.Key, out List<SpellingFix> list))
                        {
                            list.AddRange(kvp.Value);
                        }
                        else
                        {
                            dic[kvp.Key] = kvp.Value.ToList();
                        }
                    }
                }

                foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in FixList.Load(fixListPath).Items)
                {
                    if (dic.TryGetValue(kvp.Key, out List<SpellingFix> list))
                    {
                        list.RemoveAll(f => kvp.Value.Contains(f, SpellingFixComparer.Default));

                        if (list.Count == 0)
                            dic.Remove(kvp.Key);
                    }
                }
            }

            StringComparer comparer = StringComparer.CurrentCulture;

            HashSet<string> values = spellingData.IgnoreList.Values.ToHashSet(comparer);

            if (values.Count > 0)
            {
                if (File.Exists(ignoreListPath))
                    values.UnionWith(WordList.Load(ignoreListPath, comparer).Values);

                IEnumerable<string> newValues = values
                    .Except(spellingData.FixList.Items.Select(f => f.Key), WordList.DefaultComparer)
                    .Distinct(StringComparer.CurrentCulture)
                    .OrderBy(f => f)
                    .Select(f =>
                    {
                        string value = f.ToLowerInvariant();

                        var fixes = new List<string>();

                        fixes.AddRange(SpellingFixProvider.SwapMatches(
                            value,
                            spellingData));

                        if (fixes.Count == 0
                            && value.Length >= 8)
                        {
                            fixes.AddRange(SpellingFixProvider.FuzzyMatches(
                                value,
                                spellingData,
                                cancellationToken));
                        }

                        if (fixes.Count > 0)
                        {
                            var spellingFix = new SpellingFix(string.Join(",", fixes), SpellingFixKind.None);

                            if (dic.TryGetValue(value, out List<SpellingFix> list))
                            {
                                list.Add(spellingFix);
                            }
                            else
                            {
                                dic[value] = new List<SpellingFix>() { spellingFix };
                            }

                            return null;
                        }

                        return value;
                    })
                    .Where(f => f != null);

                WordList.Save(ignoreListPath, newValues, comparer);
            }

            ImmutableDictionary<string, ImmutableHashSet<SpellingFix>> fixes = dic.ToImmutableDictionary(
                f => f.Key.ToLowerInvariant(),
                f => f.Value
                    .Select(f => f.WithValue(f.Value.ToLowerInvariant()))
                    .Distinct(SpellingFixComparer.Default)
                    .ToImmutableHashSet(SpellingFixComparer.Default));

            if (fixes.Count > 0)
                FixList.Save(fixListTmpPath, fixes);
        }

        protected override void OperationCanceled(OperationCanceledException ex)
        {
            WriteLine("Fixing was canceled.", Verbosity.Minimal);
        }
    }
}
