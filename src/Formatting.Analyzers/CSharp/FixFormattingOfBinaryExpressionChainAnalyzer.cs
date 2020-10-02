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

            //IndentationAnalysis indentationAnalysis = default;
            int indentationLength = -1;

            BinaryExpressionSyntax binaryExpression = topBinaryExpression;

            while (true)
            {
                ExpressionSyntax left = binaryExpression.Left;
                SyntaxToken token = binaryExpression.OperatorToken;

                SyntaxTriviaList leftTrailing = left.GetTrailingTrivia();
                SyntaxTriviaList tokenTrailing = token.TrailingTrivia;

                if (IsOptionalWhitespaceThenOptionalSingleLineCommentThenEndOfLineTrivia(leftTrailing))
                {
                    if (Analyze(token))
                        return;
                }
                else if (IsOptionalWhitespaceThenOptionalSingleLineCommentThenEndOfLineTrivia(tokenTrailing))
                {
                    if (Analyze(binaryExpression.Right))
                        return;
                }
                else if (leftTrailing.IsEmptyOrWhitespace()
                    && tokenTrailing.IsEmptyOrWhitespace())
                {
                    ReportDiagnostic();
                    return;
                }

                left = left.WalkDownParentheses();

                if (!left.IsKind(binaryKind))
                    break;

                binaryExpression = (BinaryExpressionSyntax)left;
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
                            if (indentationLength == -1)
                            {
                                IndentationAnalysis indentationAnalysis = AnalyzeIndentation(topBinaryExpression);

                                SyntaxTrivia indentation = DetermineIndentation(topBinaryExpression, indentationAnalysis);

                                indentationLength = (indentation == indentationAnalysis.Indentation)
                                    ? indentationAnalysis.IncreasedIndentationLength
                                    : indentation.Span.Length;
                            }

                            if (en.Current.Span.Length != indentationLength)
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

        internal static SyntaxTrivia DetermineIndentation(BinaryExpressionSyntax binaryExpression, IndentationAnalysis indentationAnalysis)
        {
            SyntaxTrivia indentationTrivia = indentationAnalysis.Indentation;

            if (indentationTrivia.Span.End == binaryExpression.SpanStart)
            {
                int position = indentationTrivia.SpanStart - 1;

                SyntaxNode node = binaryExpression;

                while (!node.FullSpan.Contains(position))
                    node = node.Parent;

                SyntaxTrivia trivia = node.FindTrivia(position);

                if (trivia.IsEndOfLineTrivia()
                    && trivia.Span.End == indentationTrivia.SpanStart)
                {
                    return indentationTrivia;
                }
            }

            return indentationAnalysis.Indentation;
        }
    }
}
