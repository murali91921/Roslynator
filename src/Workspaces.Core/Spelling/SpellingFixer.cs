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
using Microsoft.CodeAnalysis.Text;
using static Roslynator.Logger;

namespace Roslynator.Spelling
{
    internal class SpellingFixer
    {
        public SpellingFixer(
            Solution solution,
            SpellingData spellingData,
            IFormatProvider formatProvider = null,
            SpellingFixerOptions options = null)
        {
            Workspace = solution.Workspace;

            SpellingData = spellingData;
            FormatProvider = formatProvider;
            Options = options ?? SpellingFixerOptions.Default;
        }

        public Workspace Workspace { get; }

        public SpellingData SpellingData { get; private set; }

        public IFormatProvider FormatProvider { get; }

        public SpellingFixerOptions Options { get; }

        private Solution CurrentSolution => Workspace.CurrentSolution;

        public async Task FixSolutionAsync(Func<Project, bool> predicate, CancellationToken cancellationToken = default)
        {
            ImmutableArray<ProjectId> projects = CurrentSolution
                .GetProjectDependencyGraph()
                .GetTopologicallySortedProjects(cancellationToken)
                .ToImmutableArray();

            var results = new List<SpellingFixResult>();

            Stopwatch stopwatch = Stopwatch.StartNew();

            TimeSpan lastElapsed = TimeSpan.Zero;

            for (int i = 0; i < projects.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Project project = CurrentSolution.GetProject(projects[i]);

                if (predicate == null || predicate(project))
                {
                    WriteLine($"Fix '{project.Name}' {$"{i + 1}/{projects.Length}"}", ConsoleColor.Cyan, Verbosity.Minimal);

                    SpellingFixResult result = await FixProjectAsync(project, cancellationToken).ConfigureAwait(false);

                    results.Add(result);
                }
                else
                {
                    WriteLine($"Skip '{project.Name}' {$"{i + 1}/{projects.Length}"}", ConsoleColor.DarkGray, Verbosity.Minimal);

                    //results.Add(SpellingFixResult.Skipped);
                }

                TimeSpan elapsed = stopwatch.Elapsed;

                WriteLine($"Done fixing '{project.Name}' in {elapsed - lastElapsed:mm\\:ss\\.ff}", Verbosity.Normal);

                lastElapsed = elapsed;
            }

            stopwatch.Stop();

            WriteLine($"Done fixing solution '{CurrentSolution.FilePath}' in {stopwatch.Elapsed:mm\\:ss\\.ff}", Verbosity.Minimal);
        }

        public async Task<SpellingFixResult> FixProjectAsync(Project project, CancellationToken cancellationToken = default)
        {
            project = CurrentSolution.GetProject(project.Id);

            while (true)
            {
                SpellingAnalysisResult spellingAnalysisResult = await SpellingAnalyzer.AnalyzeSpellingAsync(
                    project,
                    SpellingData,
                    new SpellingFixerOptions(includeLocal: false),
                    cancellationToken)
                    .ConfigureAwait(false);

                ImmutableArray<SpellingError> errors = spellingAnalysisResult.Errors;

                if (!errors.Any())
                    return default;

                List<SpellingError> commentErrors = errors.Where(f => !f.IsSymbol).ToList();
                List<SpellingError> identifierErrors = errors.Where(f => f.IsSymbol).ToList();

                await FixCommentsAsync(project, commentErrors, cancellationToken).ConfigureAwait(false);
                await FixIdentifiersAsync(project, identifierErrors, cancellationToken).ConfigureAwait(false);

                project = CurrentSolution.GetProject(project.Id);
            }
        }

