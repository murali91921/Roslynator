// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Roslynator.CodeFixes;
using Roslynator.Spelling;
using static Roslynator.Logger;
using System.Collections.Immutable;

namespace Roslynator.CommandLine
{
    internal class FixCommand : MSBuildWorkspaceCommand
    {
        public FixCommand(
            FixCommandLineOptions options,
            DiagnosticSeverity severityLevel,
            IEnumerable<KeyValuePair<string, string>> diagnosticFixMap,
            IEnumerable<KeyValuePair<string, string>> diagnosticFixerMap,
            in ProjectFilter projectFilter) : base(projectFilter)
        {
            Options = options;
            SeverityLevel = severityLevel;
            DiagnosticFixMap = diagnosticFixMap;
            DiagnosticFixerMap = diagnosticFixerMap;
        }

        public FixCommandLineOptions Options { get; }

        public DiagnosticSeverity SeverityLevel { get; }

        public IEnumerable<KeyValuePair<string, string>> DiagnosticFixMap { get; }

        public IEnumerable<KeyValuePair<string, string>> DiagnosticFixerMap { get; }

        public override async Task<CommandResult> ExecuteAsync(ProjectOrSolution projectOrSolution, CancellationToken cancellationToken = default)
        {
            AssemblyResolver.Register();

            var codeFixerOptions = new CodeFixerOptions(
                severityLevel: SeverityLevel,
                ignoreCompilerErrors: Options.IgnoreCompilerErrors,
                ignoreAnalyzerReferences: Options.IgnoreAnalyzerReferences,
                supportedDiagnosticIds: Options.SupportedDiagnostics,
                ignoredDiagnosticIds: Options.IgnoredDiagnostics,
                ignoredCompilerDiagnosticIds: Options.IgnoredCompilerDiagnostics,
                diagnosticIdsFixableOneByOne: Options.DiagnosticsFixableOneByOne,
                diagnosticFixMap: DiagnosticFixMap,
                diagnosticFixerMap: DiagnosticFixerMap,
                fileBanner: Options.FileBanner,
                maxIterations: Options.MaxIterations,
                batchSize: Options.BatchSize,
                format: Options.Format);

            IEnumerable<AnalyzerAssembly> analyzerAssemblies = Options.AnalyzerAssemblies
                .SelectMany(path => AnalyzerAssemblyLoader.LoadFrom(path).Select(info => info.AnalyzerAssembly));

            CultureInfo culture = (Options.Culture != null) ? CultureInfo.GetCultureInfo(Options.Culture) : null;

            var projectFilter = new ProjectFilter(Options.Projects, Options.IgnoredProjects, Language);

            return await FixAsync(projectOrSolution, analyzerAssemblies, codeFixerOptions, projectFilter, culture, cancellationToken);
        }

        internal static async Task<CommandResult> FixAsync(
            ProjectOrSolution projectOrSolution,
            IEnumerable<AnalyzerAssembly> analyzerAssemblies,
            CodeFixerOptions codeFixerOptions,
            ProjectFilter projectFilter,
            IFormatProvider formatProvider = null,
            CancellationToken cancellationToken = default)
        {
            CodeFixer codeFixer;

            if (projectOrSolution.IsProject)
            {
                Project project = projectOrSolution.AsProject();

                Solution solution = project.Solution;

                codeFixer = GetCodeFixer(solution);

                WriteLine($"Fix '{project.Name}'", ConsoleColor.Cyan, Verbosity.Minimal);

                Stopwatch stopwatch = Stopwatch.StartNew();

                ProjectFixResult result = await codeFixer.FixProjectAsync(project, cancellationToken);

                stopwatch.Stop();

                WriteLine($"Done fixing project '{project.FilePath}' in {stopwatch.Elapsed:mm\\:ss\\.ff}", Verbosity.Minimal);

                LogHelpers.WriteProjectFixResults(new ProjectFixResult[] { result }, codeFixerOptions, formatProvider);
            }
            else
            {
                Solution solution = projectOrSolution.AsSolution();

                codeFixer = GetCodeFixer(solution);

                await codeFixer.FixSolutionAsync(f => projectFilter.IsMatch(f), cancellationToken);
            }

            WordList ignoreList = codeFixer.SpellingData.IgnoreList;

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

            ImmutableDictionary<string, string> fixes = codeFixer.SpellingData.Fixes;

            if (fixes.Count > 0)
            {
                const string path = @"..\..\..\WordLists\fixes.txt";

                IEnumerable<KeyValuePair<string, string>> items = Enumerable.Empty<KeyValuePair<string, string>>();

                if (File.Exists(path))
                {
                    items = File.ReadLines(path)
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .Select(f =>
                        {
                            int index = f.IndexOf("=");

                            return new KeyValuePair<string, string>(f.Remove(index), f.Substring(index + 1));
                        });
                }

                items = items.Concat(fixes).OrderBy(f => f.Key);

                File.WriteAllText(path, string.Join(Environment.NewLine, items.Select(f => $"{f.Key}={f.Value}")));
            }

            return CommandResult.Success;

            CodeFixer GetCodeFixer(Solution solution)
            {
                SpellingData spellingData = SpellingData.Empty;

                string assemblyPath = typeof(FixCommand).Assembly.Location;

                if (!string.IsNullOrEmpty(assemblyPath))
                    spellingData = SpellingData.LoadFromDirectory(Path.Combine(Path.GetDirectoryName(assemblyPath), "WordLists"));

                return new CodeFixer(
                    solution,
                    analyzerAssemblies: analyzerAssemblies,
                    formatProvider: formatProvider,
                    spellingData: spellingData,
                    options: codeFixerOptions);
            }
        }

        protected override void OperationCanceled(OperationCanceledException ex)
        {
            WriteLine("Fixing was canceled.", Verbosity.Quiet);
        }
    }
}
