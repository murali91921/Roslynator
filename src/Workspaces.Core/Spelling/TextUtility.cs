// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Roslynator.Spelling
{
    internal static class TextUtility
    {
        public static string ReplaceRange(string s, string value, int index, int length)
        {
            int endIndex = index + length;

            return s.Remove(index)
                + value
                + s.Substring(endIndex, s.Length - endIndex);
        }

        public static string SetTextCasing(string s, TextCasing textCasing)
        {
            TextCasing textCasing2 = GetTextCasing(s);

            if (textCasing == textCasing2)
                return s;

            switch (textCasing)
            {
                case TextCasing.Lower:
                    return s.ToLowerInvariant();
                case TextCasing.Upper:
                    return s.ToUpperInvariant();
                case TextCasing.FirstUpper:
                    return s.Substring(0, 1).ToUpperInvariant() + s.Substring(1).ToLowerInvariant();
                default:
                    throw new InvalidOperationException($"Invalid enum value '{textCasing}'");
            }
        }

        public static TextCasing GetTextCasing(string s)
        {
            char ch = s[0];

            if (char.IsLower(ch))
            {
                for (int i = 1; i < s.Length; i++)
                {
                    if (!char.IsLower(s[i])
                        && !char.IsLetter(s[i]))
                    {
                        return TextCasing.Mixed;
                    }
                }

                return TextCasing.Lower;
            }
            else if (char.IsUpper(ch))
            {
                ch = s[1];

                if (char.IsLower(ch))
                {
                    for (int i = 2; i < s.Length; i++)
                    {
                        if (!char.IsLower(s[i])
                            && !char.IsLetter(s[i]))
                        {
                            return TextCasing.Mixed;
                        }
                    }

                    return TextCasing.FirstUpper;
                }
                else if (char.IsUpper(ch))
                {
                    for (int i = 0; i < s.Length; i++)
                    {
                        if (!char.IsUpper(s[i])
                            && !char.IsLetter(s[i]))
                        {
                            return TextCasing.Mixed;
                        }
                    }

                    return TextCasing.Upper;
                }
            }

            return TextCasing.Mixed;
        }
    }
}
