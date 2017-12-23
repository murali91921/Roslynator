﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable RCS1127

namespace Roslynator.CSharp.Refactorings.Tests
{
    internal static class JoinStringExpressions
    {
        public static void Foo(string s)
        {
            string s = "ab";

            s = "\"\r\n\\{}" + "\"\r\n\\{}";

            s = @"""\{}" + @"""\{}";

            s = @"""
\{}" + @"""
\{}";

            s = $"\"\r\n\\{{}}{s}" + $"\"\r\n\\{{}}{s}";

            s = $@"""
\{{}}{s}" + $@"""
\{{}}{s}";

            // n

            s = "x"
                + "x";

            s = "x" + @"x";

            s = @"x" + "x";

            s = "x" + $"{s}";

            s = $"{s}" + "x";

            s = "\"\r\n\\{}" + @"""
\{}";

            s = $"\"\r\n\\{{}}{s}" + $@"""
\{{}}{s}";

            s = "\"\r\n\\{}" + @"""
\{}" + $"\"\r\n\\{{}}{s}";

            s = "\"\r\n\\{}" + @"""
\{}" + $@"""
\{{}}{s}";
        }
    }
}
