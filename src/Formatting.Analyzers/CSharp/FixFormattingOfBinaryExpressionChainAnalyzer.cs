// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;
using Roslynator.CSharp.Syntax;
using static Roslynator.CSharp.SyntaxTriviaAnalysis;

namespace Roslynator.Formatting.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class FixFormattingOfBinaryExpressionChainAnalyzer : BaseDiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(DiagnosticDescriptors.FixFormattingOfBinaryExpressionChain); }
        }

        public override void Initialize(AnalysisContext context)
        {
            base.Initialize(context);

            context.RegisterSyntaxNodeAction(
                f => AnalyzeBinaryExpression(f),
                SyntaxKind.AddExpression,
                SyntaxKind.SubtractExpression,
                SyntaxKind.MultiplyExpression,
                SyntaxKind.DivideExpression,
                SyntaxKind.ModuloExpression,
                SyntaxKind.LeftShiftExpression,
                SyntaxKind.RightShiftExpression,
                SyntaxKind.LogicalOrExpression,
                SyntaxKind.LogicalAndExpression,
                SyntaxKind.BitwiseOrExpression,
                SyntaxKind.BitwiseAndExpression,
                SyntaxKind.ExclusiveOrExpression,
                SyntaxKind.CoalesceExpression);
        }

        private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
        {
            var topBinaryExpression = (BinaryExpressionSyntax)context.Node;

            switch (topBinaryExpression.WalkUpParentheses().Parent.Kind())
            {
                case SyntaxKind.AddExpression:
                case SyntaxKind.SubtractExpression:
                case SyntaxKind.MultiplyExpression:
                case SyntaxKind.DivideExpression:
                case SyntaxKind.ModuloExpression:
                case SyntaxKind.LeftShiftExpression:
                case SyntaxKind.RightShiftExpression:
                case SyntaxKind.LogicalOrExpression:
                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.BitwiseOrExpression:
                case SyntaxKind.BitwiseAndExpression:
                case SyntaxKind.ExclusiveOrExpression:
                case SyntaxKind.CoalesceExpression:
                    return;
            }

            if (topBinaryExpression.IsSingleLine(includeExteriorTrivia: false))
                return;

            SyntaxKind binaryKind = topBinaryExpression.Kind();

            if (!topBinaryExpression.Left.WalkDownParentheses().IsKind(binaryKind)
                && topBinaryExpression
                    .SyntaxTree
                    .IsSingleLineSpan(TextSpan.FromBounds(topBinaryExpression.Left.Span.End, topBinaryExpression.Span.End)))
            {
                return;
            }

            IndentationAnalysis indentationAnalysis = default;

            BinaryExpressionSyntax binaryExpression = topBinaryExpression;

            while (true)
            {
                BinaryExpressionInfo info = SyntaxInfo.BinaryExpressionInfo(binaryExpression);

                SyntaxTriviaList leftTrailing = info.Left.GetTrailingTrivia();
                SyntaxTriviaList tokenTrailing = info.OperatorToken.TrailingTrivia;

                if (IsOptionalWhitespaceThenOptionalSingleLineCommentThenEndOfLineTrivia(leftTrailing))
                {
                    if (Analyze(info.OperatorToken))
                        return;
                }
                else if (IsOptionalWhitespaceThenOptionalSingleLineCommentThenEndOfLineTrivia(tokenTrailing))
                {
                    if (Analyze(info.Right))
                        return;
                }
                else if (leftTrailing.IsEmptyOrWhitespace()
                    && tokenTrailing.IsEmptyOrWhitespace())
                {
                    ReportDiagnostic();
                    return;
                }

                if (!info.Left.IsKind(binaryKind))
                    break;

                binaryExpression = (BinaryExpressionSyntax)info.Left;
            }

            bool Analyze(SyntaxNodeOrToken nodeOrToken)
            {
                SyntaxTriviaList.Reversed.Enumerator en = nodeOrToken.GetLeadingTrivia().Reverse().GetEnumerator();

                if (!en.MoveNext())
                {
                    ReportDiagnostic();
                    return true;
                }

                switch (en.Current.Kind())
                {
                    case SyntaxKind.WhitespaceTrivia:
                        {
                            if (indentationAnalysis.IsDefault)
                                indentationAnalysis = AnalyzeIndentation(topBinaryExpression);

                            if (en.Current.Span.Length != indentationAnalysis.IncreasedIndentationLength)
                            {
                                if (!en.MoveNext()
                                    || en.Current.IsEndOfLineTrivia())
                                {
                                    if (topBinaryExpression.FindTrivia(nodeOrToken.FullSpan.Start - 1).IsEndOfLineTrivia())
                                    {
                                        ReportDiagnostic();
                                        return true;
                                    }
                                }

                                break;
                            }

                            break;
                        }
                    case SyntaxKind.EndOfLineTrivia:
                        {
                            if (topBinaryExpression.FindTrivia(nodeOrToken.FullSpan.Start - 1).IsEndOfLineTrivia())
                            {
                                ReportDiagnostic();
                                return true;
                            }

                            break;
                        }
                }

                return false;
            }

            void ReportDiagnostic()
            {
                DiagnosticHelpers.ReportDiagnostic(
                    context,
                    DiagnosticDescriptors.FixFormattingOfBinaryExpressionChain,
                    topBinaryExpression);
            }
        }
    }
}
