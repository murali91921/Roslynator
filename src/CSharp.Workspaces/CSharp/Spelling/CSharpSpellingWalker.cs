// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslynator.RegularExpressions;
using Roslynator.Spelling;
using System.Diagnostics;

namespace Roslynator.CSharp.Spelling
{
    internal class CSharpSpellingWalker : CSharpSyntaxWalker
    {
        private static readonly Regex _splitRegex = new Regex(
            @"
    \P{L}+
|
    (?<=\p{Lu})(?=\p{Lu}\p{Ll})
|
    (?<=\p{Ll})(?=\p{Lu})
",
            RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _identifierToSkipRegex = new Regex(@"\A_*\p{Ll}{1,3}\d*\z");

        private static readonly Regex _simpleIdentifierToSkipRegex = new Regex(@"(?<=\A_*)(\p{Ll}{3,}|\p{Lu}\p{Ll}{2,})(?=\d*\z)");

        private static readonly Regex _typeParameterLowercaseRegex = new Regex(@"(?<=\At)\p{Ll}{3,}\z");

        public SpellingData SpellingData { get; }

        public List<SpellingError> Errors { get; private set; }

        public SpellingAnalysisOptions Options { get; }

        public CancellationToken CancellationToken { get; }

        public CSharpSpellingWalker(SpellingData spellingData, SpellingAnalysisOptions options, CancellationToken cancellationToken)
            : base(SyntaxWalkerDepth.StructuredTrivia)
        {
            SpellingData = spellingData;
            Options = options;
            CancellationToken = cancellationToken;
        }

        private void CheckIdentifier(SyntaxToken identifier)
        {
            CheckValue(identifier.ValueText, identifier.SyntaxTree, identifier.Span, identifier);
        }

        private void CheckTrivia(SyntaxTrivia trivia)
        {
            CheckValue(trivia.ToString(), trivia.SyntaxTree, trivia.Span, default);
        }

        private void CheckValue(string value, SyntaxTree syntaxTree, TextSpan textSpan, SyntaxToken identifier)
        {
            CheckValue(value, null, syntaxTree, textSpan, identifier);
        }

        private void CheckValue(string value, string originalValue, SyntaxTree syntaxTree, TextSpan textSpan, SyntaxToken identifier)
        {
            if (identifier.Parent != null)
            {
                if (value.Length <= 2)
                    return;

                if (SpellingData.IgnoreList.Contains(originalValue ?? value))
                    return;
            }

            foreach (SplitItem splitItem in SplitItemCollection.Create(_splitRegex, value))
            {
                if (CheckValue(
                    splitItem,
                    syntaxTree,
                    textSpan,
                    identifier,
                    isSimpleIdentifier: _simpleIdentifierToSkipRegex.IsMatch(value))
                    && identifier.Parent != null)
                {
                    break;
                }
            }
        }

        private bool CheckValue(
            SplitItem splitItem,
            SyntaxTree syntaxTree,
            TextSpan textSpan,
            SyntaxToken identifier,
            bool isSimpleIdentifier)
        {
            string value = splitItem.Value;

            if (value.Length <= 1)
                return false;

            if (value.All(f => char.IsDigit(f)))
                return false;

            if (value.All(f => char.IsUpper(f)))
                return false;

            if (SpellingData.IgnoreList.Contains(value))
                return false;

            if (SpellingData.Dictionary.Contains(value))
                return false;

            if (isSimpleIdentifier
                && identifier.Parent != null
                && IsLocalOrParameterOrField(identifier.Parent))
            {
                Match match = _typeParameterLowercaseRegex.Match(value);

                if (match.Success
                    && SpellingData.Dictionary.Contains(match.Value))
                {
                    return false;
                }
            }
#if DEBUG
            switch (value)
            {
                case "":
                    {
                        break;
                    }
            }
#endif
            Debug.WriteLine(value);

            SpellingError spellingError;
            if (identifier.Parent != null)
            {
                spellingError = new SpellingError(identifier.ValueText, identifier.GetLocation(), identifier);
            }
            else
            {
                spellingError = new SpellingError(value, Location.Create(syntaxTree, new TextSpan(textSpan.Start + splitItem.Index, splitItem.Length)));
            }

            Debug.Assert(identifier.Parent == null || identifier.ValueText.Length > 2, identifier.ValueText);

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
                        CheckTrivia(trivia);
                        break;
                    }
            }
        }

        public override void VisitTupleElement(TupleElementSyntax node)
        {
            CheckIdentifier(node.Identifier);
        }

