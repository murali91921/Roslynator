// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.Formatting.CodeFixes.CSharp;
using Xunit;

namespace Roslynator.Formatting.CSharp.Tests
{
    public class RCS0052FixFormattingOfListTests : AbstractCSharpFixVerifier
    {
        public override DiagnosticDescriptor Descriptor { get; } = DiagnosticDescriptors.FixFormattingOfList;

        public override DiagnosticAnalyzer Analyzer { get; } = new FixFormattingOfListAnalyzer();

        public override CodeFixProvider FixProvider { get; } = new FixFormattingOfListCodeFixProvider();

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task Test_Singleline_AlignedToParenthesis()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    void M(
           [|object p1, object p2|]) 
    {
    }
}
", @"
class C
{
    void M(
        object p1, object p2) 
    {
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task Test_Singleline_NoIndentation()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    void M(
[|object p1, object p2|]) 
    {
    }
}
", @"
class C
{
    void M(
        object p1, object p2) 
    {
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task Test_Singleline_EmptyLine()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    void M(

[|object p1, object p2|]) 
    {
    }
}
", @"
class C
{
    void M(

        object p1, object p2) 
    {
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task Test_Singleline_Comment()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    void M( // x
    [|object p1, object p2|]) 
    {
    }
}
", @"
class C
{
    void M( // x
        object p1, object p2) 
    {
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task Test_Singleline_EmptyLine_Comment()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    void M(
// x
[|object p1, object p2|]) 
    {
    }
}
", @"
class C
{
    void M(
// x
        object p1, object p2) 
    {
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task Test_Multiline_AlignedToParenthesis()
        {
            await VerifyDiagnosticAndFixAsync(@"
using System;

class C
{
    void M()
    {
    }

    [Obsolete]
    void M2(
            [|object p1,
            object p2|]) 
    {
    }
}
", @"
using System;

class C
{
    void M()
    {
    }

    [Obsolete]
    void M2(
        object p1,
        object p2) 
    {
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task Test_Multiline_AlignedToParenthesis_WhitespaceAfterParenthesis()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    void M( [|object p1,
           object p2|]) 
    {
    }
}
", @"
class C
{
    void M(
        object p1,
        object p2) 
    {
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task Test_Multiline_FirstParameterNotWrapped()
        {
            await VerifyDiagnosticAndFixAsync(@"
namespace N
{
    class C
    {
        void M([|object p1,
            object p2|]) 
        {
        }
    }
}
", @"
namespace N
{
    class C
    {
        void M(
            object p1,
            object p2) 
        {
        }
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task Test_Multiline_TwoParametersOnSameLine()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    void M(
        [|object p1,
        object p2,object p3|]) 
    {
    }
}
", @"
class C
{
    void M(
        object p1,
        object p2,
        object p3) 
    {
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task Test_Multiline_TwoParametersOnSameLine2()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    void M(
        [|object p1,
        object p2, object p3,
        object p4|]) 
    {
    }
}
", @"
class C
{
    void M(
        object p1,
        object p2,
        object p3,
        object p4) 
    {
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task Test_Multiline_Comment()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    void M( // x
 // x
    [|object p1, // xx
    object p2|])
    {
    }
}
", @"
class C
{
    void M( // x
 // x
        object p1, // xx
        object p2)
    {
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task TestNoDiagnostic_Singleline()
        {
            await VerifyNoDiagnosticAsync(@"
class C
{
    void M(object p1, object p2, object p3) 
    {
    }

    void M2(
        object p1, object p2, object p3) 
    {
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task TestNoDiagnostic_Multiline()
        {
            await VerifyNoDiagnosticAsync(@"
class C
{
    void M()
    {
    }

    void M2(
        object p1,
        object p2,
        object p3) 
    {
    }
}
");
        }

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfList)]
        public async Task TestNoDiagnostic_Multiline2()
        {
            await VerifyNoDiagnosticAsync(@"
class C
{
void M(
    object p1,
    object p2,
    object p3) 
{
}
}
");
        }
    }
}
