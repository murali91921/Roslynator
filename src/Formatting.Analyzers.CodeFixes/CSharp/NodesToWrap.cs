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
    internal class NodesToWrap
    {
        private Dictionary<SyntaxGroup, SyntaxNode> _nodes;
        private Document document;
        private TextSpan span;
        private int maxLength;
        private string indentation;

        //private static readonly SyntaxKind[] _parameterListKinds = new[]
        //{
        //    SyntaxKind.ParameterList,
        //    SyntaxKind.BracketedParameterList,
        //};

        //private static readonly SyntaxKind[] _argumentListKinds = new[]
        //{
        //    SyntaxKind.ArgumentList,
        //    SyntaxKind.BracketedArgumentList,
        //};

        //private static readonly SyntaxKind[] _memberExpressionKinds = new[]
        //{
        //    SyntaxKind.SimpleMemberAccessExpression,
        //    SyntaxKind.MemberBindingExpression,
        //};

        //private static readonly SyntaxKind[] _initializerKinds = new[]
        //{
        //    SyntaxKind.ArrayInitializerExpression,
        //    SyntaxKind.CollectionInitializerExpression,
        //    SyntaxKind.ComplexElementInitializerExpression,
        //    SyntaxKind.ObjectInitializerExpression,
        //};

        //private static readonly SyntaxKind[] _binaryExpressionKinds = new[]
        //{
        //    SyntaxKind.AddExpression,
        //    SyntaxKind.SubtractExpression,
        //    SyntaxKind.MultiplyExpression,
        //    SyntaxKind.DivideExpression,
        //    SyntaxKind.ModuloExpression,
        //    SyntaxKind.LeftShiftExpression,
        //    SyntaxKind.RightShiftExpression,
        //    SyntaxKind.LogicalOrExpression,
        //    SyntaxKind.LogicalAndExpression,
        //    SyntaxKind.BitwiseOrExpression,
        //    SyntaxKind.BitwiseAndExpression,
        //    SyntaxKind.ExclusiveOrExpression,
        //    SyntaxKind.CoalesceExpression,
        //};

        //private static readonly ImmutableDictionary<SyntaxKind, SyntaxKind[]> _kindsMap = ImmutableDictionary.CreateRange(
        //    new[]
        //    {
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.ParameterList, _parameterListKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.BracketedParameterList, _parameterListKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.ArgumentList, _argumentListKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.BracketedArgumentList, _argumentListKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.AttributeArgumentList, _argumentListKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.SimpleMemberAccessExpression, _memberExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.MemberBindingExpression, _memberExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.ArrayInitializerExpression, _initializerKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.CollectionInitializerExpression, _initializerKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(
        //            SyntaxKind.ComplexElementInitializerExpression,
        //            _initializerKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.ObjectInitializerExpression, _initializerKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.AddExpression, _binaryExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.SubtractExpression, _binaryExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.MultiplyExpression, _binaryExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.DivideExpression, _binaryExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.ModuloExpression, _binaryExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.LeftShiftExpression, _binaryExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.RightShiftExpression, _binaryExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.LogicalOrExpression, _binaryExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.LogicalAndExpression, _binaryExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.BitwiseOrExpression, _binaryExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.BitwiseAndExpression, _binaryExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.ExclusiveOrExpression, _binaryExpressionKinds),
        //        new KeyValuePair<SyntaxKind, SyntaxKind[]>(SyntaxKind.CoalesceExpression, _binaryExpressionKinds)
        //    });

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

        public void ProcessNode(SyntaxNode node)
        {
            SyntaxKind kind = node.Kind();

            switch (kind)
            {
                case SyntaxKind.ArrowExpressionClause:
                    {
                        var expressionBody = (ArrowExpressionClauseSyntax)node;

                        SyntaxToken arrowToken = expressionBody.ArrowToken;
                        SyntaxToken previousToken = arrowToken.GetPreviousToken();

                        if (previousToken.SpanStart < span.Start)
                            return;

                        bool addNewLineAfter = document.IsAnalyzerOptionEnabled(
                            AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt);

                        int wrapPosition = (addNewLineAfter) ? arrowToken.Span.End : previousToken.Span.End;
                        int start = (addNewLineAfter) ? expressionBody.Expression.SpanStart : arrowToken.SpanStart;
                        int longestLength = expressionBody.GetLastToken().GetNextToken().Span.End - start;

                        if (!CanWrapNode(expressionBody, wrapPosition, longestLength))
                            return;

                        TryAdd(expressionBody);
                        break;
                    }
                case SyntaxKind.EqualsValueClause:
                    {
                        var equalsValueClause = (EqualsValueClauseSyntax)node;

                        if (!equalsValueClause.IsParentKind(SyntaxKind.PropertyDeclaration))
                            return;

                        SyntaxToken equalsToken = equalsValueClause.EqualsToken;
                        SyntaxToken previousToken = equalsToken.GetPreviousToken();

                        if (previousToken.SpanStart < span.Start)
                            return;

                        bool addNewLineAfter = document.IsAnalyzerOptionEnabled(
                            AnalyzerOptions.AddNewLineAfterEqualsSignInsteadOfBeforeIt);

                        int wrapPosition = (addNewLineAfter) ? equalsToken.Span.End : previousToken.Span.End;
                        int start = (addNewLineAfter) ? equalsValueClause.Value.SpanStart : equalsToken.SpanStart;
                        int longestLength = span.End - start;

                        if (!CanWrapNode(equalsValueClause, wrapPosition, longestLength))
                            return;

                        TryAdd(equalsValueClause);
                        break;
                    }
                case SyntaxKind.AttributeList:
                    {
                        var attributeList = (AttributeListSyntax)node;

                        if (!CanWrapSeparatedList(attributeList.Attributes, attributeList.OpenBracketToken.Span.End, 2))
                            return;

                        TryAdd(attributeList);
                        break;
                    }
                case SyntaxKind.ParameterList:
                    {
                        if (node.Parent is AnonymousFunctionExpressionSyntax)
                            return;

                        if (!CanAdd(node))
                            return;

                        var parameterList = (ParameterListSyntax)node;

                        if (!CanWrapSeparatedList(parameterList.Parameters, parameterList.OpenParenToken.Span.End))
                            return;

                        TryAdd(parameterList);
                        break;
                    }
                case SyntaxKind.BracketedParameterList:
                    {
                        if (!CanAdd(node))
                            return;

                        var parameterList = (BracketedParameterListSyntax)node;

                        if (!CanWrapSeparatedList(parameterList.Parameters, parameterList.OpenBracketToken.Span.End))
                            return;

                        TryAdd(parameterList);
                        break;
                    }
                case SyntaxKind.ArgumentList:
                    {
                        if (!CanAdd(node))
                            return;

                        var argumentList = (ArgumentListSyntax)node;

                        if (argumentList.Arguments.Count == 1
                            && argumentList.Parent is InvocationExpressionSyntax invocationExpression
                            && invocationExpression.Expression is IdentifierNameSyntax identifierName
                            && identifierName.Identifier.ValueText == "nameof")
                        {
                            return;
                        }

                        if (!CanWrapSeparatedList(argumentList.Arguments, argumentList.OpenParenToken.Span.End))
                            return;

                        TryAdd(argumentList);
                        break;
                    }
                case SyntaxKind.BracketedArgumentList:
                    {
                        if (!CanAdd(node))
                            return;

                        var argumentList = (BracketedArgumentListSyntax)node;

                        if (!CanWrapSeparatedList(argumentList.Arguments, argumentList.OpenBracketToken.Span.End))
                            return;

                        TryAdd(argumentList);
                        break;
                    }
                case SyntaxKind.AttributeArgumentList:
                    {
                        var argumentList = (AttributeArgumentListSyntax)node;

                        if (!CanWrapSeparatedList(argumentList.Arguments, argumentList.OpenParenToken.Span.End))
                            return;

                        TryAdd(argumentList);
                        break;
                    }
                case SyntaxKind.SimpleMemberAccessExpression:
                    {
                        if (!node.IsParentKind(SyntaxKind.InvocationExpression, SyntaxKind.ElementAccessExpression))
                            return;

                        if (!CanAdd(node))
                            return;

                        var memberAccessExpression = (MemberAccessExpressionSyntax)node;

                        SyntaxToken dotToken = memberAccessExpression.OperatorToken;

                        if (!CanWrapNode(memberAccessExpression, dotToken.SpanStart, span.End - dotToken.SpanStart))
                            return;

                        TryAdd(memberAccessExpression);
                        break;
                    }
                case SyntaxKind.MemberBindingExpression:
                    {
                        if (!node.IsParentKind(SyntaxKind.InvocationExpression, SyntaxKind.ElementAccessExpression))
                            return;

                        if (!CanAdd(node))
                            return;

                        var memberBindingExpression = (MemberBindingExpressionSyntax)node;
                        SyntaxToken dotToken = memberBindingExpression.OperatorToken;

                        if (!CanWrapNode(memberBindingExpression, dotToken.SpanStart, span.End - dotToken.SpanStart))
                            return;

                        TryAdd(memberBindingExpression);
                        break;
                    }
                case SyntaxKind.ConditionalExpression:
                    {
                        var conditionalExpression = (ConditionalExpressionSyntax)node;

                        SyntaxToken questionToken = conditionalExpression.QuestionToken;
                        SyntaxToken colonToken = conditionalExpression.ColonToken;

                        bool addNewLineAfter = document.IsAnalyzerOptionEnabled(
                            AnalyzerOptions.AddNewLineAfterConditionalOperatorInsteadOfBeforeIt);

                        int wrapPosition = (addNewLineAfter)
                            ? questionToken.Span.End
                            : conditionalExpression.Condition.Span.End;

                        int start = (addNewLineAfter) ? conditionalExpression.WhenTrue.SpanStart : questionToken.SpanStart;
                        int end = (addNewLineAfter) ? colonToken.Span.End : conditionalExpression.WhenTrue.Span.End;
                        int longestLength = end - start;

                        start = (addNewLineAfter) ? conditionalExpression.WhenFalse.SpanStart : colonToken.SpanStart;
                        int longestLength2 = span.End - start;

                        if (!CanWrapNode(conditionalExpression, wrapPosition, Math.Max(longestLength, longestLength2)))
                            return;

                        TryAdd(conditionalExpression);
                        break;
                    }
                case SyntaxKind.ArrayInitializerExpression:
                case SyntaxKind.CollectionInitializerExpression:
                case SyntaxKind.ComplexElementInitializerExpression:
                case SyntaxKind.ObjectInitializerExpression:
                    {
                        if (!CanAdd(node))
                            return;

                        var initializer = (InitializerExpressionSyntax)node;

                        if (!CanWrapSeparatedList(initializer.Expressions, initializer.OpenBraceToken.Span.End))
                            return;

                        TryAdd(initializer);
                        break;
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
                            return;

                        var binaryExpression = (BinaryExpressionSyntax)node;

                        SyntaxToken operatorToken = binaryExpression.OperatorToken;

                        bool addNewLineAfter = document.IsAnalyzerOptionEnabled(
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
                                end = span.End;
                            }

                            int start = (addNewLineAfter)
                                ? binaryExpression.Right.SpanStart
                                : binaryExpression.OperatorToken.SpanStart;

                            longestLength = Math.Max(longestLength, end - start);

                            if (parentBinaryExpression == null)
                                break;

                            binaryExpression = parentBinaryExpression;
                        }

                        if (!CanWrapNode(node, wrapPosition, longestLength))
                            return;

                        TryAdd(node);
                        break;
                    }
            }
        }

        private bool TryGetNode(SyntaxKind kind, out SyntaxNode node)
        {
            SyntaxGroup syntaxGroup = _groupsMap[kind];

            return _nodes.TryGetValue(syntaxGroup, out node);
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

        private bool CanWrapSeparatedList<TNode>(
            SeparatedSyntaxList<TNode> nodes,
            int wrapPosition,
            int minCount = 1) where TNode : SyntaxNode
        {
            if (nodes.Count < minCount)
                return false;

            int longestLength = nodes.Max(f => f.Span.Length);

            return CanWrapNode(nodes[0], wrapPosition, longestLength);
        }

        private bool CanWrapNode(
            SyntaxNode node,
            int wrapPosition,
            int longestLength)
        {
            if (wrapPosition - span.Start > maxLength)
                return false;

            indentation = SyntaxTriviaAnalysis.GetIncreasedIndentation(node);

            return indentation.Length + longestLength <= maxLength;
        }

        SyntaxNode ChooseBetweenArgumentListAndMemberExpression(SyntaxNode argumentList, SyntaxNode memberExpression)
        {
            //if (!spans.ContainsKey(SyntaxKind.ArgumentList))
            //    return null;

            //if (!spans.ContainsKey(SyntaxKind.SimpleMemberAccessExpression)
            //    && !spans.ContainsKey(SyntaxKind.MemberBindingExpression))
            //{
            //    return null;
            //}

            //SyntaxNode argumentList = spans[SyntaxKind.ArgumentList];

            //SyntaxNode memberExpression = null;

            //SyntaxNode memberAccess = (spans.ContainsKey(SyntaxKind.SimpleMemberAccessExpression))
            //    ? spans[SyntaxKind.SimpleMemberAccessExpression]
            //    : null;

            //SyntaxNode memberBinding = (spans.ContainsKey(SyntaxKind.MemberBindingExpression))
            //    ? spans[SyntaxKind.MemberBindingExpression]
            //    : null;

            //if (memberAccess != null)
            //{
            //    if (memberBinding != null)
            //    {
            //        if (memberAccess.Contains(memberBinding))
            //        {
            //            memberExpression = memberAccess;
            //        }
            //        else if (memberBinding.Contains(memberAccess))
            //        {
            //            memberExpression = memberBinding;
            //        }
            //        else if (memberAccess.SpanStart > memberBinding.SpanStart)
            //        {
            //            memberExpression = memberBinding;
            //        }
            //        else
            //        {
            //            memberExpression = memberAccess;
            //        }
            //    }
            //    else
            //    {
            //        memberExpression = memberAccess;
            //    }
            //}
            //else
            //{
            //    memberExpression = memberBinding;
            //}

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

        SyntaxNode ChooseMemberExpression(MemberAccessExpressionSyntax memberAccess, MemberBindingExpressionSyntax memberBinding)
        {
            if (memberAccess != null)
            {
                if (memberBinding != null)
                {
                    if (memberAccess.Contains(memberBinding))
                    {
                        return memberAccess;
                    }
                    else if (memberBinding.Contains(memberAccess))
                    {
                        return memberBinding;
                    }
                    else if (memberAccess.SpanStart > memberBinding.SpanStart)
                    {
                        return memberBinding;
                    }
                    else
                    {
                        return memberAccess;
                    }
                }
                else
                {
                    return memberAccess;
                }
            }
            else
            {
                return memberBinding;
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

