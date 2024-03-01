using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Iced.Intel;

namespace ChaosDbg.Evaluator.Masm
{
    //Based on https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Parser/Lexer.cs

    class MasmLexer
    {
        private char[] chars;
        private StringBuilder builder = new StringBuilder();

        private List<MasmSyntaxToken> tokens = new List<MasmSyntaxToken>();
        private List<string> errors = new List<string>();

        private static Dictionary<string, MasmSyntaxKind> keywords = new Dictionary<string, MasmSyntaxKind>(StringComparer.OrdinalIgnoreCase)
        {
            { "and", MasmSyntaxKind.AndKeyword },
            { "xor", MasmSyntaxKind.XorKeyword },
            { "or", MasmSyntaxKind.OrKeyword },
            { "mod", MasmSyntaxKind.ModKeyword },

            { "not", MasmSyntaxKind.NotKeyword },
            { "hi", MasmSyntaxKind.HiKeyword },
            { "low", MasmSyntaxKind.LowKeyword },
            { "by", MasmSyntaxKind.ByKeyword },
            { "pby", MasmSyntaxKind.PhysicalByKeyword },
            { "wo", MasmSyntaxKind.WordKeyword },
            { "pwo", MasmSyntaxKind.PhysicalWordKeyword },
            { "dwo", MasmSyntaxKind.DoubleWordKeyword },
            { "pdwo", MasmSyntaxKind.PhysicalDoubleWordKeyword },
            { "qwo", MasmSyntaxKind.QuadWordKeyword },
            { "pqwo", MasmSyntaxKind.PhysicalQuadWordKeyword },
            { "poi", MasmSyntaxKind.PoiKeyword },
            { "ppoi", MasmSyntaxKind.PhysicalPoiKeyword },
        };

        private static Dictionary<string, Register> registers = Enum.GetValues(typeof(Register)).Cast<Register>()
            .ToDictionary(v => v.ToString(), v => v, StringComparer.OrdinalIgnoreCase);

#if DEBUG
        static MasmLexer()
        {
            foreach (var value in keywords.Values)
                Debug.Assert(MasmSyntaxFacts.IsKeyword(value));
        }
#endif

        private int currentCharIndex;

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

        public static (MasmSyntaxToken[] result, string[] errors) Lex(string expr)
        {
            var lexer = new MasmLexer(expr);
            lexer.LexInternal();
            return (lexer.tokens.ToArray(), lexer.errors.ToArray());
        }

        private MasmLexer(string expr)
        {
            chars = expr.ToCharArray();
        }

