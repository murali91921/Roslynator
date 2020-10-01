// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.Formatting.CodeFixes.CSharp;
using Xunit;

namespace Roslynator.Formatting.CSharp.Tests
{
    public class RCS0054FixFormattingOfMethodChainTests : AbstractCSharpFixVerifier
    {
        public override DiagnosticDescriptor Descriptor { get; } = DiagnosticDescriptors.FixFormattingOfMethodChain;

        public override DiagnosticAnalyzer Analyzer { get; } = new FixFormattingOfMethodChainAnalyzer();

        public override CodeFixProvider FixProvider { get; } = new FixFormattingOfMethodChainCodeFixProvider();

        [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixFormattingOfMethodChain)]
        public async Task Test_Invocation()
        {
            await VerifyDiagnosticAndFixAsync(@"
class C
{
    C M() 
    {
        var x = new C();

        return [|x.M()
        .M().M()|];
    }
}
", @"
class C
{
    C M() 
    {
        var x = new C();

        return x.M()
            .M()
            .M();
    }
}
");
        }
    }
}