        private async Task FixCommentsAsync(
            Project project,
            List<SpellingError> errors,
            CancellationToken cancellationToken)
        {
            var applyChanges = false;
            var errorsRemoved = false;

            project = CurrentSolution.GetProject(project.Id);

            Document document = null;

            while (errors.Count > 0)
            {
                foreach (IGrouping<SyntaxTree, SpellingError> grouping in errors
                    .GroupBy(f => f.Location.SourceTree))
                {
                    document = project.GetDocument(grouping.Key);

                    SourceText sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

                    TextLineCollection lines = sourceText.Lines;

                    List<TextChange> textChanges = null;

                    foreach (SpellingError error in grouping.OrderBy(f => f.Location.SourceSpan.Start))
                    {
                        LogHelpers.WriteSpellingError(error, sourceText, Path.GetDirectoryName(project.FilePath), "    ", Verbosity.Normal);

                        if (SpellingData.IgnoreList.Contains(error.Value))
                            continue;

                        string fix = GetFix(error);

                        if (!string.IsNullOrEmpty(fix)
                            && !string.Equals(fix, error.Value, StringComparison.Ordinal))
                        {
                            (textChanges ??= new List<TextChange>()).Add(new TextChange(error.Location.SourceSpan, fix));

                            ProcessFix(error, fix);

                            errors.Remove(error);
                        }
                        else
                        {
                            SpellingData = SpellingData.AddIgnoredValue(error.Value);

                            if (errors.RemoveAll(f => SpellingData.IgnoreList.Comparer.Equals(f.Value, error.Value)) > 0)
                            {
                                errorsRemoved = true;
                                break;
                            }
                        }
                    }

                    if (textChanges != null
                        && (!errorsRemoved || !errors.Any(f => f.Location.SourceTree == grouping.Key)))
                    {
                        document = await document.WithTextChangesAsync(textChanges, cancellationToken).ConfigureAwait(false);
                        project = document.Project;

                        applyChanges = true;
                    }

                    if (errorsRemoved)
                    {
                        errorsRemoved = false;
                        break;
                    }
                }
            }

            if (applyChanges
                && !Workspace.TryApplyChanges(project.Solution))
            {
                Debug.Fail($"Cannot apply changes to solution '{project.Solution.FilePath}'");
                WriteLine($"Cannot apply changes to solution '{project.Solution.FilePath}'", ConsoleColor.Yellow, Verbosity.Diagnostic);
            }
        }

        private async Task FixIdentifiersAsync(
            Project project,
            List<SpellingError> errors,
            CancellationToken cancellationToken)
        {
            var errorsRemoved = false;

            while (errors.Count > 0)
            {
                foreach (SpellingError error in errors)
                {
                    Document document = project.GetDocument(error.Location.SourceTree);

                    if (document != null)
                    {
                        SourceText sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

                        LogHelpers.WriteSpellingError(error, sourceText, Path.GetDirectoryName(project.FilePath), "    ", Verbosity.Normal);

                        if (SpellingData.IgnoreList.Contains(error.Value))
                            continue;

                        string identifierText = error.Identifier.ValueText;

                        string fix = GetFix(error);

                        if (!string.IsNullOrEmpty(fix)
                            && !string.Equals(fix, identifierText, StringComparison.Ordinal))
                        {
                            project = CurrentSolution.GetProject(project.Id);

                            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                            ISymbol symbol = semanticModel.GetDeclaredSymbol(error.Identifier.Parent, cancellationToken);

                            Solution newSolution = await Microsoft.CodeAnalysis.Rename.Renamer.RenameSymbolAsync(
                                CurrentSolution,
                                symbol,
                                fix,
                                default(Microsoft.CodeAnalysis.Options.OptionSet),
                                cancellationToken)
                                .ConfigureAwait(false);

                            if (!Workspace.TryApplyChanges(newSolution))
                            {
                                Debug.Fail($"Cannot apply changes to solution '{newSolution.FilePath}'");
                                WriteLine($"Cannot apply changes to solution '{newSolution.FilePath}'", ConsoleColor.Yellow, Verbosity.Diagnostic);
                                return;
                            }

                            ProcessFix(error, fix);
                            return;
                        }
                        else
                        {
                            SpellingData = SpellingData.AddIgnoredValue(error.Value);

                            if (errors.RemoveAll(f => SpellingData.IgnoreList.Comparer.Equals(f.Value, error.Value)) > 0)
                            {
                                errorsRemoved = true;
                                break;
                            }
                        }
                    }

                    if (errorsRemoved)
                        break;
                }
            }
        }

