// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Roslynator.CSharp
{
    internal static class DebugHelpers
    {
        public static void Fail(SyntaxNode node)
        {
            Debug.Fail($"{node.Kind().ToString()}\r\n{node.SyntaxTree.GetLineSpan(node.Span)}");
        }
    }
}
