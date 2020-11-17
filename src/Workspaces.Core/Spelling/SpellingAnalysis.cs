// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Roslynator.Host.Mef;

namespace Roslynator.Spelling
{
    internal static class SpellingAnalysis
    {
        public static async Task<SpellingAnalysisResult> AnalyzeSpellingAsync(
            Project project,
            SpellingData spellingData,
            SpellingAnalysisOptions options = null,
            CancellationToken cancellationToken = default)
        {
            ISpellingService service = MefWorkspaceServices.Default.GetService<ISpellingService>(project.Language);

            if (service == null)
                return default;

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
            this ISpellingService service,
            Document document,
            SpellingData spellingData,
            SpellingAnalysisOptions options = null,
            CancellationToken cancellationToken = default)
        {
            SyntaxTree tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);

            if (tree == null)
                return default;

            if (!options.IncludeGeneratedCode
                && GeneratedCodeUtility.IsGeneratedCode(tree, f => service.SyntaxFacts.IsComment(f), cancellationToken))
            {
                return default;
            }

            SyntaxNode root = await tree.GetRootAsync(cancellationToken).ConfigureAwait(false);

            return service.AnalyzeSpelling(root, spellingData, options, cancellationToken);
        }
    }
}
