// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;

namespace Roslynator.CSharp
{
    internal readonly struct IndentationChange
    {
        public IndentationChange(ImmutableArray<IndentationInfo> indentations, string replacement)
        {
            Indentations = indentations;
            Replacement = replacement;
        }

        public static IndentationChange Empty { get; } = new IndentationChange(ImmutableArray<IndentationInfo>.Empty, null);

        public ImmutableArray<IndentationInfo> Indentations { get; }

        public string Replacement { get; }

        public bool IsEmpty => Indentations.IsDefaultOrEmpty;
    }
}
