// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;

namespace Roslynator.Spelling
{
    internal class SpellingData
    {
        public SpellingData(ImmutableHashSet<string> dictionary)
        {
            Dictionary = dictionary ?? throw new System.ArgumentNullException(nameof(dictionary));
        }

        public ImmutableHashSet<string> Dictionary { get; }
    }
}
