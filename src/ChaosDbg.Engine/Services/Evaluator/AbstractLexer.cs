using System;
using System.Collections.Generic;
using System.Text;

namespace ChaosDbg.Evaluator
{
    abstract class AbstractLexer<TSyntaxKind, TToken>
    {
        protected char[] chars;
        protected StringBuilder builder = new StringBuilder();

        protected List<TToken> tokens = new List<TToken>();
        protected List<string> errors = new List<string>();

        public static bool IsHexDigit(char c) =>
            c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';

        public static bool IsDecimalDigit(char c) =>
            c is >= '0' and <= '9';

        public static bool IsOctalDigit(char c) =>
            c is >= '0' and <= '7';

        public static bool IsBinaryDigit(char c) =>
            c == '0' | c == '1';

        protected int currentCharIndex;

        public char CurrentChar
        {
            get
            {
                if (currentCharIndex < chars.Length)
                    return chars[currentCharIndex];

                return char.MaxValue;
            }
        }

        public string CurrentWord => builder.ToString();

        public char NextChar
        {
            get
            {
                if (currentCharIndex >= chars.Length - 1)
                    return char.MaxValue;

                return chars[currentCharIndex + 1];
            }
        }

        protected AbstractLexer(char[] chars)
        {
            this.chars = chars;
        }

        protected long ScanInteger(Func<char, bool> isValidDigit, bool allowDelim, int fromBase)
        {
            var badDelim = false;

            var prefixLength = builder.Length;

            ScanNumberInternal(isValidDigit, allowDelim, ref badDelim);

            if (badDelim)
                AddError($"Extra character ` in '{CurrentWord}'");

            if (builder.Length == 0)
                return 0;

            var numStr = CurrentWord;

            if (prefixLength > 0)
                numStr = numStr.Substring(prefixLength);

            numStr = numStr.Replace("`", string.Empty);

            //e.g. 0x with nothing after it
            if (numStr.Length == 0)
                return 0;

            return Convert.ToInt64(numStr, fromBase);
        }

        protected void ScanNumberInternal(Func<char, bool> isValidDigit, bool allowDelim, ref bool badDelim)
        {
            while (currentCharIndex < chars.Length)
            {
                var ch = CurrentChar;

                if (isValidDigit(ch))
                {
                    if (!MoveNext())
                        break;

                    continue;
                }

                if (ch == '`')
                {
                    if (allowDelim)
                    {
                        if (!MoveNext())
                            break;

                        continue;
                    }

                    badDelim = true;
                }

                break;
            }
        }

        protected TToken SimpleToken(TSyntaxKind kind)
        {
            MoveNext();
            return FinalizeToken(kind);
        }

        protected string ScanIdentifier(bool allowDot, bool allowDollar)
        {
            var read = true;

            do
            {
                var ch = CurrentChar;

                switch (ch)
                {
                    case (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'):
                    case >= '0' and <= '9':
                    case '_':
                        break;

                    case '$': //Allowed in C++
                        if (!allowDollar)
                            read = false;
                        break;
                    case '.':
                        if (!allowDot)
                            read = false;
                        break;

                    default:
                        read = false;
                        break;
                }
            } while (read && MoveNext());

            var str = builder.ToString();
            builder.Clear();

            return str;
        }

        protected TToken FinalizeToken(TSyntaxKind kind, object value = null)
        {
            var str = builder.ToString();
            builder.Clear();

            var token = CreateToken(kind, str, value);

            tokens.Add(token);

            return token;
        }

        public bool MoveNext(int count = 1, bool skipSpaces = true)
        {
            for (var i = 0; i < count; i++)
            {
                builder.Append(CurrentChar);
                currentCharIndex++;
            }

            if (skipSpaces)
            {
                if (char.IsWhiteSpace(CurrentChar))
                {
                    do
                    {
                        currentCharIndex++;
                    } while (currentCharIndex < chars.Length && char.IsWhiteSpace(CurrentChar));

                    return false;
                }
            }

            return currentCharIndex < chars.Length;
        }

        protected abstract TToken CreateToken(TSyntaxKind kind, string text, object value);

        protected void AddError(string message)
        {
            errors.Add(message);
        }
    }
}
