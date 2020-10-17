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
        private readonly HashSet<SyntaxNode> _processedNodes;

        private static readonly ImmutableDictionary<SyntaxKind, SyntaxGroup> _groupsMap = ImmutableDictionary.CreateRange(
            new[]
            {
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.ArrowExpressionClause,
                    SyntaxGroup.ArrowExpressionClause),
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
                    SyntaxKind.ConditionalExpression,
                    SyntaxGroup.ConditionalExpression),
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
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.EqualsExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.NotEqualsExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.LessThanExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.LessThanOrEqualExpression,
                    SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(SyntaxKind.GreaterThanExpression, SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.GreaterThanOrEqualExpression,
                    SyntaxGroup.BinaryExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.AddAssignmentExpression,
                    SyntaxGroup.AssignmentExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.AndAssignmentExpression,
                    SyntaxGroup.AssignmentExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.CoalesceAssignmentExpression,
                    SyntaxGroup.AssignmentExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.DivideAssignmentExpression,
                    SyntaxGroup.AssignmentExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.ExclusiveOrAssignmentExpression,
                    SyntaxGroup.AssignmentExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.LeftShiftAssignmentExpression,
                    SyntaxGroup.AssignmentExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.ModuloAssignmentExpression,
                    SyntaxGroup.AssignmentExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.MultiplyAssignmentExpression,
                    SyntaxGroup.AssignmentExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.OrAssignmentExpression,
                    SyntaxGroup.AssignmentExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.RightShiftAssignmentExpression,
                    SyntaxGroup.AssignmentExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxGroup.AssignmentExpression),
                new KeyValuePair<SyntaxKind, SyntaxGroup>(
                    SyntaxKind.SubtractAssignmentExpression,
                    SyntaxGroup.AssignmentExpression),
            });

        public Document Document { get; }

        public TextSpan Span { get; }

        public int MaxLineLength { get; }

        public WrapLineNodeFinder(Document document, TextSpan span, int maxLineLength)
        {
            Document = document;
            Span = span;
            MaxLineLength = maxLineLength;

            _processedNodes = new HashSet<SyntaxNode>();
        }

        public SyntaxNode FindNodeToFix(SyntaxNode root)
        {
            int position = Span.End;

            while (position >= Span.Start)
            {
                SyntaxToken token = root.FindToken(position);

                for (SyntaxNode node = token.Parent; node?.SpanStart >= Span.Start; node = node.Parent)
                {
                    if (_processedNodes.Contains(node))
                        continue;

                    if (TryGetSyntaxGroup(node, out SyntaxGroup syntaxGroup)
                        && ShouldAnalyze(node, syntaxGroup))
                    {
                        SyntaxNode fixableNode = GetFixableNode(node);

                        if (fixableNode != null)
                            (_nodes ??= new Dictionary<SyntaxGroup, SyntaxNode>())[syntaxGroup] = node;
                    }

                    _processedNodes.Add(node);
                    break;
                }

                position = Math.Min(position, token.FullSpan.Start) - 1;
            }

            if (_nodes == null)
                return null;

            if (TryGetNode(SyntaxGroup.ArgumentList, out SyntaxNode argumentList)
                && TryGetNode(SyntaxGroup.MemberExpression, out SyntaxNode memberExpression))
            {
                SyntaxNode argumentListOrMemberExpression = ChooseBetweenArgumentListAndMemberExpression(
                    argumentList,
                    memberExpression);

                _nodes.Remove((GetSyntaxGroup(argumentListOrMemberExpression) == SyntaxGroup.ArgumentList)
                    ? SyntaxGroup.MemberExpression
                    : SyntaxGroup.ArgumentList);
            }

            if (TryGetNode(SyntaxGroup.BinaryExpression, out SyntaxNode binaryExpression)
                && TryGetNode(SyntaxGroup.ArgumentList, out argumentList))
            {
                Remove(binaryExpression, argumentList);
            }

            if (TryGetNode(SyntaxGroup.BinaryExpression, out SyntaxNode binaryExpression2)
                && TryGetNode(SyntaxGroup.MemberExpression, out memberExpression))
            {
                Remove(binaryExpression, memberExpression);
            }

            if (TryGetNode(SyntaxGroup.AssignmentExpression, out SyntaxNode assignmentNode))
            {
                var assignmentExpression = (AssignmentExpressionSyntax)assignmentNode;

                foreach (KeyValuePair<SyntaxGroup, SyntaxNode> kvp in _nodes)
                {
                    if (assignmentExpression.Left.Contains(kvp.Value))
                        _nodes.Remove(kvp.Key);
                }
            }

            return _nodes
                .Select(f => f.Value)
                .OrderBy(f => f, SyntaxKindComparer.Instance)
                .First();
        }

        private SyntaxNode GetFixableNode(SyntaxNode node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.ArrowExpressionClause:
                    {
                        var expressionBody = (ArrowExpressionClauseSyntax)node;

                        SyntaxToken arrowToken = expressionBody.ArrowToken;
                        SyntaxToken previousToken = arrowToken.GetPreviousToken();

                        if (previousToken.SpanStart < Span.Start)
                            return null;

                        bool addNewLineAfter = Document.IsAnalyzerOptionEnabled(
                            AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt);

                        int wrapPosition = (addNewLineAfter) ? arrowToken.Span.End : previousToken.Span.End;
                        int start = (addNewLineAfter) ? expressionBody.Expression.SpanStart : arrowToken.SpanStart;
                        int longestLength = expressionBody.GetLastToken().GetNextToken().Span.End - start;

                        if (!CanWrap(expressionBody, wrapPosition, longestLength))
                            return null;

                        return expressionBody;
                    }
                case SyntaxKind.EqualsValueClause:
                    {
                        var equalsValueClause = (EqualsValueClauseSyntax)node;

                        SyntaxToken equalsToken = equalsValueClause.EqualsToken;
                        SyntaxToken previousToken = equalsToken.GetPreviousToken();

                        if (previousToken.SpanStart < Span.Start)
                            return null;

                        bool addNewLineAfter = Document.IsAnalyzerOptionEnabled(
                            AnalyzerOptions.AddNewLineAfterEqualsSignInsteadOfBeforeIt);

                        int wrapPosition = (addNewLineAfter) ? equalsToken.Span.End : previousToken.Span.End;
                        int start = (addNewLineAfter) ? equalsValueClause.Value.SpanStart : equalsToken.SpanStart;
                        int longestLength = Span.End - start;

                        if (!CanWrap(equalsValueClause, wrapPosition, longestLength))
                            return null;

                        return equalsValueClause;
                    }
                case SyntaxKind.AttributeList:
                    {
                        var attributeList = (AttributeListSyntax)node;

                        if (!CanWrap(attributeList.Attributes, attributeList.OpenBracketToken.Span.End, 2))
                            return null;

                        return attributeList;
                    }
                case SyntaxKind.ParameterList:
                    {
                        if (node.Parent is AnonymousFunctionExpressionSyntax)
                            return null;

                        var parameterList = (ParameterListSyntax)node;

                        if (!CanWrap(parameterList.Parameters, parameterList.OpenParenToken.Span.End))
                            return null;

                        return parameterList;
                    }
                case SyntaxKind.BracketedParameterList:
                    {
                        var parameterList = (BracketedParameterListSyntax)node;

                        if (!CanWrap(parameterList.Parameters, parameterList.OpenBracketToken.Span.End))
                            return null;

                        return parameterList;
                    }
                case SyntaxKind.ArgumentList:
                    {
                        var argumentList = (ArgumentListSyntax)node;

                        if (argumentList.Arguments.Count == 1
                            && argumentList.Parent is InvocationExpressionSyntax invocationExpression
                            && invocationExpression.Expression is IdentifierNameSyntax identifierName
                            && identifierName.Identifier.ValueText == "nameof")
                        {
                            return null;
                        }

                        if (!CanWrap(argumentList.Arguments, argumentList.OpenParenToken.Span.End))
                            return null;

                        return argumentList;
                    }
                case SyntaxKind.BracketedArgumentList:
                    {
                        var argumentList = (BracketedArgumentListSyntax)node;

                        if (!CanWrap(argumentList.Arguments, argumentList.OpenBracketToken.Span.End))
                            return null;

                        return argumentList;
                    }
                case SyntaxKind.AttributeArgumentList:
                    {
                        var argumentList = (AttributeArgumentListSyntax)node;

                        if (!CanWrap(argumentList.Arguments, argumentList.OpenParenToken.Span.End))
                            return null;

                        return argumentList;
                    }
                case SyntaxKind.SimpleMemberAccessExpression:
                    {
                        var memberAccessExpression = (MemberAccessExpressionSyntax)node;

                        SyntaxToken dotToken = memberAccessExpression.OperatorToken;

                        if (!CanWrap(memberAccessExpression, dotToken.SpanStart, Span.End - dotToken.SpanStart))
                            return null;

                        return memberAccessExpression;
                    }
                case SyntaxKind.MemberBindingExpression:
                    {
                        var memberBindingExpression = (MemberBindingExpressionSyntax)node;
                        SyntaxToken dotToken = memberBindingExpression.OperatorToken;

                        if (!CanWrap(memberBindingExpression, dotToken.SpanStart, Span.End - dotToken.SpanStart))
                            return null;

                        return memberBindingExpression;
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
                            return null;

                        return conditionalExpression;
                    }
                case SyntaxKind.ArrayInitializerExpression:
                case SyntaxKind.CollectionInitializerExpression:
                case SyntaxKind.ComplexElementInitializerExpression:
                case SyntaxKind.ObjectInitializerExpression:
                    {
                        var initializer = (InitializerExpressionSyntax)node;

                        if (!CanWrap(initializer.Expressions, initializer.OpenBraceToken.Span.End))
                            return null;

                        return initializer;
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
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:
                case SyntaxKind.LessThanExpression:
                case SyntaxKind.LessThanOrEqualExpression:
                case SyntaxKind.GreaterThanExpression:
                case SyntaxKind.GreaterThanOrEqualExpression:
                    {
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
                            return null;

                        return node;
                    }
                case SyntaxKind.AddAssignmentExpression:
                case SyntaxKind.AndAssignmentExpression:
                case SyntaxKind.CoalesceAssignmentExpression:
                case SyntaxKind.DivideAssignmentExpression:
                case SyntaxKind.ExclusiveOrAssignmentExpression:
                case SyntaxKind.LeftShiftAssignmentExpression:
                case SyntaxKind.ModuloAssignmentExpression:
                case SyntaxKind.MultiplyAssignmentExpression:
                case SyntaxKind.OrAssignmentExpression:
                case SyntaxKind.RightShiftAssignmentExpression:
                case SyntaxKind.SimpleAssignmentExpression:
                case SyntaxKind.SubtractAssignmentExpression:
                    {
                        var assignment = (AssignmentExpressionSyntax)node;

                        SyntaxToken operatorToken = assignment.OperatorToken;
                        SyntaxNode left = assignment.Left;

                        if (left.SpanStart < Span.Start)
                            return null;

                        bool addNewLineAfter = Document.IsAnalyzerOptionEnabled(
                            AnalyzerOptions.AddNewLineAfterEqualsSignInsteadOfBeforeIt);

                        int wrapPosition = (addNewLineAfter) ? operatorToken.Span.End : left.Span.End;
                        int start = (addNewLineAfter) ? assignment.Right.SpanStart : operatorToken.SpanStart;
                        int longestLength = Span.End - start;

                        if (!CanWrap(assignment, wrapPosition, longestLength))
                            return null;

                        return assignment;
                    }
            }

            return null;
        }

        private static SyntaxGroup GetSyntaxGroup(SyntaxNode node)
        {
            if (TryGetSyntaxGroup(node, out SyntaxGroup syntaxGroup))
                return syntaxGroup;

            throw new ArgumentException("", nameof(node));
        }

        private static bool TryGetSyntaxGroup(SyntaxNode node, out SyntaxGroup syntaxGroup)
        {
            SyntaxKind kind = node.Kind();

            if (_groupsMap.TryGetValue(kind, out syntaxGroup))
                return true;

            if (kind == SyntaxKind.EqualsValueClause)
            {
                SyntaxNode parent = node.Parent;

                if (parent.IsKind(SyntaxKind.PropertyDeclaration))
                {
                    syntaxGroup = SyntaxGroup.PropertyInitializer;
                    return true;
                }

                if (parent.IsKind(SyntaxKind.VariableDeclarator))
                {
                    parent = parent.Parent;

                    if (parent.IsKind(SyntaxKind.VariableDeclaration))
                    {
                        parent = parent.Parent;

                        if (parent.IsKind(SyntaxKind.FieldDeclaration, SyntaxKind.LocalDeclarationStatement))
                        {
                            syntaxGroup = SyntaxGroup.FieldOrLocalInitializer;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool TryGetNode(SyntaxGroup syntaxGroup, out SyntaxNode node)
        {
            if (_nodes != null)
            {
                return _nodes.TryGetValue(syntaxGroup, out node);
            }
            else
            {
                node = null;
                return false;
            }
        }

        private void Remove(SyntaxNode node1, SyntaxNode node2)
        {
            _nodes.Remove(GetSyntaxGroup(GetNodeToRemove(node1, node2)));

            static SyntaxNode GetNodeToRemove(SyntaxNode node1, SyntaxNode node2)
            {
                if (node1.FullSpan.Contains(node2.FullSpan))
                    return node2;

                if (node2.FullSpan.Contains(node1.FullSpan))
                    return node1;

                if (node1.SpanStart > node2.SpanStart)
                    return node2;

                return node1;
            }
        }

        private bool ShouldAnalyze(SyntaxNode node, SyntaxGroup syntaxGroup)
        {
            switch (syntaxGroup)
            {
                case SyntaxGroup.MemberExpression:
                case SyntaxGroup.ArgumentList:
                case SyntaxGroup.InitializerExpression:
                case SyntaxGroup.BinaryExpression:
                case SyntaxGroup.ConditionalExpression:
                    {
                        if (IsInsideInterpolation(node.Parent))
                            return false;

                        break;
                    }
            }

            if (_nodes == null)
                return true;

            foreach (KeyValuePair<SyntaxGroup, SyntaxNode> kvp in _nodes)
            {
                switch (kvp.Key)
                {
                    case SyntaxGroup.ConditionalExpression:
                    case SyntaxGroup.InitializerExpression:
                    case SyntaxGroup.BinaryExpression:
                    case SyntaxGroup.MemberExpression:
                    case SyntaxGroup.ArgumentList:
                        {
                            if (kvp.Value.FullSpan.Contains(node.FullSpan))
                                return false;

                            break;
                        }
                }
            }

            if (!_nodes.TryGetValue(syntaxGroup, out SyntaxNode node2))
                return true;

            if (node.FullSpan.Contains(node2.FullSpan))
                return true;

            if (syntaxGroup == SyntaxGroup.MemberExpression)
            {
                if (TryGetNode(SyntaxGroup.ArgumentList, out SyntaxNode argumentList)
                    && TryGetNode(SyntaxGroup.MemberExpression, out SyntaxNode memberExpression))
                {
                    SyntaxNode argumentListOrMemberExpression = ChooseBetweenArgumentListAndMemberExpression(
                        argumentList,
                        memberExpression);

                    if (GetSyntaxGroup(argumentListOrMemberExpression) == SyntaxGroup.ArgumentList)
                    {
                        _nodes.Remove(SyntaxGroup.MemberExpression);
                        return true;
                    }
                }
            }
            else if (syntaxGroup == SyntaxGroup.ArgumentList)
            {
                if (TryGetNode(SyntaxGroup.ArgumentList, out SyntaxNode argumentList)
                    && TryGetNode(SyntaxGroup.MemberExpression, out SyntaxNode memberExpression))
                {
                    SyntaxNode argumentListAndMemberExpression = ChooseBetweenArgumentListAndMemberExpression(
                        argumentList,
                        memberExpression);

                    if (GetSyntaxGroup(argumentListAndMemberExpression) == SyntaxGroup.MemberExpression)
                    {
                        _nodes.Remove(SyntaxGroup.ArgumentList);
                        return true;
                    }
                }
            }

            return false;

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

            int indentationLength = SyntaxTriviaAnalysis.GetIncreasedIndentationLength(node);

            return indentationLength + longestLength <= MaxLineLength;
        }

        private SyntaxNode ChooseBetweenArgumentListAndMemberExpression(SyntaxNode argumentList, SyntaxNode memberExpression)
        {
            if (argumentList.FullSpan.Contains(memberExpression.FullSpan))
                return argumentList;

            if (memberExpression.FullSpan.Contains(argumentList.FullSpan))
                return memberExpression;

            if (memberExpression.Span.End == argumentList.SpanStart)
            {
                SyntaxToken dotToken = (memberExpression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    ? ((MemberAccessExpressionSyntax)memberExpression).OperatorToken
                    : ((MemberBindingExpressionSyntax)memberExpression).OperatorToken;

                var expression = (ExpressionSyntax)memberExpression;

                if (memberExpression is MemberBindingExpressionSyntax memberBinding)
                {
                    SyntaxToken token = memberBinding.OperatorToken.GetPreviousToken();

                    if (token.IsKind(SyntaxKind.QuestionToken)
                        && token.FullSpan.End == memberBinding.OperatorToken.SpanStart
                        && token.Parent.IsKind(SyntaxKind.ConditionalAccessExpression))
                    {
                        expression = (ExpressionSyntax)token.Parent;
                    }
                }

                foreach (SyntaxNode node in new MethodChain(expression))
                {
                    SyntaxKind kind = node.Kind();

                    if (kind == SyntaxKind.SimpleMemberAccessExpression)
                    {
                        if (((MemberAccessExpressionSyntax)node).OperatorToken.SpanStart < dotToken.SpanStart)
                            return memberExpression;
                    }
                    else if (kind == SyntaxKind.MemberBindingExpression)
                    {
                        if (((MemberBindingExpressionSyntax)node).OperatorToken.SpanStart < dotToken.SpanStart)
                            return memberExpression;
                    }
                }

                return argumentList;
            }

            return memberExpression;
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

                return GetSyntaxGroup(x).CompareTo(GetSyntaxGroup(y));
            }
        }

        private enum SyntaxGroup
        {
            ArrowExpressionClause = 0,
            PropertyInitializer = 1,
            ConditionalExpression = 2,
            AttributeList = 3,
            InitializerExpression = 4,
            ParameterList = 5,
            BinaryExpression = 6,
            MemberExpression = 7,
            ArgumentList = 8,
            AssignmentExpression = 9,
            FieldOrLocalInitializer = 10,
        }
    }
}