        public override void VisitArgument(ArgumentSyntax node)
        {
            if (node.Expression is DeclarationExpressionSyntax declarationExpression)
                VisitDeclarationExpression(declarationExpression);
        }

        public override void VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
        {
            CheckIdentifier(node.NameEquals.Name.Identifier);
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            CheckIdentifier(node.Identifier);
            base.VisitLocalFunctionStatement(node);
        }

        public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            if (node.IsParentKind(SyntaxKind.VariableDeclaration)
                && node.Parent.IsParentKind(SyntaxKind.LocalDeclarationStatement, SyntaxKind.UsingStatement, SyntaxKind.FieldDeclaration))
            {
                string s = node.Identifier.ValueText;

                if (ShouldBeSkipped(s))
                    return;
            }

            CheckIdentifier(node.Identifier);
            base.VisitVariableDeclarator(node);
        }

        public override void VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
        {
            CheckIdentifier(node.Identifier);
            base.VisitSingleVariableDesignation(node);
        }

        public override void VisitCatchDeclaration(CatchDeclarationSyntax node)
        {
            string s = node.Identifier.ValueText;

            if (ShouldBeSkipped(s))
                return;

            CheckIdentifier(node.Identifier);
            base.VisitCatchDeclaration(node);
        }

        public override void VisitExternAliasDirective(ExternAliasDirectiveSyntax node)
        {
            CheckIdentifier(node.Identifier);
            base.VisitExternAliasDirective(node);
        }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            if (node.Alias != null)
                CheckIdentifier(node.Alias.Name.Identifier);
        }

        private void VisitName(NameSyntax node)
        {
            switch (node)
            {
                case IdentifierNameSyntax identifierName:
                    {
                        CheckIdentifier(identifierName.Identifier);
                        break;
                    }
                case QualifiedNameSyntax qualifiedName:
                    {
                        VisitName(qualifiedName.Left);

                        if (qualifiedName.Right is IdentifierNameSyntax identifierName)
                            CheckIdentifier(identifierName.Identifier);

                        break;
                    }
            }
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            VisitName(node.Name);
            base.VisitNamespaceDeclaration(node);
        }

        public override void VisitTypeParameter(TypeParameterSyntax node)
        {
            SyntaxToken identifier = node.Identifier;

            string origValue = identifier.ValueText;
            string value = origValue;

            if (value.Length > 1)
            {
                if (value[0] == 'T'
                    && char.IsUpper(value[1]))
                {
                    value = value.Substring(1);
                }

                CheckValue(value, origValue, identifier.SyntaxTree, identifier.Span, identifier);
            }

            base.VisitTypeParameter(node);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            CheckIdentifier(node.Identifier);
            base.VisitClassDeclaration(node);
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            CheckIdentifier(node.Identifier);
            base.VisitStructDeclaration(node);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            SyntaxToken identifier = node.Identifier;

            string origValue = identifier.ValueText;
            string value = origValue;

            if (value.Length > 1)
            {
                if (value[0] == 'I'
                    && char.IsUpper(value[1]))
                {
                    value = value.Substring(1);
                }

                CheckValue(value, origValue, identifier.SyntaxTree, identifier.Span, identifier);
            }

            base.VisitInterfaceDeclaration(node);
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            CheckIdentifier(node.Identifier);
            base.VisitEnumDeclaration(node);
        }

        public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            CheckIdentifier(node.Identifier);
            base.VisitDelegateDeclaration(node);
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            CheckIdentifier(node.Identifier);
            base.VisitEnumMemberDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            CheckIdentifier(node.Identifier);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            CheckIdentifier(node.Identifier);
            base.VisitPropertyDeclaration(node);
        }

        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            CheckIdentifier(node.Identifier);
            base.VisitEventDeclaration(node);
        }

        public override void VisitParameter(ParameterSyntax node)
        {
            string s = node.Identifier.ValueText;

            if (ShouldBeSkipped(s))
                return;

            CheckValue(s, node.SyntaxTree, node.Span, node.Identifier);
            base.VisitParameter(node);
        }

        public override void VisitXmlText(XmlTextSyntax node)
        {
            foreach (SyntaxToken token in node.TextTokens)
            {
                if (token.IsKind(SyntaxKind.XmlTextLiteralToken))
                    CheckValue(token.ValueText, node.SyntaxTree, token.Span, default);
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
                                SyntaxKind.FieldDeclaration);
                    }
                default:
                    {
                        return false;
                    }
            }
        }
    }
}
