// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Roslynator.Spelling
{
    [Flags]
    internal enum SpellingScopeFilter
    {
        None = 0,
        DocumentationComment = 1,
        NonDocumentationComment = 1 << 1,
        Comment = DocumentationComment | NonDocumentationComment,
        RegionDirective = 1 << 2,
        Namespace = 1 << 3,
        Parameter = 1 << 4,
        LocalVariable = 1 << 5,
        UsingAlias = 1 << 6,
        Method = 1 << 7,
        LocalFunction = 1 << 8,
        Local = LocalVariable | LocalFunction,
        Property = 1 << 9,
        Indexer = 1 << 10,
        Field = 1 << 11,
        Event = 1 << 12,
        Constant = 1 << 13,
        Member = Method | LocalFunction | Property | Indexer | Field | Event | Constant,
        Class = 1 << 14,
        Struct = 1 << 15,
        Delegate = 1 << 16,
        Interface = 1 << 17,
        Type = Class | Struct | Delegate | Interface,
        Symbol = Namespace | Parameter | LocalVariable | UsingAlias | Member | Type,
        All = int.MaxValue,
    }
}
