// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslynator.Spelling;

namespace Roslynator.CSharp.Spelling
{
    internal class CSharpSpellingWalker : CSharpSyntaxWalker
    {
        private static readonly Regex _splitRegex = new Regex(
            @"
(
    _+
|
    \d+
|
    (?<=\p{Lu})(?=\p{Lu}\p{Ll})
|
    (?<=\p{Ll})(?=\p{Lu})
)
",
            RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _identifierToSkipRegex = new Regex(@"\A_*\p{Ll}{2,3}\d*\z");

        public SpellingData SpellingData { get; }

        public List<SpellingError> Errors { get; private set; }

        public SpellingAnalysisOptions Options { get; }

        public CancellationToken CancellationToken { get; }

        public CSharpSpellingWalker(SpellingData spellingData, SpellingAnalysisOptions options, CancellationToken cancellationToken)
            : base(SyntaxWalkerDepth.Trivia)
        {
            SpellingData = spellingData;
            Options = options;
            CancellationToken = cancellationToken;
        }

        private void CheckIdentifier(SyntaxToken identifier)
        {
            CheckValue(identifier.ValueText, identifier.SyntaxTree, identifier.Span);
        }

        private void CheckTrivia(SyntaxTrivia trivia)
        {
            CheckValue(trivia.ToString(), trivia.SyntaxTree, trivia.Span);
        }

        private void CheckValue(string text, SyntaxTree syntaxTree, TextSpan textSpan)
        {
            foreach (string value in _splitRegex.Split(text))
            {
                if (value.Length <= 1)
                    continue;

                if (value.All(f => char.IsDigit(f)))
                    continue;

                if (value.All(f => char.IsUpper(f)))
                    continue;

                if (value[0] == 'T'
                    && char.IsUpper(value[1]))
                {
                }

                switch (value)
                {
                    case "Nul":
                    case "Sln":
                        {
                            break;
                        }
                }

                if (!SpellingData.Dictionary.Contains(value))
                {
                    if (value.Length == 2
                        || value.Length == 3)
                    {
                    }

                    System.Diagnostics.Debug.WriteLine(value);

                    (Errors ??= new List<SpellingError>()).Add(
                        new SpellingError(value, Location.Create(syntaxTree, textSpan)));
                }
            }
        }

        public override void VisitTrivia(SyntaxTrivia trivia)
        {
            switch (trivia.Kind())
            {
                case SyntaxKind.SingleLineCommentTrivia:
                case SyntaxKind.MultiLineCommentTrivia:
                    {
                        //TODO: analyze comment
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
            string s = node.Identifier.ValueText;

            if (s.Length > 1)
            {
                if (s[0] == 'T'
                    && char.IsUpper(s[1]))
                {
                    s = s.Substring(1);
                }

                CheckValue(s, node.SyntaxTree, node.Span);
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
            string s = node.Identifier.ValueText;

            if (s.Length > 1)
            {
                if (s[0] == 'I'
                    && char.IsUpper(s[1]))
                {
                    s = s.Substring(1);
                }

                CheckValue(s, node.SyntaxTree, node.Span);
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

            CheckValue(s, node.SyntaxTree, node.Span);
            base.VisitParameter(node);
        }

        public override void VisitXmlText(XmlTextSyntax node)
        {
            //TODO: VisitXmlText
        }

        private bool ShouldBeSkipped(string s)
        {
            return _identifierToSkipRegex.IsMatch(s);
        }
    }
}
