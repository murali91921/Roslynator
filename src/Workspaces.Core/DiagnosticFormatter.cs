﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Roslynator.Spelling;
using Roslynator.Text;

namespace Roslynator
{
    internal static class DiagnosticFormatter
    {
        public static string FormatDiagnostic(
            Diagnostic diagnostic,
            string baseDirectoryPath = null,
            IFormatProvider formatProvider = null)
        {
            StringBuilder sb = StringBuilderCache.GetInstance();

            switch (diagnostic.Location.Kind)
            {
                case LocationKind.SourceFile:
                case LocationKind.XmlFile:
                case LocationKind.ExternalFile:
                    {
                        FileLinePositionSpan span = diagnostic.Location.GetMappedLineSpan();

                        if (span.IsValid)
                        {
                            sb.Append(PathUtilities.TrimStart(span.Path, baseDirectoryPath));

                            LinePosition linePosition = span.Span.Start;

                            sb.Append('(');
                            sb.Append(linePosition.Line + 1);
                            sb.Append(',');
                            sb.Append(linePosition.Character + 1);
                            sb.Append("): ");
                        }

                        break;
                    }
            }

            string severity = GetSeverityText(diagnostic.Severity);

            sb.Append(severity);
            sb.Append(' ');
            sb.Append(diagnostic.Id);
            sb.Append(": ");

            string message = diagnostic.GetMessage(formatProvider);

            sb.Append(message);

            return StringBuilderCache.GetStringAndFree(sb);
        }

        private static string GetSeverityText(DiagnosticSeverity diagnosticSeverity)
        {
            switch (diagnosticSeverity)
            {
                case DiagnosticSeverity.Hidden:
                    return "hidden";
                case DiagnosticSeverity.Info:
                    return "info";
                case DiagnosticSeverity.Warning:
                    return "warning";
                case DiagnosticSeverity.Error:
                    return "error";
                default:
                    throw new InvalidOperationException();
            }
        }

        public static string FormatSpellingError(
            SpellingError spellingError,
            string baseDirectoryPath = null)
        {
            StringBuilder sb = StringBuilderCache.GetInstance();

            AppendLocation(spellingError.Location, baseDirectoryPath, sb);

            sb.Append("Fix spelling of '");

            sb.Append(spellingError.Value);

            sb.Append("'");

            //if (spellingError.IsSymbol
            //    && !string.Equals(spellingError.Value, spellingError.Identifier.ValueText, StringComparison.Ordinal))
            //{
            //    sb.Append(" in '");
            //    sb.Append(spellingError.Identifier.ValueText);
            //    sb.Append("'");
            //}

            return StringBuilderCache.GetStringAndFree(sb);
        }

        private static void AppendLocation(Location location, string baseDirectoryPath, StringBuilder sb)
        {
            switch (location.Kind)
            {
                case LocationKind.SourceFile:
                case LocationKind.XmlFile:
                case LocationKind.ExternalFile:
                    {
                        FileLinePositionSpan span = location.GetMappedLineSpan();

                        if (span.IsValid)
                        {
                            sb.Append(PathUtilities.TrimStart(span.Path, baseDirectoryPath));

                            LinePosition linePosition = span.Span.Start;

                            sb.Append('(');
                            sb.Append(linePosition.Line + 1);
                            sb.Append(',');
                            sb.Append(linePosition.Character + 1);
                            sb.Append("): ");
                        }

                        break;
                    }
            }
        }
    }
}
