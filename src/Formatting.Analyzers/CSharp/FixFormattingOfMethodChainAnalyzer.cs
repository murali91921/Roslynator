// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.CSharp;
using static Roslynator.CSharp.SyntaxTriviaAnalysis;

namespace Roslynator.Formatting.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class FixFormattingOfMethodChainAnalyzer : BaseDiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(DiagnosticDescriptors.FixFormattingOfMethodChain); }
        }

        public override void Initialize(AnalysisContext context)
        {
            base.Initialize(context);

            context.RegisterSyntaxNodeAction(f => Analyze(f), SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(f => Analyze(f), SyntaxKind.ElementAccessExpression);
            context.RegisterSyntaxNodeAction(f => Analyze(f), SyntaxKind.ConditionalAccessExpression);
        }

        private static void Analyze(SyntaxNodeAnalysisContext context)
        {
            var expression = (ExpressionSyntax)context.Node;

            if (expression.IsParentKind(
                SyntaxKind.ConditionalAccessExpression,
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxKind.ElementAccessExpression,
                SyntaxKind.MemberBindingExpression,
                SyntaxKind.InvocationExpression))
            {
                return;
            }

            if (expression.IsSingleLine(includeExteriorTrivia: false))
                return;

            IndentationAnalysis indentationAnalysis = default;

            foreach (SyntaxNode node in CSharpUtility.EnumerateExpressionChain(expression))
            {
                switch (node.Kind())
                {
                    case SyntaxKind.SimpleMemberAccessExpression:
                        {
                            var memberAccess = (MemberAccessExpressionSyntax)node;

                            if (AnalyzeToken(node, memberAccess.OperatorToken))
                                return;

                            break;
                        }
                    case SyntaxKind.MemberBindingExpression:
                        {
                            var memberBinding = (MemberBindingExpressionSyntax)node;

                            if (AnalyzeToken(node, memberBinding.OperatorToken))
                                return;

                            break;
                        }
                }
            }

            bool AnalyzeToken(SyntaxNode node, SyntaxToken token)
            {
                SyntaxTriviaList.Reversed.Enumerator en = token.LeadingTrivia.Reverse().GetEnumerator();

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
                                indentationAnalysis = AnalyzeIndentation(expression);

                            if (en.Current.Span.Length != indentationAnalysis.IndentationLength)
                            {
                                if (!en.MoveNext()
                                    || en.Current.IsEndOfLineTrivia())
                                {
                                    if (expression.FindTrivia(node.FullSpan.Start - 1).IsEndOfLineTrivia())
                                    {
                                        ReportDiagnostic();
                                        return true;
                                    }
                                }

                                break;
                            }

                            return false;
                        }
                    case SyntaxKind.EndOfLineTrivia:
                        {
                            if (expression.FindTrivia(node.FullSpan.Start - 1).IsEndOfLineTrivia())
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
                    DiagnosticDescriptors.FixFormattingOfMethodChain,
                    expression);
            }
        }
    }
}
