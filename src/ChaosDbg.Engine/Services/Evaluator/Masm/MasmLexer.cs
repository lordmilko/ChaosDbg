using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Iced.Intel;

namespace ChaosDbg.Evaluator.Masm
{
    //Based on https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Parser/Lexer.cs

    class MasmLexer : AbstractLexer<MasmSyntaxKind, MasmSyntaxToken>
    {
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

        public static (MasmSyntaxToken[] result, string[] errors) Lex(string expr)
        {
            var lexer = new MasmLexer(expr);
            lexer.LexInternal();
            return (lexer.tokens.ToArray(), lexer.errors.ToArray());
        }

        private MasmLexer(string expr) : base(expr.ToCharArray())
        {
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
                        if (NextChar == ':')
                        {
                            MoveNext();

                            SimpleToken(MasmSyntaxKind.ColonColonToken);
                        }
                        else
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

                    case ('_' or >= 'a' and <= 'z') or (>= 'A' and <= 'Z'):
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
            var str = ScanIdentifier(allowDot: true, allowDollar: false);

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
                var registerStr = str;

                if (registerStr.StartsWith("@"))
                    registerStr = str.Substring(1);

                if (registers.TryGetValue(registerStr, out var register))
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
                        AddError($"Bad register error at '{value}'");
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
                value = ScanInteger(IsHexDigit, true, 16);

                //Ignore any 'h' identifier on the end
                if (CurrentChar == 'h')
                    MoveNext();
            }
            else if (isDecimal)
                value = ScanInteger(IsDecimalDigit, false, 10);
            else if (isOctal)
                value = ScanInteger(IsOctalDigit, false, 8);
            else if (isBinary)
                value = ScanInteger(IsBinaryDigit, false, 2);
            else
                throw new NotImplementedException("Don't know what type of value this is");

            return FinalizeToken(MasmSyntaxKind.NumericLiteralToken, value);
        }

        protected override MasmSyntaxToken CreateToken(MasmSyntaxKind kind, string text, object value) =>
            new MasmSyntaxToken(kind, text, value);
    }
}
