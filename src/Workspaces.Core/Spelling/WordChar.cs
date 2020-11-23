// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Roslynator.Spelling
{
    internal readonly struct WordChar : IEquatable<WordChar>
    {
        public WordChar(char value, int index)
        {
            Value = value;
            Index = index;
        }

        public char Value { get; }

        public int Index { get; }

        public static WordChar Create(string value, int index)
        {
            return new WordChar(value[index], index);
        }

        public override bool Equals(object obj)
        {
            return obj is WordChar wordChar
                && Equals(wordChar);
        }

        public bool Equals(WordChar other)
        {
            return Value == other.Value
                && Index == other.Index;
        }

        public override int GetHashCode()
        {
            return Hash.Combine(Value, Hash.Create(Index));
        }

        public static bool operator ==(WordChar left, WordChar right) => left.Equals(right);

        public static bool operator !=(WordChar left, WordChar right) => !(left == right);
    }
}
