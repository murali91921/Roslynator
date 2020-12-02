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

            var results = new List<ImmutableArray<SpellingFixResult>>();

            Stopwatch stopwatch = Stopwatch.StartNew();

            TimeSpan lastElapsed = TimeSpan.Zero;

            for (int i = 0; i < projects.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Project project = CurrentSolution.GetProject(projects[i]);

                if (predicate == null || predicate(project))
                {
                    WriteLine($"Fix '{project.Name}' {$"{i + 1}/{projects.Length}"}", ConsoleColor.Cyan, Verbosity.Minimal);

                    ImmutableArray<SpellingFixResult> results2 = await FixProjectAsync(project, cancellationToken).ConfigureAwait(false);

                    results.Add(results2);
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

            WriteLine(Verbosity.Normal);

            foreach (IGrouping<SpellingFixResult, SpellingFixResult> grouping in results
                .SelectMany(f => f)
                .OrderBy(f => f.OldValue)
                .ThenBy(f => f.NewValue)
                .GroupBy(f => f, SpellingFixResultEqualityComparer.OldValueAndNewValue))
            {
                WriteLine($"{grouping.Key.OldValue} = {grouping.Key.NewValue}", Verbosity.Normal);

                foreach (SpellingFixResult result in grouping)
                {
                    if (result.IsSymbol)
                        WriteLine($"  {result.OldIdentifier}  {result.NewIdentifier}", Verbosity.Normal);
                }
            }
        }

        public async Task<ImmutableArray<SpellingFixResult>> FixProjectAsync(
            Project project,
            CancellationToken cancellationToken = default)
        {
            ImmutableArray<SpellingFixResult>.Builder results = ImmutableArray.CreateBuilder<SpellingFixResult>();

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
                    break;

                List<SpellingError> errors = spellingAnalysisResult.Errors.ToList();

                if (!commentsFixed)
                {
                    List<SpellingFixResult> commentResults = await FixCommentsAsync(project, errors, cancellationToken).ConfigureAwait(false);
                    results.AddRange(commentResults);
                    commentsFixed = true;
                }

                List<SpellingFixResult> symbolResults = await FixSymbolsAsync(project, errors, cancellationToken).ConfigureAwait(false);
                results.AddRange(symbolResults);

                if (Options.DryRun)
                    break;

                project = CurrentSolution.GetProject(project.Id);
            }

            return results.ToImmutableArray();
        }

        private async Task<List<SpellingFixResult>> FixCommentsAsync(
            Project project,
            List<SpellingError> errors,
            CancellationToken cancellationToken)
        {
            var results = new List<SpellingFixResult>();

            List<SpellingError> commentErrors = errors.Where(f => !f.IsSymbol).ToList();

            var applyChanges = false;

            project = CurrentSolution.GetProject(project.Id);

            foreach (IGrouping<SyntaxTree, SpellingError> grouping in commentErrors
                .GroupBy(f => f.Location.SourceTree))
            {
                cancellationToken.ThrowIfCancellationRequested();

                Document document = project.GetDocument(grouping.Key);

                SourceText sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

                List<TextChange> textChanges = null;

                foreach (SpellingError error in grouping.OrderBy(f => f.Location.SourceSpan.Start))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (SpellingData.IgnoreList.Contains(error.Value))
                        continue;

                    LogHelpers.WriteSpellingError(error, Options, sourceText, Path.GetDirectoryName(project.FilePath), "    ", Verbosity.Normal);

                    SpellingFix fix = GetFix(error, cancellationToken);

                    Debug.Assert(fix.Value != "");

                    if (!fix.IsDefault
                        && !string.Equals(fix.Value, error.Value, StringComparison.Ordinal))
                    {
                        if (!Options.DryRun)
                            (textChanges ??= new List<TextChange>()).Add(new TextChange(error.Location.SourceSpan, fix.Value));

                        results.Add(new SpellingFixResult(
                            error.Value,
                            fix.Value,
                            error.Location.GetMappedLineSpan()));

                        ProcessFix(error, fix);
                    }
                    else
                    {
                        SpellingData = SpellingData.AddIgnoredValue(error.Value);
                    }
                }

                if (textChanges != null)
                {
                    document = await document.WithTextChangesAsync(textChanges, cancellationToken).ConfigureAwait(false);
                    project = document.Project;

                    applyChanges = true;
                }
            }

            if (applyChanges
                && !Workspace.TryApplyChanges(project.Solution))
            {
                Debug.Fail($"Cannot apply changes to solution '{project.Solution.FilePath}'");
                WriteLine($"    Cannot apply changes to solution '{project.Solution.FilePath}'", ConsoleColor.Yellow, Verbosity.Diagnostic);
            }

            return results;
        }

        private async Task<List<SpellingFixResult>> FixSymbolsAsync(
            Project project,
            List<SpellingError> spellingErrors,
            CancellationToken cancellationToken)
        {
            var results = new List<SpellingFixResult>();

            List<(SyntaxToken identifier, List<SpellingError> errors, DocumentId documentId)> symbolErrors = spellingErrors
                .Where(f => f.IsSymbol)
                .GroupBy(f => f.Identifier)
                .Select(f => (
                    identifier: f.Key,
                    errors: f.OrderBy(f => f.Location.SourceSpan.Start).ToList(),
                    documentId: project.GetDocument(f.Key.SyntaxTree).Id))
                .OrderBy(f => f.documentId.Id)
                .ThenByDescending(f => f.identifier.SpanStart)
                .ToList();

            for (int i = 0; i < symbolErrors.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                (SyntaxToken identifier, List<SpellingError> errors, DocumentId documentId) = symbolErrors[i];

                SyntaxNode node = null;

                foreach (SpellingError error in errors)
                {
                    if (error != null)
                    {
                        node = error.Node;
                        break;
                    }
                }

                if (node == null)
                    continue;

                Document document = project.GetDocument(documentId);

                Debug.Assert(document != null, identifier.GetLocation().ToString());

                if (document == null)
                {
                    WriteLine($"    Cannot find document for'{identifier.ValueText}'", ConsoleColor.Yellow, Verbosity.Detailed);
                    SpellingData = SpellingData.AddIgnoredValues(errors);
                    continue;
                }

                SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

                if (identifier.SyntaxTree != root.SyntaxTree)
                {
                    SyntaxToken identifier2 = root.FindToken(identifier.SpanStart, findInsideTrivia: false);

                    if (identifier.Span != identifier2.Span
                        || identifier.RawKind != identifier2.RawKind
                        || !string.Equals(identifier2.ValueText, identifier2.ValueText, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SyntaxNode node2 = identifier2.Parent;

                    SyntaxNode n = identifier.Parent;
                    while (n != node)
                    {
                        node2 = node2.Parent;
                        n = n.Parent;
                    }

                    identifier = identifier2;
                    node = node2;
                }

                SourceText sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

                SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                ISymbol symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken)
                    ?? semanticModel.GetSymbol(node, cancellationToken);

                Debug.Assert(symbol != null, identifier.ToString());

                if (symbol == null)
                {
                    WriteLine($"    Cannot find symbol for '{identifier.ValueText}'", ConsoleColor.Yellow, Verbosity.Detailed);
                    SpellingData = SpellingData.AddIgnoredValues(errors);
                    continue;
                }

                if (!symbol.IsKind(SymbolKind.Namespace, SymbolKind.Alias)
                    && !symbol.IsVisible(Options.SymbolVisibility))
                {
                    continue;
                }

                var fixes = new List<(SpellingError error, SpellingFix fix)>();
                string newName = identifier.ValueText;
                int indexOffset = 0;

                for (int j = 0; j < errors.Count; j++)
                {
                    SpellingError error = errors[j];

                    if (error == null)
                        continue;

                    LogHelpers.WriteSpellingError(error, Options, sourceText, Path.GetDirectoryName(project.FilePath), "    ", Verbosity.Normal);

                    SpellingFix fix = GetFix(error, cancellationToken);
                    fixes.Add((error, fix));

                    if (!fix.IsDefault)
                    {
                        newName = TextUtility.ReplaceRange(newName, fix.Value, error.Index + indexOffset, error.Length);

                        indexOffset += fix.Value.Length - error.Length;
                    }
                    else
                    {
                        SpellingData = SpellingData.AddIgnoredValue(error.Value);

                        for (int k = 0; k < symbolErrors.Count; k++)
                        {
                            List<SpellingError> errors2 = symbolErrors[k].errors;

                            for (int l = 0; l < errors2.Count; l++)
                            {
                                if (SpellingData.IgnoreList.Comparer.Equals(errors2[l]?.Value, error.Value))
                                    errors2[l] = null;
                            }
                        }
                    }
                }

                if (string.Equals(identifier.Text, newName, StringComparison.Ordinal))
                    continue;

                Solution newSolution = null;
                if (!Options.DryRun)
                {
                    WriteLine($"    Rename '{identifier.ValueText}' to '{newName}'", Verbosity.Minimal);

                    try
                    {
                        //TODO: check name conflict
                        newSolution = await Microsoft.CodeAnalysis.Rename.Renamer.RenameSymbolAsync(
                            CurrentSolution,
                            symbol,
                            newName,
                            default(Microsoft.CodeAnalysis.Options.OptionSet),
                            cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (InvalidOperationException ex)
                    {
                        WriteLine($"    Cannot rename '{symbol.Name}'", ConsoleColor.Yellow, Verbosity.Normal);
#if DEBUG
                        WriteLine(document.FilePath);
                        WriteLine(identifier.ValueText);
                        WriteLine(ex.ToString());
#endif
                        SpellingData = SpellingData.AddIgnoredValues(errors);
                        continue;
                    }
                }

                if (newSolution != null)
                {
                    if (Workspace.TryApplyChanges(newSolution))
                    {
                        project = CurrentSolution.GetProject(project.Id);
                    }
                    else
                    {
                        Debug.Fail($"Cannot apply changes to solution '{newSolution.FilePath}'");
                        WriteLine($"    Cannot apply changes to solution '{newSolution.FilePath}'", ConsoleColor.Yellow, Verbosity.Diagnostic);

                        SpellingData = SpellingData.AddIgnoredValues(errors);
                        continue;
                    }
                }

                foreach ((SpellingError error, SpellingFix fix) in fixes)
                {
                    if (fix.IsDefault)
                        continue;

                    results.Add(new SpellingFixResult(
                        error.Value,
                        fix.Value,
                        error.Identifier.ValueText,
                        newName,
                        error.Index,
                        error.Location.GetMappedLineSpan()));

                    ProcessFix(error, fix);
                }
            }

            return results;
        }

        private SpellingFix GetFix(SpellingError spellingError, CancellationToken cancellationToken)
        {
            string value = spellingError.Value;
            string containingValue = spellingError.ContainingValue;

            if (Options.AutoFix
                && spellingError.IsContained
                && SpellingData.FixList.TryGetValue(containingValue, out ImmutableHashSet<SpellingFix> fixes2))
            {
                SpellingFix fix2 = fixes2.SingleOrDefault(
                    f => f.Kind == SpellingFixKind.List && spellingError.IsApplicableFix(f.Value),
                    shouldThrow: false);

                if (string.Equals(containingValue, fix2.Value, StringComparison.Ordinal))
                {
                    WriteFix(spellingError, fix2);
                    return fix2;
                }
            }

            SpellingFix fix = default;
            var fixes = new List<SpellingFix>();

            TextCasing textCasing = TextUtility.GetTextCasing(value);

            if (SpellingData.FixList.TryGetValue(value, out fixes2))
            {
                if (Options.AutoFix
                    && textCasing != TextCasing.Mixed)
                {
                    SpellingFix fix2 = fixes2.SingleOrDefault(
                        f => spellingError.IsApplicableFix(f.Value),
                        shouldThrow: false);

                    if (!fix2.IsDefault)
                    {
                        if (fix2.Kind == SpellingFixKind.List
                            || (SpellingData.FixList.TryGetKey(value, out string originalValue)
                                && string.Equals(value, originalValue, StringComparison.Ordinal)))
                        {
                            if (TextUtility.GetTextCasing(fix2.Value) != TextCasing.Mixed)
                                fix = fix2.WithValue(TextUtility.SetTextCasing(fix2.Value, textCasing));
                        }
                    }
                }

                if (fix.IsDefault)
                {
                    fixes = fixes2
                        .Where(f => TextUtility.GetTextCasing(f.Value) != TextCasing.Mixed
                            && spellingError.IsApplicableFix(f.Value))
                        .Select(f => f.WithValue(TextUtility.SetTextCasing(f.Value, textCasing)))
                        .ToList();
                }
            }

            if (fix.IsDefault
                && Options.Interactive)
            {
                if (textCasing != TextCasing.Mixed)
                {
                    if (fixes.Count == 0)
                        AddPossibleFixes(spellingError, ref fixes, cancellationToken);

                    fix = ChooseFix(spellingError, fixes);
                }

                if (fix.IsDefault)
                    fix = GetUserFix();
            }

            if (!fix.IsDefault)
                WriteFix(spellingError, fix);

            return fix;
        }

        private SpellingFix ChooseFix(
            SpellingError spellingError,
            List<SpellingFix> fixes)
        {
            //TODO: set max number of suggestions
            fixes = fixes
                .Distinct(SpellingFixComparer.Default)
                .Where(f =>
                {
                    return f.Kind == SpellingFixKind.List
                        || spellingError.IsApplicableFix(f.Value);
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
                for (int i = 0; i < fixes.Count; i++)
                    WriteSuggestion(spellingError, fixes[i], i);

                if (TryReadSuggestion(out int index)
                    && index < fixes.Count)
                {
                    return fixes[index];
                }
            }

            return default;
        }

        private void AddPossibleFixes(
            SpellingError spellingError,
            ref List<SpellingFix> fixes,
            CancellationToken cancellationToken)
        {
            Debug.WriteLine($"find possible fix for '{spellingError.Value}'");

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

                    if (spellingError.IsSymbol)
                    {
                        // foobar > foo_bar
                        if (spellingError.ContainingValue.Contains("_"))
                            fixes.Add(new SpellingFix(value.Insert(splitIndex, "_"), SpellingFixKind.Split));
                    }
                    else if (splitIndex > 1)
                    {
                        // foobar > foo bar
                        fixes.Add(new SpellingFix(value.Insert(splitIndex, " "), SpellingFixKind.Split));
                    }
                }
            }

            ImmutableArray<string> matches = SpellingFixProvider.SwapMatches(
                spellingError.ValueLower,
                SpellingData);

            foreach (string match in matches)
                fixes.Add(new SpellingFix(match, SpellingFixKind.Swap));

            if (matches.Length == 0)
            {
                matches = SpellingFixProvider.FuzzyMatches(
                    spellingError.ValueLower,
                    SpellingData,
                    cancellationToken);

                foreach (string match in matches)
                    fixes.Add(new SpellingFix(match, SpellingFixKind.Fuzzy));
            }
        }

        private SpellingFix GetUserFix()
        {
            Write("    Enter fix: ");

            string fix = Console.ReadLine()?.Trim();

            return (!string.IsNullOrEmpty(fix))
                ? new SpellingFix(fix, SpellingFixKind.User)
                : default;
        }

        private static void WriteFix(SpellingError spellingError, SpellingFix fix)
        {
            WriteLine($"    Replace '{spellingError.Value}' with '{fix.Value}'", ConsoleColor.Green, Verbosity.Minimal);
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

        private void ProcessFix(SpellingError spellingError, SpellingFix spellingFix)
        {
            SpellingData = SpellingData.AddFix(spellingError.Value, spellingFix);
            SpellingData = SpellingData.AddWord(spellingFix.Value);
        }
    }
}
