// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.Formatting.CodeFixes;
using Xunit;

namespace Roslynator.Formatting.CSharp.Tests
{
    public class RCS0056LineIsTooLongTests : AbstractCSharpFixVerifier
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
[|    string M(object ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp) => null;|]
}
",
@"
class C
{
    string M(object ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp)
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
[|    string M(object pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp) => null;|]
}
",
@"
class C
{
    string M(object pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp) =>
        null;
}
", options: Options.WithEnabled(DiagnosticDescriptors.AddNewLineBeforeExpressionBodyArrowInsteadOfAfterItOrViceVersa, AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt));
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task Test_ParameterList()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
[|    void M(object x, object y, object z, object xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx)|]
    {
    }
}
",
@"
class C
{
    void M(
        object x,
        object y,
        object z,
        object xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx)
    {
    }
}
", options: Options.WithEnabled(DiagnosticDescriptors.AddNewLineBeforeExpressionBodyArrowInsteadOfAfterItOrViceVersa, AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt));
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task Test_ArgumentList()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    void M(
        object x,
        object y,
        object z,
        object xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx)
    {
[|        M(x, y, z, xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx);|]
    }
}
",
@"
class C
{
    void M(
        object x,
        object y,
        object z,
        object xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx)
    {
        M(
            x,
            y,
            z,
            xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx);
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task Test_PreferCallChainOverArgumentList()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    C M(
        object x,
        object y,
        object zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
    {
[|        return M(x, y, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz).M(x, y, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);|]
    }
}
",
@"
class C
{
    C M(
        object x,
        object y,
        object zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
    {
        return M(x, y, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
            .M(x, y, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task Test_PreferArgumentListOverCallChain()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    C M(
        string x,
        string yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy)
    {
[|        return M(x.ToString(), yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy.ToString().ToString().ToString());|]
    }
}
",
@"
class C
{
    C M(
        string x,
        string yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy)
    {
        return M(
            x.ToString(),
            yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy.ToString().ToString().ToString());
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task Test_PreferArgumentListOverCallChain_WhenLeftIsSimpleName()
        {
            await VerifyDiagnosticAndFixAsync(@"
using System.Collections.Generic;
using System.Linq;

class C
{
    void M()
    {
        var items = new List<string>();

[|        foreach ((string ffff, string gggg) item in items.Join(items, ffff => ffff, ffff => ffff, (ffff, gggg) => (ffff, gggg)))|]
        {
        }
    }
}
",
@"
using System.Collections.Generic;
using System.Linq;

class C
{
    void M()
    {
        var items = new List<string>();

        foreach ((string ffff, string gggg) item in items.Join(
            items,
            ffff => ffff,
            ffff => ffff,
            (ffff, gggg) => (ffff, gggg)))
        {
        }
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task Test_BinaryExpression()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    void M()
    {
        bool x = false;
        bool y = false;
        bool zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz = false;

[|        if (x && y && zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)|]
        {
        }
    }
}
",
@"
class C
{
    void M()
    {
        bool x = false;
        bool y = false;
        bool zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz = false;

        if (x && y
            && zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz)
        {
        }
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task Test_InitializerExpression()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    void M()
    {
        string sssssssssssssssssssssssssssss = null;
[|        var arr = new string[] { sssssssssssssssssssssssssssss, sssssssssssssssssssssssssssss, sssssssssssssssssssssssssssss };|]
    }
}
",
@"
class C
{
    void M()
    {
        string sssssssssssssssssssssssssssss = null;
        var arr = new string[]
        {
            sssssssssssssssssssssssssssss,
            sssssssssssssssssssssssssssss,
            sssssssssssssssssssssssssssss
        };
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task TestNoFix_ExpressionBody_TooLongAfterWrapping()
        {
            await VerifyDiagnosticAndNoFixAsync(@"
class C
{
[|    string Foooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo() => null;|]
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task TestNoFix_ExpressionBody_TooLongAfterWrapping2()
        {
            await VerifyDiagnosticAndNoFixAsync(@"
class C
{
[|    string Fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo() => null;|]
}
", options: Options.WithEnabled(DiagnosticDescriptors.AddNewLineBeforeExpressionBodyArrowInsteadOfAfterItOrViceVersa, AnalyzerOptions.AddNewLineAfterExpressionBodyArrowInsteadOfBeforeIt));
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task TestNoFix_ExpressionBody_AlreadyWrapped()
        {
            await VerifyDiagnosticAndNoFixAsync(
@"
class C
{
    string M(object p)
[|        => ""xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"";|]
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task TestNoFix_ExpressionBody_AlreadyWrapped2()
        {
            await VerifyDiagnosticAndNoFixAsync(@"
class C
{
    string M(object p) =>
[|        ""xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"";|]
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.LineIsTooLong)]
        public async Task TestNoDiagnostic_Banner()
        {
            await VerifyNoDiagnosticAsync(@"//xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

class C
{
}
");
        }
    }
}
