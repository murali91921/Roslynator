// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Roslynator.Spelling
{
    // vložit mezeru nebo vložit pomlčku a nebo změnit case: 
    // usecase=use case
    // wellformed=well-formed
    // TestNonexistantFields=Nonexistent

    // přidat písmeno na konec Unknow > Unknown
    // přidat sa Unqoted > Unquoted
    // přidat sou mezi sa a sou vebose > verbose
    // odmazat jedno písmeno
    // souhlásku za jinou souhlásku
    // pronounciation > pronounciation
    internal static class SpellingFixGenerator
    {
        private static readonly string[] _vowels = new[] {"a", "e", "i", "o", "u", "y" };

        public static IEnumerable<string> GeneratePossibleFixes(SpellingError spellingError, SpellingData spellingData)
        {
            string value = spellingError.Value;

            int length = value.Length;

            // shouldnt > shouldn't
            string fix = AddMissingApostrophe(value);

            if (fix != null)
                yield return fix;

            // usefull > useful
            fix = value.Remove(value.Length - 1);

            if (IsValidFix(fix))
                yield return fix;

            if (value.EndsWith("ed"))
            {
                if (value.EndsWith("tted"))
                {
                    string s = value.Remove(value.Length - 3);

                    if (IsValidFix(s))
                        yield return s;
                }
                else
                {
                    string s = value.Remove(value.Length - 2);

                    if (IsValidFix(s))
                        yield return s;
                }
            }
            else if (value.EndsWith("ial"))
            {
                string s = value.Remove(value.Length - 3);

                if (IsValidFix(s))
                    yield return s;
            }
            else if (value.EndsWith("ical"))
            {
                string s = value.Remove(value.Length - 2);

                if (IsValidFix(s))
                    yield return s;
            }

            int i = 0;
            for (; i < length; i++)
            {
                char ch = value[i];

                if (Peek(1) == ch)
                {
                    // xxx > xx
                    if (Peek(2) == ch)
                    {
                        string s = value.Remove(i);

                        if (IsValidFix(s))
                            yield return s;
                    }
                    // xx > x
                    else
                    {
                        string s = value.Remove(i);

                        if (IsValidFix(s))
                            yield return s;
                    }
                }

                switch (ch)
                {
                    case 'a':
                        {
                            // collapsable > collapsible
                            // compatability > compatibility
                            if (IsNextSuffix("ble")
                                && IsNextSuffix("bility"))
                            {
                                string s = Replace("i");

                                if (IsValidFix(s))
                                    yield return s;
                            }

                            break;
                        }
                    case 'e':
                        {
                            // cacheing > caching
                            // customizeable > customizable
                            // serializeability > serializability
                            if (IsNextSuffix("ing")
                                || IsNextSuffix("able")
                                || IsNextSuffix("ability"))
                            {
                                string s = value.Remove(i);

                                if (IsValidFix(s))
                                    yield return s;
                            }

                            break;
                        }
                    case 'f':
                        {
                            // alfabetical > alphabetical
                            string s = Replace("ph");

                            if (IsValidFix(s))
                                yield return s;

                            break;
                        }
                    case 'i':
                        {
                            string s = Replace("y");

                            if (IsValidFix(s))
                                yield return s;

                            break;
                        }
                    case 'k':
                        {
                            // kanada > canada
                            string s = Replace("c");

                            if (IsValidFix(s))
                                yield return s;

                            break;
                        }
                    case 'm':
                        {
                            // synomyms > synonyms
                            string s = Replace("n");

                            if (IsValidFix(s))
                                yield return s;

                            break;
                        }
                    case 'y':
                        {
                            string s = Replace("z");

                            if (IsValidFix(s))
                                yield return s;

                            s = Replace("i");

                            if (IsValidFix(s))
                                yield return s;

                            break;
                        }
                    case 'z':
                        {
                            // spezification > specification
                            string s = Replace("c");

                            if (IsValidFix(s))
                                yield return s;

                            s = Replace("y");

                            if (IsValidFix(s))
                                yield return s;

                            break;
                        }
                }

                // readonlyly > readonly
                // sensititive > sensitive
                if (i < length - 3)
                {
                    char ch2 = Peek();

                    if (ch2 != ch
                        && ch == Peek(2)
                        && ch2 == Peek(3))
                    {
                        string s = value.Remove(i, 2);

                        if (IsValidFix(s))
                            yield return s;
                    }
                }

                // delimter > delimiter
                // unusd > unused
                // targt > target
                if (IsConsonant(ch))
                {
                    char ch2 = Peek();

                    if (ch2 != '\0')
                    {
                        if (IsConsonant(ch2))
                        {
                            foreach (string vowel in _vowels)
                            {
                                string s = value.Insert(i + 1, vowel);
                                if (IsValidFix(s))
                                    yield return s;
                            }
                        }
                        else
                        {
                            // wraping > wrapping
                            // succesfully > successfully
                            // sucessfully > successfully
                            string s = value.Insert(i, ch.ToString());
                            if (IsValidFix(s))
                                yield return s;

                            if (IsConsonant(Peek(2)))
                            {
                                // deterimine > determine
                                // dicationary > dictionary
                                // verision > version
                                // weired > weird
                                s = value.Remove(i + 1, 1);
                                if (IsValidFix(s))
                                    yield return s;
                            }
                        }
                    }
                }
                else
                {
                    char ch2 = Peek();
                    if (IsConsonant(ch2))
                    {
                        // succed > succeed
                        string s = value.Insert(i, ch.ToString());
                        if (IsValidFix(s))
                            yield return s;
                    }
                }
            }

            i = 0;
            for (; i < length - 1; i++)
            {
                // udpate > update
                // ugprade > upgrade
                // sublcass > subclass
                // threefore > therefore
                string s = value.Remove(i, 1).Insert(i + 1, value[i].ToString());
                if (IsValidFix(s))
                    yield return s;
            }

            i = 0;
            for (; i < length - 1; i++)
            {
                if (IsVowel(value[i]))
                {
                    // valies > values
                    // stabelize > stabilize
                    foreach (string vowel in _vowels)
                    {
                        string s2 = Replace(vowel);
                        if (IsValidFix(s2))
                            yield return s2;
                    }
                }
                // udpate > update
                // ugprade > upgrade
                // sublcass > subclass
                // threefore > therefore
                string s = value.Remove(i, 1).Insert(i + 1, value[i].ToString());
                if (IsValidFix(s))
                    yield return s;
            }

            string Replace(string s, int length = 1)
            {
                return value.Remove(i, length).Insert(i, s);
            }

            char Peek(int offset = 1)
            {
                if (i < length - offset)
                    return value[i + offset];

                return default;
            }

            bool IsNextText(string s, int offset = 1)
            {
                return i + offset + s.Length <= length
                    && string.CompareOrdinal(value, i + offset, s, 0, s.Length) == 0;
            }

            bool IsNextSuffix(string s, int offset = 1)
            {
                return IsNextText(s, offset)
                    && i + offset + s.Length == length;
            }

            bool IsValidFix(string value)
            {
                return spellingData.List.Contains(value)
                    && !spellingData.IgnoreList.Contains(value);
            }
        }

        private static string AddMissingApostrophe(string value)
        {
            switch (value)
            {
                case "aint": // ain't
                case "arent": // aren't
                case "cant": // can't
                case "couldnt": // couldn't
                case "didnt": // didn't
                case "doesnt": // doesn't
                case "dont": // don't
                case "hadnt": // hadn't
                case "hasnt": // hasn't
                case "havent": // haven't
                case "hed": // he'd
                case "hell": // he'll
                case "hes": // he's
                case "id": // i'd
                case "ill": // i'll
                case "im": // i'm
                case "is": // i's
                case "isnt": // isn't
                case "itd": // it'd
                case "itll": // it'll
                case "its": // it's
                case "ive": // i've
                case "mustnt": // mustn't
                case "mustve": // must've
                case "neednt": // needn't
                case "oughtnt": // oughtn't
                case "shant": // shan't
                case "shed": // she'd
                case "shell": // she'll
                case "shes": // she's
                case "shouldnt": // shouldn't
                case "thatd": // that'd
                case "thatll": // that'll
                case "thats": // that's
                case "thered": // there'd
                case "therell": // there'll
                case "theres": // there's
                case "theyd": // they'd
                case "theyll": // they'll
                case "theyre": // they're
                case "theyve": // they've
                case "wasnt": // wasn't
                case "wed": // we'd
                case "well": // we'll
                case "were": // we're
                case "werent": // weren't
                case "weve": // we've
                case "whatd": // what'd
                case "whatll": // what'll
                case "whats": // what's
                case "whens": // when's
                case "whered": // where'd
                case "wheres": // where's
                case "whod": // who'd
                case "wholl": // who'll
                case "whore": // who're
                case "whos": // who's
                case "whove": // who've
                case "whys": // why's
                case "wont": // won't
                case "wouldnt": // wouldn't
                case "youd": // you'd
                case "youll": // you'll
                case "youre": // you're
                case "youve": // you've
                    {
                        switch (value[value.Length - 1])
                        {
                            case 'e':
                            case 'l':
                                    return value.Insert(value.Length - 2, "'");
                            default:
                                    return value.Insert(value.Length - 1, "'");
                        }
                    }
            }

            return null;
        }

        private static bool IsVowel(char ch)
        {
            switch (ch)
            {
                case 'a':
                case 'e':
                case 'i':
                case 'o':
                case 'u':
                case 'A':
                case 'E':
                case 'I':
                case 'O':
                case 'U':
                case 'y':
                case 'Y':
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsConsonant(char ch)
        {
            return ch != '\0'
                && !IsVowel(ch);
        }
    }
}
