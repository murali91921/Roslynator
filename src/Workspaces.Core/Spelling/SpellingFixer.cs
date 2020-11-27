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

            var commentsFixed = false;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SpellingAnalysisResult spellingAnalysisResult = await SpellingAnalyzer.AnalyzeSpellingAsync(
                    project,
                    SpellingData,
                    new SpellingFixerOptions(
                        includeComments: !commentsFixed,
                        includeLocal: false),
                    cancellationToken)
                    .ConfigureAwait(false);

                if (!spellingAnalysisResult.Errors.Any())
                    return default;

                List<SpellingError> errors = spellingAnalysisResult.Errors.ToList();

                if (!commentsFixed)
                {
                    await FixCommentsAsync(project, errors, cancellationToken).ConfigureAwait(false);
                    commentsFixed = true;
                }

                await FixSymbolsAsync(project, errors, cancellationToken).ConfigureAwait(false);

                project = CurrentSolution.GetProject(project.Id);
            }
        }

        private async Task FixCommentsAsync(
            Project project,
            List<SpellingError> errors,
            CancellationToken cancellationToken)
        {
            List<SpellingError> commentErrors = errors.Where(f => !f.IsSymbol).ToList();

            var applyChanges = false;
            var errorsRemoved = false;

            project = CurrentSolution.GetProject(project.Id);

            Document document = null;

            while (commentErrors.Count > 0)
            {
                foreach (IGrouping<SyntaxTree, SpellingError> grouping in commentErrors
                    .GroupBy(f => f.Location.SourceTree))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    document = project.GetDocument(grouping.Key);

                    SourceText sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

                    TextLineCollection lines = sourceText.Lines;

                    List<TextChange> textChanges = null;

                    foreach (SpellingError error in grouping.OrderBy(f => f.Location.SourceSpan.Start))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        LogHelpers.WriteSpellingError(error, Options, sourceText, Path.GetDirectoryName(project.FilePath), "    ", Verbosity.Normal);

                        string fix = GetFix(error, cancellationToken);

                        Debug.Assert(fix != "");

                        if (fix != null
                            && !string.Equals(fix, error.Value, StringComparison.Ordinal))
                        {
                            (textChanges ??= new List<TextChange>()).Add(new TextChange(error.Location.SourceSpan, fix));

                            ProcessFix(error, fix);

                            commentErrors.Remove(error);
                        }
                        else
                        {
                            SpellingData = SpellingData.AddIgnoredValue(error.Value);

                            if (commentErrors.RemoveAll(f => SpellingData.IgnoreList.Comparer.Equals(f.Value, error.Value)) > 0)
                            {
                                errors.RemoveAll(f => SpellingData.IgnoreList.Comparer.Equals(f.Value, error.Value));
                                errorsRemoved = true;
                                break;
                            }
                        }
                    }

                    if (textChanges != null
                        && (!errorsRemoved || !commentErrors.Any(f => f.Location.SourceTree == grouping.Key)))
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

        private async Task FixSymbolsAsync(
            Project project,
            List<SpellingError> errors,
            CancellationToken cancellationToken)
        {
            List<(SpellingError error, DocumentId documentId)> symbolErrors = errors
                .Where(f => f.IsSymbol)
                .Select(f => (error: f, documentId: project.GetDocument(f.Location.SourceTree).Id))
                .OrderBy(f => f.documentId.Id)
                .ThenByDescending(f => f.error.Location.SourceSpan.Start)
                .ToList();

            for (int i = 0; i < symbolErrors.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                (SpellingError error, DocumentId documentId) = symbolErrors[i];

                if (error == null)
                    continue;

                Document document = project.GetDocument(documentId);

                Debug.Assert(document != null, error.Location.ToString());

                if (document == null)
                {
                    WriteLine($"Cannot find document for'{error.Identifier.ValueText}'", ConsoleColor.Yellow, Verbosity.Detailed);
                    SpellingData = SpellingData.AddIgnoredValue(error.Value);
                    continue;
                }

                SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

                SyntaxToken identifier = error.Identifier;

                SyntaxToken identifier2 = root.FindToken(error.Location.SourceSpan.Start, findInsideTrivia: false);

                if (identifier.Span != identifier2.Span
                    || identifier.RawKind != identifier2.RawKind
                    || !string.Equals(identifier2.ValueText, identifier2.ValueText, StringComparison.Ordinal))
                {
                    continue;
                }

                SourceText sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

                LogHelpers.WriteSpellingError(error, Options, sourceText, Path.GetDirectoryName(project.FilePath), "    ", Verbosity.Normal);

                SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                ISymbol symbol = semanticModel.GetDeclaredSymbol(identifier2.Parent, cancellationToken)
                    ?? semanticModel.GetSymbol(identifier2.Parent, cancellationToken);

                Debug.Assert(symbol != null, identifier.ToString());

                if (symbol == null)
                {
                    WriteLine($"Cannot find symbol for '{identifier.ValueText}'", ConsoleColor.Yellow, Verbosity.Detailed);
                    SpellingData = SpellingData.AddIgnoredValue(error.Value);
                    continue;
                }

                string fix = GetFix(error, cancellationToken);

                if (fix == null
                    || string.Equals(fix, identifier.ValueText, StringComparison.Ordinal))
                {
                    SpellingData = SpellingData.AddIgnoredValue(error.Value);

                    for (int j = 0; j < symbolErrors.Count; j++)
                    {
                        if (symbolErrors[j].error != null
                            && SpellingData.IgnoreList.Comparer.Equals(symbolErrors[j].error.Value, error.Value))
                        {
                            symbolErrors[j] = default;
                        }
                    }

                    continue;
                }

                WriteLine($"    Rename '{identifier.ValueText}' to '{fix}'", Verbosity.Minimal);

                Solution newSolution = null;
                try
                {
                    newSolution = await Microsoft.CodeAnalysis.Rename.Renamer.RenameSymbolAsync(
                        CurrentSolution,
                        symbol,
                        fix,
                        default(Microsoft.CodeAnalysis.Options.OptionSet),
                        cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    WriteLine($"Cannot rename '{symbol.Name}'", ConsoleColor.Yellow, Verbosity.Normal);
#if DEBUG
                    WriteLine(document.FilePath);
                    WriteLine(identifier.Text);
                    WriteLine(ex.ToString());
#endif
                    SpellingData = SpellingData.AddIgnoredValue(error.Value);
                    continue;
                }

                if (!Workspace.TryApplyChanges(newSolution))
                {
                    Debug.Fail($"Cannot apply changes to solution '{newSolution.FilePath}'");
                    WriteLine($"Cannot apply changes to solution '{newSolution.FilePath}'", ConsoleColor.Yellow, Verbosity.Diagnostic);

                    SpellingData = SpellingData.AddIgnoredValue(error.Value);
                    continue;
                }

                project = CurrentSolution.GetProject(project.Id);

                ProcessFix(error, fix);
            }
        }

        private string GetFix(SpellingError spellingError, CancellationToken cancellationToken)
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
                    WriteFix(spellingError, singleFix);
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
            }

            if (fix == null
                && textCasing != TextCasing.Mixed)
            {
                fix = GetAutoFix(
                    spellingError,
                    (fixes?.Count > 1)
                        ? fixes.Select(f => new SpellingFix(f, SpellingFixKind.List)).ToList()
                        : new List<SpellingFix>(),
                    cancellationToken);
            }

            if (fix == null
                && Options.Interactive)
            {
                fix = GetUserFix();
            }

            if (fix != null)
            {
                WriteFix(spellingError, fix);

                if (spellingError.IsContained
                    && spellingError.IsSymbol)
                {
                    int endIndex = spellingError.Index + value.Length;

                    fix = containingValue.Remove(spellingError.Index)
                        + fix
                        + containingValue.Substring(endIndex, containingValue.Length - endIndex);
                }
            }

            return fix;
        }

        private string GetAutoFix(
            SpellingError spellingError,
            List<SpellingFix> fixes,
            CancellationToken cancellationToken)
        {
            Debug.WriteLine($"find fix for '{spellingError.Value}'");

            string value = spellingError.Value;

            if (spellingError.Casing == TextCasing.Lower
                || spellingError.Casing == TextCasing.FirstUpper)
            {
                foreach (int splitIndex in SpellingFixProvider.GetSplitIndexes(spellingError, SpellingData))
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

            if (spellingError.Length >= 4)
            {
                foreach (string match in SpellingFixProvider.SwapMatches(
                    spellingError.ValueLower,
                    SpellingData,
                    cancellationToken))
                {
                    fixes.Add(new SpellingFix(match, SpellingFixKind.Swap));
                }

                foreach (string match in SpellingFixProvider.FuzzyMatches(
                    spellingError.ValueLower,
                    SpellingData,
                    cancellationToken))
                {
                    fixes.Add(new SpellingFix(match, SpellingFixKind.Fuzzy));
                }
            }

            fixes = fixes
                .Distinct(SpellingFixComparer.Value)
                .Where(f =>
                {
                    //TODO: is valid identifier
                    return !spellingError.IsSymbol
                        || (!f.Value.Contains('\'') && !f.Value.Contains('-'));
                })
                .Select(fix =>
                {
                    if (TextUtility.GetTextCasing(fix.Value) != TextCasing.Mixed)
                        return fix.WithValue(TextUtility.SetTextCasing(fix.Value, spellingError.Casing));

                    return fix;
                })
                .OrderBy(f => f.Kind)
                .Take(9)
                .ToList();

            if (fixes.Count > 0)
            {
                if (fixes.Count == 1
                    //&& !spellingError.IsSymbol
                    && Options.AutoFix)
                {
                    SpellingFix fix = fixes[0];

                    if (fix.Kind == SpellingFixKind.Swap
                        || fix.Kind == SpellingFixKind.Fuzzy)
                    {
                        return fix.Value;
                    }
                }

                if (Options.Interactive)
                {
                    for (int i = 0; i < fixes.Count; i++)
                        WriteSuggestion(spellingError, fixes[i], i);

                    if (TryReadSuggestion(out int index)
                        && index < fixes.Count)
                    {
                        return fixes[index].Value;
                    }
                }
            }

            return null;
        }

        private string GetUserFix()
        {
            Write("    Enter fix: ");

            string fix = Console.ReadLine()?.Trim();

            return (!string.IsNullOrEmpty(fix)) ? fix : null;
        }

        private static void WriteFix(SpellingError spellingError, string fix)
        {
            WriteLine($"    Replace '{spellingError.Value}' with '{fix}'", ConsoleColor.Green, Verbosity.Minimal);
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
                Write("    Replace  '");

                if (spellingError.IsContained)
                {
                    Write(containingValue.Remove(spellingError.Index));
                    Write(value);
                    Write(containingValue.Substring(spellingError.EndIndex, containingValue.Length - spellingError.EndIndex));
                }
                else
                {
                    Write(value, ConsoleColor.Cyan);
                }

                WriteLine("'");
            }

            Write("    ");

            if (Options.Interactive)
            {
                Write($"({index + 1}) ");
            }
            else
            {
                Write("   ");
            }

            Write("with '");

            if (spellingError.IsContained)
            {
                Write(containingValue.Remove(spellingError.Index));
                Write(fix.Value, ConsoleColor.Cyan);
                Write(containingValue.Substring(spellingError.EndIndex, containingValue.Length - spellingError.EndIndex));
            }
            else
            {
                Write(fix.Value, ConsoleColor.Cyan);
            }

            Write("'");

            if (Options.Interactive)
                Write($" ({index + 1})");

            WriteLine();
        }

        private static bool TryReadSuggestion(out int index)
        {
            Write("    Enter number of a suggestion: ");

            string text = Console.ReadLine()?.Trim();

            if (text?.Length == 1)
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
