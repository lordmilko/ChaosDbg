using System;

namespace ChaosDbg.Evaluator.Cpp
{
    class CppLexer : AbstractLexer<CppSyntaxKind, CppSyntaxToken>
    {
        public CppLexer(string str) : base(str.ToCharArray())
        {
        }

        public void LexInternal()
        {
            while (currentCharIndex < chars.Length)
            {
                switch (CurrentChar)
                {
                    case '~':
                        SimpleToken(CppSyntaxKind.TildeToken);
                        break;

                    case '!':
                        SimpleToken(CppSyntaxKind.ExclamationToken);
                        break;

                    case '%':
                        SimpleToken(CppSyntaxKind.PercentToken);
                        break;

                    case '^':
                        SimpleToken(CppSyntaxKind.CaretToken);
                        break;

                    case '&':
                        SimpleToken(CppSyntaxKind.AmpersandToken);
                        break;

                    case '*':
                        SimpleToken(CppSyntaxKind.AsteriskToken);
                        break;

                    case '(':
                        SimpleToken(CppSyntaxKind.OpenParenToken);
                        break;

                    case ')':
                        SimpleToken(CppSyntaxKind.CloseParenToken);
                        break;

                    case '-':
                        SimpleToken(CppSyntaxKind.MinusToken);
                        break;

                    case '+':
                        SimpleToken(CppSyntaxKind.PlusToken);
                        break;

                    case '=':
                        SimpleToken(CppSyntaxKind.EqualsToken);
                        break;

                    case '{':
                        SimpleToken(CppSyntaxKind.OpenBraceToken);
                        break;

                    case '}':
                        SimpleToken(CppSyntaxKind.CloseBraceToken);
                        break;

                    case '[':
                        SimpleToken(CppSyntaxKind.OpenBracketToken);
                        break;

                    case ']':
                        SimpleToken(CppSyntaxKind.CloseBracketToken);
                        break;

                    case '|':
                        SimpleToken(CppSyntaxKind.BarToken);
                        break;

                    case '\\':
                        SimpleToken(CppSyntaxKind.BackslashToken);
                        break;

                    case ':':
                        SimpleToken(CppSyntaxKind.ColonToken);
                        break;

                    case ';':
                        SimpleToken(CppSyntaxKind.SemicolonToken);
                        break;

                    case '"':
                        ScanStringLiteral(CppSyntaxKind.StringLiteralToken, '\"');
                        break;

                    case '\'':
                        ScanStringLiteral(CppSyntaxKind.CharacterLiteralToken, '\'');
                        break;

                    case '<':
                        SimpleToken(CppSyntaxKind.LessThanToken);
                        break;

                    case ',':
                        SimpleToken(CppSyntaxKind.CommaToken);
                        break;

                    case '>':
                        SimpleToken(CppSyntaxKind.GreaterThanToken);
                        break;

                    case '.':
                        SimpleToken(CppSyntaxKind.DotToken);
                        break;

                    case '?':
                        SimpleToken(CppSyntaxKind.QuestionToken);
                        break;

                    case '/':
                        if (NextChar == '/')
                            ScanSingleLineComment();
                        else if (NextChar == '*')
                            ScanMultiLineComment();
                        else
                            SimpleToken(CppSyntaxKind.SlashToken);
                        break;

                    case '#':
                        SimpleToken(CppSyntaxKind.HashToken);
                        break;

                    //Evidently C++ does allow identifiers starting with $
                    case ('$' or '_' or >= 'a' and <= 'z') or (>= 'A' and <= 'Z'):
                        ScanIdentifierOrKeyword();
                        break;

                    case >= '0' and <= '9':
                        ScanNumericLiteral();
                        break;

                    case '\r':
                    case '\n':
                    case ' ':
                    case '\t':
                    case '\0': //Files can have stray nul's in them!
                        MoveNext();
                        builder.Clear();
                        break;

                    default:
                        MoveNext(); //Assume it's a "bad" character, e.g. some random unicode thing or a pilcrow or something. Just skip over it
                        builder.Clear();
                        break;
                }
            }

            tokens.Add(new CppSyntaxToken(CppSyntaxKind.EndOfFileToken, string.Empty, null));
        }