        private string GetFix(SpellingError spellingError)
        {
            string value = spellingError.Value;
            string containingValue = spellingError.ContainingValue;

            if (spellingError.IsContained
                && SpellingData.FixList.TryGetValue(containingValue, out ImmutableHashSet<string> fixes)
                && fixes.Count == 1)
            {
                string singleFix = fixes.First();

                if (string.Equals(containingValue, singleFix, StringComparison.Ordinal))
                {
                    WriteAutoFix(spellingError, singleFix);
                    return singleFix;
                }
            }

            string fix = null;

            TextCasing textCasing = TextUtility.GetTextCasing(value);

            if (SpellingData.FixList.TryGetValue(value, out fixes)
                && fixes.Count == 1)
            {
                fix = fixes.First();

                if (SpellingData.FixList.Items.TryGetKey(value, out string originalValue)
                    && !string.Equals(value, originalValue, StringComparison.Ordinal))
                {
                    if (textCasing == TextCasing.Mixed)
                    {
                        fix = null;
                    }
                    else if (TextUtility.GetTextCasing(fix) != TextCasing.Mixed)
                    {
                        fix = TextUtility.SetTextCasing(fix, textCasing);
                    }
                }

                if (fix != null)
                    WriteAutoFix(spellingError, fix);
            }

            if (fix == null
                && textCasing != TextCasing.Mixed)
            {
                fix = GetAutoFix(
                    spellingError,
                    (fixes?.Count > 1)
                        ? fixes.Select(f => new SpellingFix(f, SpellingFixKind.List)).ToList()
                        : new List<SpellingFix>());
            }

            if (fix != null
                && spellingError.IsContained)
            {
                int endIndex = spellingError.Index + value.Length;

                fix = containingValue.Remove(spellingError.Index)
                    + fix
                    + containingValue.Substring(endIndex, containingValue.Length - endIndex);
            }

            if (fix == null)
                fix = GetUserFix(spellingError);

            return fix;
        }

        private string GetAutoFix(
            SpellingError spellingError,
            List<SpellingFix> fixes)
        {
            Debug.WriteLine($"find fix for '{spellingError.Value}'");

            string value = spellingError.Value;

            if (spellingError.Casing == TextCasing.Lower
                || spellingError.Casing == TextCasing.FirstUpper)
            {
                foreach (int splitIndex in SpellingFixProvider
                    .GetSplitIndex(spellingError, SpellingData))
                {
                    // foofooBar > fooBar
                    if (value.Length - splitIndex >= splitIndex
                        && string.Compare(value, 0, value, splitIndex, splitIndex, StringComparison.Ordinal) == 0)
                    {
                        fixes.Add(new SpellingFix(value.Remove(splitIndex, splitIndex), SpellingFixKind.Split));
                    }

                    // foobar > fooBar
                    // Tvalue > TValue
                    fixes.Add(new SpellingFix(
                        value
                            .Remove(splitIndex, 1)
                            .Insert(splitIndex, char.ToUpperInvariant(value[splitIndex]).ToString()),
                        SpellingFixKind.Split));

                    // foobar > foo bar
                    if (!spellingError.IsSymbol
                        && splitIndex > 1)
                    {
                        fixes.Add(new SpellingFix(value.Insert(splitIndex, " "), SpellingFixKind.Split));
                    }
                }
            }

            using (IEnumerator<string> en = SpellingFixProvider
                .GetFixes(spellingError, SpellingData)
                .GetEnumerator())
            {
                if (en.MoveNext())
                {
                    fixes.Add(new SpellingFix(en.Current, SpellingFixKind.Fuzzy));

                    while (en.MoveNext())
                        fixes.Add(new SpellingFix(en.Current, SpellingFixKind.Fuzzy));
                }
            }

            fixes = fixes
                .Distinct(SpellingFixComparer.Value)
                .Take(9)
                .Select(fix =>
                {
                    if (TextUtility.GetTextCasing(fix.Value) != TextCasing.Mixed)
                        return fix.WithValue(TextUtility.SetTextCasing(fix.Value, spellingError.Casing));

                    return fix;
                })
                .OrderBy(f => f.Value)
                .ToList();

            if (fixes.Count == 1
                && Options.AutoFix)
            {
                SpellingFix fix = fixes[0];

                if (fix.Kind == SpellingFixKind.Swap
                    || fix.Kind == SpellingFixKind.Fuzzy)
                {
                    WriteAutoFix(spellingError, fix.Value);
                    return fix.Value;
                }
            }

            for (int i = 0; i < fixes.Count; i++)
                WriteSuggestion(spellingError, fixes[i], i);

            if (Options.Interactive
                && fixes.Count > 0
                && TryReadSuggestion(out int index)
                && index < fixes.Count)
            {
                return fixes[index].Value;
            }

            return null;
        }

