﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslynator.RegularExpressions;
using Roslynator.Spelling;

namespace Roslynator.CSharp.Spelling
{
    //TODO: decode html entity?
    //TODO: parse email address
    internal class CSharpSpellingWalker : CSharpSyntaxWalker
    {
        private static readonly Regex _splitIdentifierRegex = new Regex(
            @"
    \P{L}+
|
    (?<=\p{Lu})(?=\p{Lu}\p{Ll})
|
    (?<=\p{Ll})(?=\p{Lu})
",
            RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _splitCommentWordRegex = new Regex(
            @"
    (?<=\p{Lu})(?=\p{Lu}\p{Ll})
|
    (?<=\p{Ll})(?=\p{Lu})
",
            RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _wordInComment = new Regex(
            @"
\b
\p{L}{2,}
(
    (?='s\b)
|
    ('(d|ll|m|re|t|ve)\b)
|
    \b
)",
            RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _identifierToSkipRegex = new Regex(@"\A_*\p{Ll}{1,3}\d*\z");

        private static readonly Regex _simpleIdentifierToSkipRegex = new Regex(
            @"(?<=\A_*)(\p{Ll}{3,}|\p{Lu}\p{Ll}{2,})(?=\d*\z)");

        private static readonly Regex _typeParameterLowercaseRegex = new Regex(@"(?<=\At)\p{Ll}{3,}\z");

        private static readonly Regex _urlRegex = new Regex(
            @"\bhttps?://[^\s]+(?=\s|\z)", RegexOptions.IgnoreCase);

        public SpellingData SpellingData { get; }

        public SpellingFixerOptions Options { get; }

        public CancellationToken CancellationToken { get; }

        public List<SpellingError> Errors { get; private set; }

        public CSharpSpellingWalker(SpellingData spellingData, SpellingFixerOptions options, CancellationToken cancellationToken)
            : base(SyntaxWalkerDepth.StructuredTrivia)
        {
            SpellingData = spellingData;
            Options = options;
            CancellationToken = cancellationToken;
        }

        private void AnalyzeComment(string value, SyntaxTree syntaxTree, TextSpan textSpan)
        {
            int prevEnd = 0;

            Match match = _urlRegex.Match(value, prevEnd);

            while (match.Success)
            {
                AnalyzeComment(value, prevEnd, match.Index - prevEnd, textSpan, syntaxTree);

                prevEnd = match.Index + match.Length;

                match = match.NextMatch();
            }

            AnalyzeComment(value, prevEnd, value.Length - prevEnd, textSpan, syntaxTree);
        }

        private void AnalyzeComment(
            string value,
            int startIndex,
            int length,
            TextSpan textSpan,
            SyntaxTree syntaxTree)
        {
            Match match = _wordInComment.Match(value, startIndex, length);

            while (match.Success)
            {
                if (match.Length > 2)
                {
                    foreach (SplitItem splitItem in SplitItemCollection.Create(_splitCommentWordRegex, match.Value))
                    {
                        AnalyzeValue(
                            splitItem.Value,
                            match.Value,
                            splitItem.Index,
                            new TextSpan(textSpan.Start + match.Index + splitItem.Index, splitItem.Value.Length),
                            default(SyntaxToken),
                            syntaxTree,
                            isSimpleIdentifier: false);
                    }
                }

                match = match.NextMatch();
            }
        }

        private void AnalyzeIdentifier(
            SyntaxToken identifier,
            int prefixLength = 0)
        {
            string value = identifier.ValueText;

            if (value.Length < 3)
                return;

            if (prefixLength > 0)
            {
                if (SpellingData.IgnoreList.Contains(value))
                    return;

                if (SpellingData.List.Contains(value))
                    return;
            }

            string value2 = (prefixLength > 0) ? value.Substring(prefixLength) : value;

            SplitItemCollection splitItems = SplitItemCollection.Create(_splitIdentifierRegex, value2);

            if (splitItems.Count > 1)
            {
                if (SpellingData.IgnoreList.Contains(value2))
                    return;

                if (SpellingData.List.Contains(value2))
                    return;
            }

            foreach (SplitItem splitItem in splitItems)
            {
                Debug.Assert(splitItem.Value.All(f => char.IsLetter(f)), splitItem.Value);

                if (AnalyzeValue(
                    splitItem.Value,
                    value,
                    splitItem.Index,
                    new TextSpan(identifier.SpanStart + splitItem.Index + prefixLength, splitItem.Length),
                    identifier,
                    identifier.SyntaxTree,
                    isSimpleIdentifier: _simpleIdentifierToSkipRegex.IsMatch(value2)))
                {
                    break;
                }
            }
        }

        private bool AnalyzeValue(
            string value,
            string containingValue,
            int index,
            TextSpan textSpan,
            SyntaxToken identifier,
            SyntaxTree syntaxTree,
            bool isSimpleIdentifier)
        {
            if (value.Length < 3)
                return false;

            if (IsAllowedNonsensicalWord(value))
                return false;

            if (SpellingData.IgnoreList.Contains(value))
                return false;

            if (SpellingData.List.Contains(value))
                return false;

            if (isSimpleIdentifier
                && identifier.Parent != null
                && IsLocalOrParameterOrField(identifier.Parent))
            {
                Match match = _typeParameterLowercaseRegex.Match(value);

                if (match.Success
                    && SpellingData.List.Contains(match.Value))
                {
                    return false;
                }
            }

            var spellingError = new SpellingError(
                value,
                containingValue,
                Location.Create(syntaxTree, textSpan),
                index,
                identifier);

            (Errors ??= new List<SpellingError>()).Add(spellingError);

            return true;
        }

        public override void VisitTrivia(SyntaxTrivia trivia)
        {
            switch (trivia.Kind())
            {
                case SyntaxKind.SingleLineCommentTrivia:
                case SyntaxKind.MultiLineCommentTrivia:
                    {
                        AnalyzeComment(trivia.ToString(), trivia.SyntaxTree, trivia.Span);
                        break;
                    }
                case SyntaxKind.SingleLineDocumentationCommentTrivia:
                case SyntaxKind.MultiLineDocumentationCommentTrivia:
                case SyntaxKind.RegionDirectiveTrivia:
                case SyntaxKind.EndRegionDirectiveTrivia:
                    {
                        base.VisitTrivia(trivia);
                        break;
                    }
                case SyntaxKind.PreprocessingMessageTrivia:
                    {
                        AnalyzeComment(trivia.ToString(), trivia.SyntaxTree, trivia.Span);
                        break;
                    }
            }
        }

        public override void VisitTupleType(TupleTypeSyntax node)
        {
            if (!Options.IncludeLocal
                && node.IsParentKind(SyntaxKind.VariableDeclaration)
                && node.Parent.IsParentKind(SyntaxKind.LocalDeclarationStatement, SyntaxKind.UsingStatement))
            {
                return;
            }

            base.VisitTupleType(node);
        }

        public override void VisitTupleElement(TupleElementSyntax node)
        {
            if (node.Identifier.Parent != null)
                AnalyzeIdentifier(node.Identifier);
        }

        public override void VisitArgument(ArgumentSyntax node)
        {
            if (node.Expression is DeclarationExpressionSyntax declarationExpression)
                VisitDeclarationExpression(declarationExpression);
        }

        public override void VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
        {
            if (node.NameEquals != null)
                AnalyzeIdentifier(node.NameEquals.Name.Identifier);
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            AnalyzeIdentifier(node.Identifier);
            base.VisitLocalFunctionStatement(node);
        }

        public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            if (ShouldAnalyze(node))
                AnalyzeIdentifier(node.Identifier);

            base.VisitVariableDeclarator(node);

            bool ShouldAnalyze(VariableDeclaratorSyntax node)
            {
                Debug.Assert(node.IsParentKind(SyntaxKind.VariableDeclaration), node.Parent.Kind().ToString());

                if (node.IsParentKind(SyntaxKind.VariableDeclaration))
                {
                    Debug.Assert(
                        node.Parent.IsParentKind(
                            SyntaxKind.LocalDeclarationStatement,
                            SyntaxKind.UsingStatement,
                            SyntaxKind.ForStatement,
                            SyntaxKind.FixedStatement,
                            SyntaxKind.FieldDeclaration,
                            SyntaxKind.EventFieldDeclaration),
                        node.Parent.Parent.Kind().ToString());

                    switch (node.Parent.Parent.Kind())
                    {
                        case SyntaxKind.LocalDeclarationStatement:
                        case SyntaxKind.UsingStatement:
                        case SyntaxKind.ForStatement:
                        case SyntaxKind.FixedStatement:
                            {
                                if (!Options.IncludeLocal)
                                    return false;

                                if (ShouldBeSkipped(node.Identifier.ValueText))
                                    return false;

                                break;
                            }
                        case SyntaxKind.FieldDeclaration:
                        case SyntaxKind.EventFieldDeclaration:
                            {
                                if (ShouldBeSkipped(node.Identifier.ValueText))
                                    return false;

                                break;
                            }
                    }
                }

                return true;
            }
        }

        public override void VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
        {
            if (Options.IncludeLocal)
                AnalyzeIdentifier(node.Identifier);

            base.VisitSingleVariableDesignation(node);
        }

        public override void VisitCatchDeclaration(CatchDeclarationSyntax node)
        {
            if (Options.IncludeLocal
                && !ShouldBeSkipped(node.Identifier.ValueText))
            {
                AnalyzeIdentifier(node.Identifier);
            }

            base.VisitCatchDeclaration(node);
        }

        public override void VisitExternAliasDirective(ExternAliasDirectiveSyntax node)
        {
            AnalyzeIdentifier(node.Identifier);
            base.VisitExternAliasDirective(node);
        }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            if (node.Alias != null)
                AnalyzeIdentifier(node.Alias.Name.Identifier);
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            VisitName(node.Name);
            base.VisitNamespaceDeclaration(node);
        }

        private void VisitName(NameSyntax node)
        {
            switch (node)
            {
                case IdentifierNameSyntax identifierName:
                    {
                        AnalyzeIdentifier(identifierName.Identifier);
                        break;
                    }
                case QualifiedNameSyntax qualifiedName:
                    {
                        VisitName(qualifiedName.Left);

                        if (qualifiedName.Right is IdentifierNameSyntax identifierName)
                            AnalyzeIdentifier(identifierName.Identifier);

                        break;
                    }
            }
        }

        public override void VisitTypeParameter(TypeParameterSyntax node)
        {
            SyntaxToken identifier = node.Identifier;
            string value = identifier.ValueText;

            int prefixLength = 0;
            if (value.Length > 1
                && value[0] == 'T'
                && char.IsUpper(value[1]))
            {
                prefixLength = 1;
            }

            AnalyzeIdentifier(identifier, prefixLength: prefixLength);
            base.VisitTypeParameter(node);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            AnalyzeIdentifier(node.Identifier);
            base.VisitClassDeclaration(node);
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            AnalyzeIdentifier(node.Identifier);
            base.VisitStructDeclaration(node);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            SyntaxToken identifier = node.Identifier;
            string value = identifier.ValueText;

            int prefixLength = 0;
            if (value.Length > 1
                && value[0] == 'I'
                && char.IsUpper(value[1]))
            {
                prefixLength = 1;
            }

            AnalyzeIdentifier(identifier, prefixLength: prefixLength);
            base.VisitInterfaceDeclaration(node);
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            AnalyzeIdentifier(node.Identifier);
            base.VisitEnumDeclaration(node);
        }

        public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            AnalyzeIdentifier(node.Identifier);
            base.VisitDelegateDeclaration(node);
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            AnalyzeIdentifier(node.Identifier);
            base.VisitEnumMemberDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            AnalyzeIdentifier(node.Identifier);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            AnalyzeIdentifier(node.Identifier);
            base.VisitPropertyDeclaration(node);
        }

        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            AnalyzeIdentifier(node.Identifier);
            base.VisitEventDeclaration(node);
        }

        public override void VisitParameter(ParameterSyntax node)
        {
            if (!ShouldBeSkipped(node.Identifier.ValueText))
                AnalyzeIdentifier(node.Identifier);

            base.VisitParameter(node);
        }

        public override void VisitXmlText(XmlTextSyntax node)
        {
            foreach (SyntaxToken token in node.TextTokens)
            {
                if (token.IsKind(SyntaxKind.XmlTextLiteralToken))
                    AnalyzeComment(token.ValueText, node.SyntaxTree, token.Span);
            }
        }

        private bool ShouldBeSkipped(string s)
        {
            return _identifierToSkipRegex.IsMatch(s);
        }

        private static bool IsLocalOrParameterOrField(SyntaxNode node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.CatchDeclaration:
                case SyntaxKind.SingleVariableDesignation:
                    {
                        return true;
                    }
                case SyntaxKind.VariableDeclarator:
                    {
                        return node.IsParentKind(SyntaxKind.VariableDeclaration)
                            && node.Parent.IsParentKind(
                                SyntaxKind.LocalDeclarationStatement,
                                SyntaxKind.UsingStatement,
                                SyntaxKind.ForStatement,
                                SyntaxKind.FixedStatement,
                                SyntaxKind.FieldDeclaration,
                                SyntaxKind.EventFieldDeclaration);
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        private bool IsAllowedNonsensicalWord(string value)
        {
            if (value.Length < 3)
                return false;

            if (IsSequence())
                return true;

            char ch = value[0];
            int i = 1;

            // aaa
            while (i < value.Length)
            {
                if (value[i] != ch)
                    return IsSequence2();

                i++;
            }

            return true;

            // abc, Abc, ABC
            bool IsSequence()
            {
                int num = 0;

                if (value[0] == 'a')
                {
                    if (value[1] == 'b')
                    {
                        num = 'c';
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (value[0] == 'A')
                {
                    if (value[1] == 'B')
                    {
                        num = 'C';
                    }
                    else if (value[1] == 'b')
                    {
                        num = 'c';
                    }
                    else
                    {
                        return false;
                    }
                }

                for (int i = 2; i < value.Length; i++)
                {
                    if (value[i] != num)
                        return false;

                    num++;
                }

                return true;
            }

            // aabbcc
            bool IsSequence2()
            {
                if (i > 1
                    && (ch == 'a' || ch == 'A')
                    && value.Length >= 6
                    && value.Length % i == 0)
                {
                    int length = i;
                    int count = value.Length / i;

                    for (int j = 0; j < count - 1; j++)
                    {
                        var ch2 = (char)(ch + j + 1);

                        int start = i + (j * length);
                        int end = start + length;

                        for (int k = i + (j * length); k < end; k++)
                        {
                            if (ch2 != value[k])
                                return false;
                        }
                    }

                    return true;
                }

                return false;
            }
        }
    }
}