        public void LexInternal()
        {
            while (currentCharIndex < chars.Length)
            {
                switch (CurrentChar)
                {
                    case '*':
                        SimpleToken(MasmSyntaxKind.AsteriskToken);
                        break;

                    case '/':
                        SimpleToken(MasmSyntaxKind.SlashToken);
                        break;

                    case '%':
                        SimpleToken(MasmSyntaxKind.PercentToken);
                        break;

                    case '+':
                        SimpleToken(MasmSyntaxKind.PlusToken);
                        break;

                    case '-':
                        SimpleToken(MasmSyntaxKind.MinusToken);
                        break;

                    case '<':
                        if (NextChar == '<')
                        {
                            MoveNext();
                            SimpleToken(MasmSyntaxKind.LessThanLessThanToken);
                        }
                        else if (NextChar == '=')
                        {
                            MoveNext();
                            SimpleToken(MasmSyntaxKind.LessThanEqualsToken);
                        }
                        else
                            SimpleToken(MasmSyntaxKind.LessThanToken);
                        break;

                    case '>':
                        if (NextChar == '>')
                        {
                            MoveNext();

                            if (NextChar == '>')
                            {
                                MoveNext();
                                SimpleToken(MasmSyntaxKind.GreaterThanGreaterThanGreaterThanToken);
                            }
                            else
                            {
                                SimpleToken(MasmSyntaxKind.GreaterThanGreaterThanToken);
                            }
                        }
                        else if (NextChar == '=')
                        {
                            MoveNext();

                            SimpleToken(MasmSyntaxKind.GreaterThanEqualsToken);
                        }
                        else
                            SimpleToken(MasmSyntaxKind.GreaterThanToken);

                        break;

                    case '!':
                        if (NextChar == '=')
                        {
                            MoveNext();

                            SimpleToken(MasmSyntaxKind.ExclamationEqualsToken);
                        }
                        else
                            SimpleToken(MasmSyntaxKind.ExclamationToken);

                        break;

                    case '(':
                        SimpleToken(MasmSyntaxKind.OpenParenToken);
                        break;

                    case ')':
                        SimpleToken(MasmSyntaxKind.CloseParenToken);
                        break;

                    case '[':
                        SimpleToken(MasmSyntaxKind.OpenBracketToken);
                        break;

                    case ']':
                        SimpleToken(MasmSyntaxKind.CloseBracketToken);
                        break;

                    case ':':
                        SimpleToken(MasmSyntaxKind.ColonToken);
                        break;

                    case ';':
                        SimpleToken(MasmSyntaxKind.SemicolonToken);
                        break;

                    case '&':
                        SimpleToken(MasmSyntaxKind.AmpersandToken);
                        break;

                    case '^':
                        SimpleToken(MasmSyntaxKind.CaretToken);
                        break;

                    case '|':
                        SimpleToken(MasmSyntaxKind.BarToken);
                        break;

                    case '.':
                        SimpleToken(MasmSyntaxKind.DotToken);
                        break;

                    case '$':
                        throw new NotImplementedException("Handling $ values is not implemented");

                    case '@':
                        if (NextChar == '$')
                        {
                            throw new NotImplementedException("Handling @$ values is not implemented");
                        }
                        else
                            MoveNext();

                        ScanIdentifierOrKeywordOrRegister(MasmSyntaxKind.RegisterLiteralToken);

                        break;

                    case (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'):
                        ScanIdentifierOrKeywordOrRegister();
                        break;

                    case '`':
                        var badToken = ScanNumericLiteral();
                        AddError($"Syntax error at '{badToken}'");
                        break;

                    case >= '0' and <= '9':
                        ScanNumericLiteral();
                        break;

                    default:
                        throw new NotImplementedException($"Don't know how to handle a token starting with character '{CurrentChar}'");
                }
            }

            tokens.Add(new MasmSyntaxToken(MasmSyntaxKind.EndOfFileToken, string.Empty, null));
        }

        private void ScanIdentifierOrKeywordOrRegister(MasmSyntaxKind expectedKind = MasmSyntaxKind.None)
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
                        builder.Append(ch);
                        break;

                    default:
                        read = false;
                        break;
                }
            } while (read && MoveNext());

            var str = builder.ToString();
            builder.Clear();

            if (expectedKind == MasmSyntaxKind.None)
            {
                //Prefer hex as long as we're not asking for a specific kind. e.g. "ah" is a valid register, but if
                //we didn't do @ah then prefer hex
                if (long.TryParse(str.TrimEnd('h'), NumberStyles.AllowHexSpecifier, null, out var hexNumber))
                {
                    var numericToken = new MasmSyntaxToken(MasmSyntaxKind.NumericLiteralToken, str, hexNumber);
                    tokens.Add(numericToken);
                    return;
                }
            }

            MasmSyntaxKind kind;
            object value = str;

            if (!keywords.TryGetValue(str, out kind))
            {
                if (registers.TryGetValue(str, out var register))
                {
                    kind = MasmSyntaxKind.RegisterLiteralToken;
                    value = register;
                }
                else
                    kind = MasmSyntaxKind.IdentifierToken;
            }

            if (expectedKind != MasmSyntaxKind.None && expectedKind != kind)
            {
                switch (expectedKind)
                {
                    case MasmSyntaxKind.RegisterLiteralToken:
                        AddError($"Bad register error at '@{value}'");
                        break;

                    default:
                        throw new NotImplementedException($"Don't know how to handle error for '{nameof(MasmSyntaxKind)}.{expectedKind}'");
                }
            }