        private string GetUserFix(SpellingError spellingError)
        {
            Console.Write("    Enter fix: ");

            string fix = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(fix))
                return null;

            string value = spellingError.Value;
            string containingValue = spellingError.ContainingValue;

            if (string.Equals(value, containingValue, StringComparison.Ordinal))
                return fix;

            var startsWith = false;

            int length = spellingError.Index;

            if (length > 0
                && fix.Length > length)
            {
                startsWith = string.CompareOrdinal(fix, 0, containingValue, 0, length) == 0;
            }

            var endsWith = false;

            length = containingValue.Length - spellingError.EndIndex;

            if (length > 0
                && fix.Length > length)
            {
                endsWith = string.CompareOrdinal(fix, fix.Length - length, containingValue, spellingError.EndIndex, length) == 0;
            }

            if (!startsWith
                && !endsWith)
            {
                int endIndex = spellingError.Index + value.Length;

                string fix2 = containingValue.Remove(spellingError.Index)
                    + fix
                    + containingValue.Substring(endIndex, containingValue.Length - endIndex);

                WriteSuggestion(spellingError, new SpellingFix(fix, SpellingFixKind.User), 0);

                if (TryReadSuggestion(out int index)
                    && index == 0)
                {
                    fix = fix2;
                }
            }

            return fix;
        }

        private static void WriteAutoFix(SpellingError spellingError, string fix)
        {
            Write("    Replace '", ConsoleColor.Green);
            Write(spellingError.Value, ConsoleColor.Green);
            Write("' with '", ConsoleColor.Green);
            Write(fix, ConsoleColor.Green);
            WriteLine("'", ConsoleColor.Green);
        }

        private void WriteSuggestion(
            SpellingError spellingError,
            SpellingFix fix,
            int index)
        {
            string value = spellingError.Value;
            string containingValue = spellingError.ContainingValue;

            if (index == 0)
            {
                Write("    Replace   '");

                if (spellingError.IsContained)
                {
                    Write(containingValue.Remove(spellingError.Index));
                    Write(value);
                    Write(containingValue.Substring(spellingError.EndIndex, containingValue.Length - spellingError.EndIndex));
                }
                else
                {
                    Write(value, ConsoleColor.Green);
                }

                WriteLine("'");
            }

            Write("    ");

            if (Options.Interactive)
            {
                Write('(');
                Write(index + 1);

                int num = index + 97;

                if (num <= 122)
                    Write((char)num);

                Write(") ");
            }
            else
            {
                Write("     ");
            }

            Write("with '");

            if (spellingError.IsContained)
            {
                Write(containingValue.Remove(spellingError.Index));
                Write(fix.Value, ConsoleColor.Green);
                Write(containingValue.Substring(spellingError.EndIndex, containingValue.Length - spellingError.EndIndex));
            }
            else
            {
                Write(fix.Value, ConsoleColor.Green);
            }

            WriteLine("'");
        }

        private static bool TryReadSuggestion(out int index)
        {
            Console.Write("    Enter number/letter of a suggestion: ");

            string text = Console.ReadLine()?.Trim();

            if (text.Length == 1)
            {
                int num = text[0];

                if (num >= 97
                    && num <= 122)
                {
                    index = num - 97;
                    return true;
                }
            }

            if (int.TryParse(
                text,
                NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.CurrentCulture,
                out index)
                && index > 0)
            {
                index--;
                return true;
            }

            index = -1;
            return false;
        }

        private void ProcessFix(SpellingError spellingError, string fix)
        {
            string fix2 = null;

            string containingValue = spellingError.ContainingValue;

            int index = spellingError.Index;

            if (fix.Length > index
                && string.CompareOrdinal(fix, 0, containingValue, 0, index) == 0)
            {
                int endIndex = spellingError.EndIndex;

                int length = containingValue.Length - endIndex;

                if (fix.Length > index + length
                    && string.CompareOrdinal(fix, fix.Length - length, containingValue, endIndex, length) == 0)
                {
                    fix2 = fix.Substring(index, fix.Length - length - index);
                }
            }

            if (fix2 != null)
            {
                SpellingData = SpellingData.AddFix(spellingError.Value, fix2);
                SpellingData = SpellingData.AddWord(fix2);
            }
            else
            {
                SpellingData = SpellingData.AddFix(spellingError.ContainingValue, fix);
            }
        }
    }
}
