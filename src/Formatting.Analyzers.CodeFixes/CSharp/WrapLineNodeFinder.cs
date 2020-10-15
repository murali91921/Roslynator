// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;
using Roslynator.Formatting.CSharp;

namespace Roslynator.Formatting.CodeFixes
{
    internal class WrapLineNodeFinder
    {
        private Dictionary<SyntaxGroup, SyntaxNode> _nodes;
        private static readonly ImmutableDictionary<SyntaxKind, SyntaxGroup> _groupsMap = ImmutableDictionary.CreateRange(
            new[]
            {
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.ArrowExpressionClause,
                    SyntaxGroup.ArrowExpressionClause),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.EqualsValueClause, SyntaxGroup.EqualsValueClause),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.AttributeList, SyntaxGroup.AttributeList),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.ParameterList, SyntaxGroup.ParameterList),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.BracketedParameterList, SyntaxGroup.ParameterList),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.ArgumentList, SyntaxGroup.ArgumentList),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.BracketedArgumentList, SyntaxGroup.ArgumentList),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.AttributeArgumentList, SyntaxGroup.ArgumentList),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxGroup.MemberExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.MemberBindingExpression, SyntaxGroup.MemberExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.ArrayInitializerExpression,
                    SyntaxGroup.InitializerExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.CollectionInitializerExpression,
                    SyntaxGroup.InitializerExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.ComplexElementInitializerExpression,
                    SyntaxGroup.InitializerExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.ObjectInitializerExpression,
                    SyntaxGroup.InitializerExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.AddExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.SubtractExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.MultiplyExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.DivideExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.ModuloExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.LeftShiftExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.RightShiftExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.LogicalOrExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.LogicalAndExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.BitwiseOrExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.BitwiseAndExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.ExclusiveOrExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.CoalesceExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.ConditionalExpression,
                    SyntaxGroup.ConditionalExpression)
            });

        public Document Document { get; }

        public TextSpan Span { get; }

        public int MaxLineLength { get; }

        public WrapLineNodeFinder(Document document, TextSpan span, int maxLineLength)
        {
            Document = document;
            Span = span;
            MaxLineLength = maxLineLength;
        }

        public SyntaxNode FindNodeToFix(SyntaxNode root)
        {
            int position = Span.End;

            while (position >= Span.Start)
            {
                SyntaxToken token = root.FindToken(position);

                for (SyntaxNode node = token.Parent; node?.SpanStart >= Span.Start; node = node.Parent)
                {
                    if (ProcessNode(node))
                        break;
                }

                position = Math.Min(position, token.FullSpan.Start) - 1;
            }

            if (_nodes == null)
                return null;

            if (_nodes.TryGetValue(SyntaxGroup.ArgumentList, out SyntaxNode argumentList)
                && _nodes.TryGetValue(SyntaxGroup.MemberExpression, out SyntaxNode memberExpression))
            {
                SyntaxNode argumentListOrMemberExpression = ChooseBetweenArgumentListAndMemberExpression(
                    argumentList,
                    memberExpression);

                _nodes.Remove((_groupsMap[argumentListOrMemberExpression.Kind()] == SyntaxGroup.ArgumentList)
                    ? SyntaxGroup.MemberExpression
                    : SyntaxGroup.ArgumentList);
            }

            if (_nodes.TryGetValue(SyntaxGroup.BinaryExpression, out SyntaxNode binaryExpression)
                && _nodes.TryGetValue(SyntaxGroup.ArgumentList, out argumentList))
            {
                _nodes.Remove((binaryExpression.Contains(argumentList))
                    ? SyntaxGroup.ArgumentList
                    : SyntaxGroup.BinaryExpression);
            }

            if (_nodes.TryGetValue(SyntaxGroup.BinaryExpression, out SyntaxNode binaryExpression2)
                && _nodes.TryGetValue(SyntaxGroup.MemberExpression, out memberExpression))
            {
                _nodes.Remove((binaryExpression2.Contains(memberExpression))
                    ? SyntaxGroup.MemberExpression
                    : SyntaxGroup.BinaryExpression);
            }

            return _nodes
                .Select(f => f.Value)
                .OrderBy(f => f, SyntaxKindComparer.Instance)
                .First();
        }

        public bool ProcessNode(SyntaxNode node)
        {
            if (TryGetNode(node.Kind(), out SyntaxNode node2)
                && object.ReferenceEquals(node, node2))
            {
                return false;
            }

            switch (node.Kind())
            {
                case SyntaxKind.ArrowExpressionClause:
                    {
                        var expressionBody = (ArrowExpressionClauseSyntax)node;

                        SyntaxToken arrowToken = expressionBody.ArrowToken;
                        SyntaxToken previousToken = arrowToken.GetPreviousToken();

                        if (previousToken.SpanStart < Span.Start)
                            return false;

                        bool addNewLineAfter = Document.IsAnalyzerOptionEnabled(
                            AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt);

                        int wrapPosition = (addNewLineAfter) ? arrowToken.Span.End : previousToken.Span.End;
                        int start = (addNewLineAfter) ? expressionBody.Expression.SpanStart : arrowToken.SpanStart;
                        int longestLength = expressionBody.GetLastToken().GetNextToken().Span.End - start;

                        if (!CanWrap(expressionBody, wrapPosition, longestLength))
                            return false;

                        TryAdd(expressionBody);
                        return true;
                    }
                case SyntaxKind.EqualsValueClause:
                    {
                        if (!node.IsParentKind(SyntaxKind.PropertyDeclaration))
                            return false;

                        var equalsValueClause = (EqualsValueClauseSyntax)node;

                        SyntaxToken equalsToken = equalsValueClause.EqualsToken;
                        SyntaxToken previousToken = equalsToken.GetPreviousToken();

                        if (previousToken.SpanStart < Span.Start)
                            return false;

                        bool addNewLineAfter = Document.IsAnalyzerOptionEnabled(
                            AnalyzerOptions.AddNewLineAfterEqualsSignInsteadOfBeforeIt);

                        int wrapPosition = (addNewLineAfter) ? equalsToken.Span.End : previousToken.Span.End;
                        int start = (addNewLineAfter) ? equalsValueClause.Value.SpanStart : equalsToken.SpanStart;
                        int longestLength = Span.End - start;

                        if (!CanWrap(equalsValueClause, wrapPosition, longestLength))
                            return false;

                        TryAdd(equalsValueClause);
                        return true;
                    }
                case SyntaxKind.AttributeList:
                    {
                        var attributeList = (AttributeListSyntax)node;

                        if (!CanWrap(attributeList.Attributes, attributeList.OpenBracketToken.Span.End, 2))
                            return false;

                        TryAdd(attributeList);
                        return true;
                    }
                case SyntaxKind.ParameterList:
                    {
                        if (node.Parent is AnonymousFunctionExpressionSyntax)
                            return false;

                        if (!CanAdd(node))
                            return false;

                        var parameterList = (ParameterListSyntax)node;

                        if (!CanWrap(parameterList.Parameters, parameterList.OpenParenToken.Span.End))
                            return false;

                        TryAdd(parameterList);
                        return true;
                    }
                case SyntaxKind.BracketedParameterList:
                    {
                        if (!CanAdd(node))
                            return false;

                        var parameterList = (BracketedParameterListSyntax)node;

                        if (!CanWrap(parameterList.Parameters, parameterList.OpenBracketToken.Span.End))
                            return false;

                        TryAdd(parameterList);
                        return true;
                    }
                case SyntaxKind.ArgumentList:
                    {
                        if (!CanAdd(node))
                            return false;

                        var argumentList = (ArgumentListSyntax)node;

                        if (argumentList.Arguments.Count == 1
                            && argumentList.Parent is InvocationExpressionSyntax invocationExpression
                            && invocationExpression.Expression is IdentifierNameSyntax identifierName
                            && identifierName.Identifier.ValueText == "nameof")
                        {
                            return false;
                        }

                        if (!CanWrap(argumentList.Arguments, argumentList.OpenParenToken.Span.End))
                            return false;

                        TryAdd(argumentList);
                        return true;
                    }
                case SyntaxKind.BracketedArgumentList:
                    {
                        if (!CanAdd(node))
                            return false;

                        var argumentList = (BracketedArgumentListSyntax)node;

                        if (!CanWrap(argumentList.Arguments, argumentList.OpenBracketToken.Span.End))
                            return false;

                        TryAdd(argumentList);
                        return true;
                    }
                case SyntaxKind.AttributeArgumentList:
                    {
                        var argumentList = (AttributeArgumentListSyntax)node;

                        if (!CanWrap(argumentList.Arguments, argumentList.OpenParenToken.Span.End))
                            return false;

                        TryAdd(argumentList);
                        return true;
                    }
                case SyntaxKind.SimpleMemberAccessExpression:
                    {
                        if (!node.IsParentKind(SyntaxKind.InvocationExpression, SyntaxKind.ElementAccessExpression))
                            return false;

                        if (!CanAdd(node))
                            return false;

                        var memberAccessExpression = (MemberAccessExpressionSyntax)node;

                        SyntaxToken dotToken = memberAccessExpression.OperatorToken;

                        if (!CanWrap(memberAccessExpression, dotToken.SpanStart, Span.End - dotToken.SpanStart))
                            return false;

                        TryAdd(memberAccessExpression);
                        return true;
                    }
                case SyntaxKind.MemberBindingExpression:
                    {
                        if (!node.IsParentKind(SyntaxKind.InvocationExpression, SyntaxKind.ElementAccessExpression))
                            return false;

                        if (!CanAdd(node))
                            return false;

                        var memberBindingExpression = (MemberBindingExpressionSyntax)node;
                        SyntaxToken dotToken = memberBindingExpression.OperatorToken;

                        if (!CanWrap(memberBindingExpression, dotToken.SpanStart, Span.End - dotToken.SpanStart))
                            return false;

                        TryAdd(memberBindingExpression);
                        return true;
                    }
                case SyntaxKind.ConditionalExpression:
                    {
                        var conditionalExpression = (ConditionalExpressionSyntax)node;

                        SyntaxToken questionToken = conditionalExpression.QuestionToken;
                        SyntaxToken colonToken = conditionalExpression.ColonToken;

                        bool addNewLineAfter = Document.IsAnalyzerOptionEnabled(
                            AnalyzerOptions.AddNewLineAfterConditionalOperatorInsteadOfBeforeIt);

                        int wrapPosition = (addNewLineAfter)
                            ? questionToken.Span.End
                            : conditionalExpression.Condition.Span.End;

                        int start = (addNewLineAfter) ? conditionalExpression.WhenTrue.SpanStart : questionToken.SpanStart;
                        int end = (addNewLineAfter) ? colonToken.Span.End : conditionalExpression.WhenTrue.Span.End;
                        int longestLength = end - start;

                        start = (addNewLineAfter) ? conditionalExpression.WhenFalse.SpanStart : colonToken.SpanStart;
                        int longestLength2 = Span.End - start;

                        if (!CanWrap(conditionalExpression, wrapPosition, Math.Max(longestLength, longestLength2)))
                            return false;

                        TryAdd(conditionalExpression);
                        return true;
                    }
                case SyntaxKind.ArrayInitializerExpression:
                case SyntaxKind.CollectionInitializerExpression:
                case SyntaxKind.ComplexElementInitializerExpression:
                case SyntaxKind.ObjectInitializerExpression:
                    {
                        if (!CanAdd(node))
                            return false;

                        var initializer = (InitializerExpressionSyntax)node;

                        if (!CanWrap(initializer.Expressions, initializer.OpenBraceToken.Span.End))
                            return false;

                        TryAdd(initializer);
                        return true;
                    }
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
                    {
                        if (!CanAdd(node))
                            return false;

                        var binaryExpression = (BinaryExpressionSyntax)node;

                        SyntaxToken operatorToken = binaryExpression.OperatorToken;

                        bool addNewLineAfter = Document.IsAnalyzerOptionEnabled(
                            AnalyzerOptions.AddNewLineAfterBinaryOperatorInsteadOfBeforeIt);

                        int wrapPosition = (addNewLineAfter)
                            ? operatorToken.Span.End
                            : binaryExpression.Left.Span.End;

                        int longestLength = 0;

                        while (true)
                        {
                            BinaryExpressionSyntax parentBinaryExpression = null;
                            if (binaryExpression.Parent.IsKind(binaryExpression.Kind()))
                            {
                                parentBinaryExpression = (BinaryExpressionSyntax)binaryExpression.Parent;
                            }

                            int end;
                            if (parentBinaryExpression != null)
                            {
                                end = (addNewLineAfter)
                                    ? parentBinaryExpression.OperatorToken.Span.End
                                    : binaryExpression.Right.Span.End;
                            }
                            else
                            {
                                end = Span.End;
                            }

                            int start = (addNewLineAfter)
                                ? binaryExpression.Right.SpanStart
                                : binaryExpression.OperatorToken.SpanStart;

                            longestLength = Math.Max(longestLength, end - start);

                            if (parentBinaryExpression == null)
                                break;

                            binaryExpression = parentBinaryExpression;
                        }

                        if (!CanWrap(node, wrapPosition, longestLength))
                            return false;

                        TryAdd(node);
                        return true;
                    }
            }

            return false;
        }

        public SyntaxNode GetNodeToFix()
        {
            if (_nodes == null)
                return null;

            if (_nodes.TryGetValue(SyntaxGroup.ArgumentList, out SyntaxNode argumentList)
                && _nodes.TryGetValue(SyntaxGroup.MemberExpression, out SyntaxNode memberExpression))
            {
                SyntaxNode argumentListOrMemberExpression = ChooseBetweenArgumentListAndMemberExpression(
                    argumentList,
                    memberExpression);

                _nodes.Remove((_groupsMap[argumentListOrMemberExpression.Kind()] == SyntaxGroup.ArgumentList)
                    ? SyntaxGroup.MemberExpression
                    : SyntaxGroup.ArgumentList);
            }

            if (_nodes.TryGetValue(SyntaxGroup.BinaryExpression, out SyntaxNode binaryExpression)
                && _nodes.TryGetValue(SyntaxGroup.ArgumentList, out argumentList))
            {
                _nodes.Remove((binaryExpression.Contains(argumentList))
                    ? SyntaxGroup.ArgumentList
                    : SyntaxGroup.BinaryExpression);
            }

            if (_nodes.TryGetValue(SyntaxGroup.BinaryExpression, out SyntaxNode binaryExpression2)
                && _nodes.TryGetValue(SyntaxGroup.MemberExpression, out memberExpression))
            {
                _nodes.Remove((binaryExpression2.Contains(memberExpression))
                    ? SyntaxGroup.MemberExpression
                    : SyntaxGroup.BinaryExpression);
            }

            return _nodes
                .Select(f => f.Value)
                .OrderBy(f => f, SyntaxKindComparer.Instance)
                .First();
        }

        public bool TryGetNode(SyntaxKind kind, out SyntaxNode node)
        {
            if (_nodes != null)
            {
                return _nodes.TryGetValue(_groupsMap[kind], out node);
            }
            else
            {
                node = null;
                return false;
            }
        }

        public bool CanAdd(SyntaxNode node)
        {
            if (_nodes == null)
                return true;

            SyntaxKind kind = node.Kind();

            if (!TryGetNode(kind, out SyntaxNode node2))
                return true;

            if (kind == node2.Kind())
                return true;

            if (node2.Contains(node))
                return false;

            if (node.SpanStart > node2.SpanStart)
                return false;

            return true;
        }

        public void TryAdd(SyntaxNode node)
        {
            SyntaxGroup syntaxGroup = _groupsMap[node.Kind()];

            switch (syntaxGroup)
            {
                case SyntaxGroup.MemberExpression:
                case SyntaxGroup.ArgumentList:
                case SyntaxGroup.InitializerExpression:
                case SyntaxGroup.BinaryExpression:
                case SyntaxGroup.ConditionalExpression:
                    {
                        if (IsInsideInterpolation(node.Parent))
                            return;

                        break;
                    }
            }

            if (_nodes == null)
                _nodes = new Dictionary<SyntaxGroup, SyntaxNode>();

            _nodes[syntaxGroup] = node;

            static bool IsInsideInterpolation(SyntaxNode node)
            {
                for (SyntaxNode n = node; n != null; n = n.Parent)
                {
                    switch (n)
                    {
                        case MemberDeclarationSyntax _:
                        case StatementSyntax _:
                        case AccessorDeclarationSyntax _:
                            return false;
                        case InterpolationSyntax _:
                            return true;
                    }
                }

                return false;
            }
        }

        private bool CanWrap<TNode>(
            SeparatedSyntaxList<TNode> nodes,
            int wrapPosition,
            int minCount = 1) where TNode : SyntaxNode
        {
            if (nodes.Count < minCount)
                return false;

            int longestLength = nodes.Max(f => f.Span.Length);

            return CanWrap(nodes[0], wrapPosition, longestLength);
        }

        private bool CanWrap(
            SyntaxNode node,
            int wrapPosition,
            int longestLength)
        {
            if (wrapPosition - Span.Start > MaxLineLength)
                return false;

            //TODO: cache
            int indentationLength = SyntaxTriviaAnalysis.GetIncreasedIndentationLength(node);

            return indentationLength + longestLength <= MaxLineLength;
        }

        private SyntaxNode ChooseBetweenArgumentListAndMemberExpression(SyntaxNode argumentList, SyntaxNode memberExpression)
        {
            if (argumentList.Contains(memberExpression))
                return argumentList;

            if (memberExpression.Contains(argumentList))
                return memberExpression;

            if (memberExpression.Span.End == argumentList.SpanStart)
            {
                ExpressionSyntax expression = null;

                if (memberExpression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                {
                    var memberAccess2 = (MemberAccessExpressionSyntax)memberExpression;
                    expression = memberAccess2.Expression;
                }
                else
                {
                    var memberBinding2 = (MemberBindingExpressionSyntax)memberExpression;

                    if (memberBinding2.Parent is ConditionalAccessExpressionSyntax conditionalAccess)
                        expression = conditionalAccess.Expression;
                }

                if (expression is SimpleNameSyntax)
                    return argumentList;

                if (expression is CastExpressionSyntax castExpression
                    && castExpression.Expression is SimpleNameSyntax)
                {
                    return argumentList;
                }
            }

            if (argumentList.SpanStart > memberExpression.SpanStart)
                return memberExpression;

            return argumentList;
        }

        private class SyntaxKindComparer : IComparer<SyntaxNode>
        {
            public static SyntaxKindComparer Instance { get; } = new SyntaxKindComparer();

            public int Compare(SyntaxNode x, SyntaxNode y)
            {
                if (object.ReferenceEquals(x, y))
                    return 0;

                if (x == null)
                    return -1;

                if (y == null)
                    return 1;

                return GetRank(x.Kind()).CompareTo(GetRank(y.Kind()));
            }

            private static int GetRank(SyntaxKind kind)
            {
                switch (kind)
                {
                    case SyntaxKind.ArrowExpressionClause:
                        return 10;
                    case SyntaxKind.EqualsValueClause:
                        return 11;
                    case SyntaxKind.AttributeList:
                        return 12;
                    case SyntaxKind.ArrayInitializerExpression:
                    case SyntaxKind.CollectionInitializerExpression:
                    case SyntaxKind.ComplexElementInitializerExpression:
                    case SyntaxKind.ObjectInitializerExpression:
                        return 13;
                    case SyntaxKind.ParameterList:
                        return 20;
                    case SyntaxKind.BracketedParameterList:
                        return 30;
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
                        return 31;
                    case SyntaxKind.SimpleMemberAccessExpression:
                        return 40;
                    case SyntaxKind.MemberBindingExpression:
                        return 50;
                    case SyntaxKind.ArgumentList:
                        return 60;
                    case SyntaxKind.BracketedArgumentList:
                        return 61;
                    case SyntaxKind.AttributeArgumentList:
                        return 62;
                    case SyntaxKind.ConditionalExpression:
                        return 70;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        private enum SyntaxGroup
        {
            ArrowExpressionClause,
            EqualsValueClause,
            AttributeList,
            ParameterList,
            MemberExpression,
            ArgumentList,
            InitializerExpression,
            BinaryExpression,
            ConditionalExpression,
        }
    }
}

