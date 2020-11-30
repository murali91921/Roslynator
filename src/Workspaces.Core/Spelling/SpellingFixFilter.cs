// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Roslynator.Spelling
{
    [Flags]
    internal enum SpellingFixFilter
    {
        None = 0,
        DocumentationComment = 1,
        NonDocumentationComment = 1 << 1,
        Comment = DocumentationComment | NonDocumentationComment,
        Namespace = 1 << 2,
        Parameter = 1 << 3,
        LocalVariable = 1 << 4,
        UsingAlias = 1 << 5,
        Method = 1 << 6,
        LocalFunction = 1 << 7,
        Local = LocalVariable | LocalFunction,
        Property = 1 << 8,
        Indexer = 1 << 9,
        Field = 1 << 10,
        Event = 1 << 11,
        Constant = 1 << 12,
        Member = Method | LocalFunction | Property | Indexer | Field | Event | Constant,
        Class = 1 << 13,
        Struct = 1 << 14,
        Delegate = 1 << 15,
        Interface = 1 << 16,
        Type = Class | Struct | Delegate | Interface,
        Symbol = Namespace | Parameter | LocalVariable | UsingAlias | Member | Type,
        All = int.MaxValue,
    }
}
