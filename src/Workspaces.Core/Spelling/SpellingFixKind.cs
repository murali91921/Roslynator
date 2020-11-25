// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Roslynator.Spelling
{
    internal enum SpellingFixKind
    {
        None = 0,
        List = 1,
        Split = 2,
        Swap = 3,
        Fuzzy = 4,
        User = 5,
    }
}