        private void ScanIdentifierOrKeyword()
        {
            var str = ScanIdentifier(allowDot: false, allowDollar: true);
            object value = str;

            var kind = CppSyntaxKind.IdentifierToken;

            var token = new CppSyntaxToken(kind, str, value);
            tokens.Add(token);
        }

        private void ScanNumericLiteral()
        {
            var isHex = false;

            var ch = CurrentChar;

            if (ch == '0')
            {
                ch = NextChar;

                if (ch == 'x' || ch == 'X')
                {
                    isHex = true;
                    MoveNext(2);
                }
            }

            bool badDelim = false;
            object value;

            if (isHex)
            {
                ScanNumberInternal(IsHexDigit, false, ref badDelim);

                //Preprocessor shenanigans can trip us up trying to convert to numbers
                if (CurrentWord == "0x")
                {
                    tokens.Add(FinalizeToken(CppSyntaxKind.BadToken));
                    return;
                }
                
                value = Convert.ToInt64(CurrentWord, 16);
            }
            else
            {
                //You can have "e" and stuff after a decimal value, but I think if we run into anything not supported, we'll parse it as a bad token or will capture it
                //as an identifier after this

                ScanNumberInternal(IsDecimalDigit, false, ref badDelim);

                if (CurrentChar == '.')
                {
                    MoveNext();
                    ScanNumberInternal(IsDecimalDigit, false, ref badDelim);

                    if (CurrentWord.EndsWith("."))
                    {
                        tokens.Add(FinalizeToken(CppSyntaxKind.BadToken));
                        return;
                    }

                    value = Convert.ToDouble(CurrentWord);
                }
                else
                    value = Convert.ToUInt64(CurrentWord, 10);
            }

            var token = FinalizeToken(CppSyntaxKind.NumericLiteralToken, value);
            tokens.Add(token);
        }

        private void ScanStringLiteral(CppSyntaxKind kind, char endChar)
        {
            while (currentCharIndex < chars.Length)
            {
                MoveNext(skipSpaces: false);

                var ch = CurrentChar;

                if (ch == '\\')
                {
                    //Skip past whatever character is after it
                    MoveNext(skipSpaces: false);
                }
                else if (ch == endChar)
                {
                    MoveNext();
                    break;
                }
            }

            //You can have header (*.h) files that contain assembly in them.
            //These have comments starting with ; and so we'll be tripped up
            //by apostrophe's in words. Thus, we don't assert whether we found the end or not

            FinalizeToken(kind, CurrentWord.Trim(endChar));
        }

        private void ScanSingleLineComment()
        {
            MoveNext(2, skipSpaces: false);

            var done = false;

            while (!done && currentCharIndex < chars.Length)
            {
                switch (CurrentChar)
                {
                    case '\r':
                    case '\n':
                        done = true;
                        break;

                    default:
                        MoveNext(skipSpaces: false);
                        break;
                }
            }

            FinalizeToken(CppSyntaxKind.SingleLineCommentTrivia);

            //Skip over the newline characters
            MoveNext();
            builder.Clear();
        }

        private void ScanMultiLineComment()
        {
            MoveNext(2);

            //If they didn't close the comment, it'll just extend to the end
            while (currentCharIndex < chars.Length)
            {
                if (CurrentChar == '*' && NextChar == '/')
                {
                    MoveNext(2);
                    break;
                }

                MoveNext(skipSpaces: false);
            }

            FinalizeToken(CppSyntaxKind.MultiLineCommentTrivia);
        }

        protected override CppSyntaxToken CreateToken(CppSyntaxKind kind, string text, object value) =>
            new CppSyntaxToken(kind, text, value);
    }
}
