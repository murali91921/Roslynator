// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Roslynator.Spelling
{
    internal abstract class SpellingErrorComparer :
        IComparer<SpellingError>,
        IEqualityComparer<SpellingError>,
        IComparer,
        IEqualityComparer
    {
        public static SpellingErrorComparer FilePathThenSpanStart { get; } = new FilePathThenSpanStartComparer();

        public abstract int Compare(SpellingError x, SpellingError y);

        public abstract bool Equals(SpellingError x, SpellingError y);

        public abstract int GetHashCode(SpellingError obj);

        public int Compare(object x, object y)
        {
            if (x == y)
                return 0;

            if (x == null)
                return -1;

            if (y == null)
                return 1;

            if (x is SpellingError a
                && y is SpellingError b)
            {
                return Compare(a, b);
            }

            throw new ArgumentException("", nameof(x));
        }

        new public bool Equals(object x, object y)
        {
            if (x == y)
                return true;

            if (x == null)
                return false;

            if (y == null)
                return false;

            if (x is SpellingError a
                && y is SpellingError b)
            {
                return Equals(a, b);
            }

            throw new ArgumentException("", nameof(x));
        }

        public int GetHashCode(object obj)
        {
            if (obj == null)
                return 0;

            if (obj is SpellingError spellingError)
                return GetHashCode(spellingError);

            throw new ArgumentException("", nameof(obj));
        }

        private class FilePathThenSpanStartComparer : SpellingErrorComparer
        {
            public override int Compare(SpellingError x, SpellingError y)
            {
                int result = StringComparer.OrdinalIgnoreCase.Compare(
                    x.Location.SourceTree?.FilePath,
                    y.Location.SourceTree?.FilePath);

                if (result != 0)
                    return result;

                return x.Location.SourceSpan.Start.CompareTo(y.Location.SourceSpan.Start);
            }

            public override bool Equals(SpellingError x, SpellingError y)
            {
                return StringComparer.OrdinalIgnoreCase.Equals(
                    x.Location.SourceTree?.FilePath,
                    y.Location.SourceTree?.FilePath)
                    && x.Location.SourceSpan.Start == y.Location.SourceSpan.Start;
            }

            public override int GetHashCode(SpellingError obj)
            {
                return Hash.Combine(
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Location.SourceTree?.FilePath),
                    obj.Location.SourceSpan.Start);
            }
        }
    }
}
