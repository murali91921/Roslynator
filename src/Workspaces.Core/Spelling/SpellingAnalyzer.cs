// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.Host.Mef;

#pragma warning disable RS1001

namespace Roslynator.Spelling
{
    internal sealed class SpellingAnalyzer : DiagnosticAnalyzer
    {
        private readonly ISpellingService _spellingService;
        private readonly SpellingData _spellingData;
        private readonly SpellingFixerOptions _spellingFixerOptions;
        private readonly GeneratedCodeAnalysisFlags _generatedCodeAnalysisFlags;

        public const string Id = "RCS8000";

        public static readonly DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(
            id: Id,
            title: "Fix possible spelling error.",
            messageFormat: "Fix possible spelling error.",
            category: "Spelling",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: null,
            helpLinkUri: null,
            customTags: Array.Empty<string>());

        private SpellingAnalyzer(
            ISpellingService spellingService,
            SpellingData spellingData,
            SpellingFixerOptions spellingFixerOptions,
            GeneratedCodeAnalysisFlags generatedCodeAnalysisFlags)
        {
            _spellingService = spellingService;
            _spellingData = spellingData;
            _spellingFixerOptions = spellingFixerOptions;
            _generatedCodeAnalysisFlags = generatedCodeAnalysisFlags;
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(Descriptor); }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(_generatedCodeAnalysisFlags);

            context.RegisterSyntaxTreeAction(f => AnalyzeSyntaxTree(f));
        }

        private void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
        {
            SyntaxTree tree = context.Tree;

            SyntaxNode root = tree.GetRoot(context.CancellationToken);

            SpellingAnalysisResult result = _spellingService.AnalyzeSpelling(
                root,
                _spellingData,
                _spellingFixerOptions,
                context.CancellationToken);

            foreach (SpellingError error in result.Errors)
                context.ReportDiagnostic(Descriptor, error.Location);
        }

        public static SpellingAnalyzer Create(
            ISpellingService service,
            SpellingData spellingData,
            SpellingFixerOptions spellingFixerOptions = null,
            GeneratedCodeAnalysisFlags generatedCodeAnalysisFlags = GeneratedCodeAnalysisFlags.None)
        {
            return new SpellingAnalyzer(
                service,
                spellingData,
                spellingFixerOptions,
                generatedCodeAnalysisFlags);
        }

        public static async Task<SpellingAnalysisResult> AnalyzeSpellingAsync(
            Project project,
            SpellingData spellingData,
            SpellingFixerOptions options = null,
            CancellationToken cancellationToken = default)
        {
            ISpellingService service = MefWorkspaceServices.Default.GetService<ISpellingService>(project.Language);

            if (service == null)
                return SpellingAnalysisResult.Empty;

            SpellingAnalysisResult result = SpellingAnalysisResult.Empty;

            foreach (Document document in project.Documents)
            {
                if (!document.SupportsSyntaxTree)
                    continue;

                SpellingAnalysisResult result2 = await AnalyzeSpellingAsync(service, document, spellingData, options, cancellationToken).ConfigureAwait(false);

                result = result.Add(result2);
            }

            return result;
        }

        public static async Task<SpellingAnalysisResult> AnalyzeSpellingAsync(
            ISpellingService service,
            Document document,
            SpellingData spellingData,
            SpellingFixerOptions options = null,
            CancellationToken cancellationToken = default)
        {
            SyntaxTree tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);

            if (tree == null)
                return SpellingAnalysisResult.Empty;

            if (!options.IncludeGeneratedCode
                && GeneratedCodeUtility.IsGeneratedCode(tree, f => service.SyntaxFacts.IsComment(f), cancellationToken))
            {
                return SpellingAnalysisResult.Empty;
            }

            SyntaxNode root = await tree.GetRootAsync(cancellationToken).ConfigureAwait(false);

            return service.AnalyzeSpelling(root, spellingData, options, cancellationToken);
        }
    }
}
