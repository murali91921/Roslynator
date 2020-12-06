// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Roslynator.Spelling
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal abstract class SpellingDiagnostic
    {
        private string _valueLower;
        private bool? _isContained;
        private TextCasing? _casing;

        protected SpellingDiagnostic(
            Diagnostic diagnostic,
            string value,
            string containingValue,
            Location location,
            int index,
            SyntaxToken identifier = default)
        {
            Diagnostic = diagnostic;
            Value = value;
            ContainingValue = containingValue;
            Location = location;
            Index = index;
            Identifier = identifier;
        }

        public Diagnostic Diagnostic { get; }

        public string Value { get; }

        public string ContainingValue { get; }

        public Location Location { get; }

        public SyntaxToken Identifier { get; }

        public int Index { get; }

        public bool IsSymbol => Identifier.Parent != null;

        public bool IsContained => _isContained ??= !string.Equals(Value, ContainingValue, StringComparison.Ordinal);

        public string ValueLower => _valueLower ??= Value.ToLowerInvariant();

        public TextCasing Casing => _casing ??= TextUtility.GetTextCasing(Value);

        public int EndIndex => Index + Length;

        public int Length => Value.Length;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                string value2 = (string.Equals(Value, ContainingValue, StringComparison.Ordinal))
                    ? null
                    : ContainingValue;

                return $"{Value}  {value2}";
            }
        }

        public string ApplyFix(string fix)
        {
            if (!IsSymbol)
                return fix;

            return TextUtility.ReplaceRange(ContainingValue, fix, Index, Length);
        }

        public abstract bool IsApplicableFix(string fix);
    }
}
