// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.Formatting.CodeFixes;
using Xunit;

#pragma warning disable RCS0052

namespace Roslynator.Formatting.CSharp.Tests
{
    public class RCS0052LineIsTooLongTests : AbstractCSharpFixVerifier
    {
        public override DiagnosticDescriptor Descriptor { get; } = DiagnosticDescriptors.LineIsTooLong;

        public override DiagnosticAnalyzer Analyzer { get; } = new LineIsTooLongAnalyzer();

        public override CodeFixProvider FixProvider { get; } = new LineIsTooLongCodeFixProvider();

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task Test_ExpressionBody_AddNewLineBeforeArrow()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
[|    string M(object pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp) => null;|]
}
",
@"
class C
{
    string M(object pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp)
        => null;
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task Test_ExpressionBody_AddNewLineAfterArrow()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
[|    string M(object ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp) => null;|]
}
",
@"
class C
{
    string M(object ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp) =>
        null;
}
", options: Options.WithEnabled(DiagnosticDescriptors.AddNewLineBeforeExpressionBodyArrowInsteadOfAfterItOrViceVersa, AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt));
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task TestNoDiagnostic_ExpressionBody_TooLongAfterWrapping()
        {
            await VerifyNoDiagnosticAsync(@"
class C
{
    string M(object pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp) => null;
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task TestNoDiagnostic_ExpressionBody_TooLongAfterWrapping2()
        {
            await VerifyNoDiagnosticAsync(@"
class C
{
    string M(object ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp) => null;
}
", options: Options.WithEnabled(DiagnosticDescriptors.AddNewLineBeforeExpressionBodyArrowInsteadOfAfterItOrViceVersa, AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt));
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task TestNoDiagnostic_ExpressionBody_AlreadyWrapped()
        {
            await VerifyNoDiagnosticAsync(
@"
class C
{
    string M(object p)
        => ""xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"";
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task TestNoDiagnostic_ExpressionBody_AlreadyWrapped2()
        {
            await VerifyNoDiagnosticAsync(@"
class C
{
    string M(object p) =>
        ""xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"";
}
");
        }
    }
}
