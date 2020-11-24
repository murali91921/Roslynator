// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

            var options = new SpellingFixerOptions(
                includeLocal: false,
                includeGeneratedCode: Options.IncludeGeneratedCode,
                interactive: true);

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

                SpellingFixResult result = await spellingFixer.FixProjectAsync(project, cancellationToken);

                stopwatch.Stop();

                WriteLine($"Done fixing project '{project.FilePath}' in {stopwatch.Elapsed:mm\\:ss\\.ff}", Verbosity.Minimal);
            }
            else
            {
                Solution solution = projectOrSolution.AsSolution();

                spellingFixer = GetSpellingFixer(solution);

                await spellingFixer.FixSolutionAsync(f => projectFilter.IsMatch(f), cancellationToken);
            }

            WordList ignoreList = spellingFixer.SpellingData.IgnoreList;

            if (ignoreList.Count > 0)
            {
                var oldWordList = new WordList("roslynator.spelling.ignore.wordlist", StringComparer.CurrentCulture, null);

                if (File.Exists(oldWordList.Path))
                {
                    oldWordList = WordList.Load(oldWordList.Path, oldWordList.Comparer);
                }

                var wordList = new WordList(oldWordList.Path + ".new", oldWordList.Comparer, ignoreList.Values);

                wordList = wordList.Except(oldWordList);

                wordList.Save();
            }

            ImmutableDictionary<string, SpellingFix> fixes = spellingFixer.SpellingData.Fixes;

            if (fixes.Count > 0)
            {
                const string path = @"..\..\..\WordLists\roslynator.spelling.core.fixlist2";

                if (File.Exists(path))
                    fixes = fixes.SetItems(FixList.Load(path));

                FixList.Save(path, fixes);
            }

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

        protected override void OperationCanceled(OperationCanceledException ex)
        {
            WriteLine("Fixing was canceled.", Verbosity.Minimal);
        }
    }
}
