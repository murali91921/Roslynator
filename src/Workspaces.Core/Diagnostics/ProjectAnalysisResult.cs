// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics.Telemetry;
using Roslynator.Spelling;

namespace Roslynator.Diagnostics
{
    internal class ProjectAnalysisResult
    {
        internal ProjectAnalysisResult(
            ProjectId projectId,
            ImmutableArray<DiagnosticAnalyzer> analyzers,
            ImmutableArray<Diagnostic> compilerDiagnostics,
            ImmutableArray<Diagnostic> diagnostics,
            ImmutableArray<SpellingError> spellingErrors,
            ImmutableDictionary<DiagnosticAnalyzer, AnalyzerTelemetryInfo> telemetry)
        {
            ProjectId = projectId;
            Analyzers = analyzers;
            CompilerDiagnostics = compilerDiagnostics;
            Diagnostics = diagnostics;
            Telemetry = telemetry;
            SpellingErrors = spellingErrors;
        }

        public ProjectId ProjectId { get; }

        public ImmutableArray<DiagnosticAnalyzer> Analyzers { get; }

        public ImmutableArray<Diagnostic> CompilerDiagnostics { get; }

        public ImmutableArray<Diagnostic> Diagnostics { get; }

        public ImmutableArray<SpellingError> SpellingErrors { get; }

        public ImmutableDictionary<DiagnosticAnalyzer, AnalyzerTelemetryInfo> Telemetry { get; }

        public IEnumerable<Diagnostic> GetAllDiagnostics()
        {
            return CompilerDiagnostics.Concat(Diagnostics);
        }

        public ProjectAnalysisResult WithSpellingErrors(ImmutableArray<SpellingError> spellingErrors)
        {
            return new ProjectAnalysisResult(
                ProjectId,
                Analyzers,
                CompilerDiagnostics,
                Diagnostics,
                spellingErrors,
                Telemetry);
        }
    }
}