            var token = new MasmSyntaxToken(kind, str, value);
            tokens.Add(token);
        }

        private MasmSyntaxToken ScanNumericLiteral()
        {
            //All numbers are implicitly assumed to be hex, but we also track what sort of number it is
            //since we can have various prefixes and suffixes that can change how we interpret the value
            var haveSpecifier = false;
            var isHex = false;
            var isDecimal = false;
            var isOctal = false;
            var isBinary = false;

            var ch = CurrentChar;

            //Is there a hex identifier?
            if (ch == '0')
            {
                ch = NextChar;

                if (ch == 'x' || ch == 'X')
                {
                    isHex = true;
                    haveSpecifier = true;
                    MoveNext(2);
                }
                else if (ch == 'n' || ch == 'N')
                {
                    isDecimal = true;
                    haveSpecifier = true;
                    MoveNext(2);
                }
                else if (ch == 't' || ch == 'T')
                {
                    isOctal = true;
                    haveSpecifier = true;
                    MoveNext(2);
                }
                else if (ch == 'y' || ch == 'Y')
                {
                    isBinary = true;
                    haveSpecifier = true;
                    MoveNext(2);
                }
            }

            if (!haveSpecifier)
                isHex = true;

            long value;

            if (isHex)
            {
                value = ScanNumberInternal(MasmSyntaxFacts.IsHexDigit, true, 16);

                //Ignore any 'h' identifier on the end
                if (CurrentChar == 'h')
                    MoveNext();
            }
            else if (isDecimal)
                value = ScanNumberInternal(MasmSyntaxFacts.IsDecimalDigit, false, 10);
            else if (isOctal)
                value = ScanNumberInternal(MasmSyntaxFacts.IsOctalDigit, false, 8);
            else if (isBinary)
                value = ScanNumberInternal(MasmSyntaxFacts.IsBinaryDigit, false, 2);
            else
                throw new NotImplementedException("Don't know what type of value this is");

            return FinalizeToken(MasmSyntaxKind.NumericLiteralToken, value);
        }

        private long ScanNumberInternal(Func<char, bool> isValidDigit, bool allowDelim, int fromBase)
        {
            var badDelim = false;

            while (currentCharIndex < chars.Length)
            {
                var ch = CurrentChar;

                if (isValidDigit(ch))
                {
                    builder.Append(ch);

                    if (!MoveNext())
                        break;

                    continue;
                }    

                if (ch == '`')
                {
                    if (allowDelim)
                    {
                        builder.Append(ch);

                        if (!MoveNext())
                            break;

                        continue;
                    }

                    badDelim = true;
                }

                break;
            }

            if (badDelim)
                AddError($"Extra character ` in '{CurrentWord}'");

            if (builder.Length == 0)
                return 0;

            return Convert.ToInt64(CurrentWord.Replace("`", string.Empty), fromBase);
        }

        private MasmSyntaxToken SimpleToken(MasmSyntaxKind kind)
        {
            builder.Append(CurrentChar);
            MoveNext();
            return FinalizeToken(kind);
        }

        private MasmSyntaxToken FinalizeToken(MasmSyntaxKind kind, object value = null)
        {
            var str = builder.ToString();
            builder.Clear();

            var token = new MasmSyntaxToken(kind, str, value);

            tokens.Add(token);

            return token;
        }

        public bool MoveNext(int count = 1)
        {
            currentCharIndex += count;

            if (char.IsWhiteSpace(CurrentChar))
            {
                do
                {
                    currentCharIndex++;
                } while (currentCharIndex < chars.Length && char.IsWhiteSpace(CurrentChar));

                return false;
            }

            return currentCharIndex < chars.Length;
        }

        private void AddError(string message)
        {
            errors.Add(message);
        }
    }
}
