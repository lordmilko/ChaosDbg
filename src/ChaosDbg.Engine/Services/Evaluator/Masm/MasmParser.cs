using System;
using System.Collections.Generic;
using System.Linq;
using ChaosDbg.Evaluator.Masm.Syntax;

namespace ChaosDbg.Evaluator.Masm
{
    //Based on https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Parser/LanguageParser.cs

    class MasmParser
    {
        public static (ExpressionSyntax result, string[] errors) Parse(MasmSyntaxToken[] tokens)
        {
            var parser = new MasmParser(tokens);
            var result = parser.ParseInternal();

            return (result, parser.errors.ToArray());
        }

        private MasmSyntaxToken[] tokens;
        private List<string> errors = new List<string>();
        private int currentTokenIndex;

        public MasmSyntaxToken CurrentToken
        {
            get
            {
                if (currentTokenIndex < tokens.Length)
                    return tokens[currentTokenIndex];

                return default;
            }
        }

        public MasmSyntaxToken NextToken
        {
            get
            {
                if (currentTokenIndex < tokens.Length - 1)
                    return tokens[currentTokenIndex + 1];

                return default;
            }
        }

        private MasmParser(MasmSyntaxToken[] tokens)
        {
            this.tokens = tokens;
        }

        private ExpressionSyntax ParseInternal()
        {
            //There should be a single expression that encapsulates all tokens. If we have remaining tokens, it's a syntax error

            var result = ParseSubExpression(Precedence.Expression);

            if (currentTokenIndex < tokens.Length - 1)
                AddError($"Extra characters were encountered: {string.Join(", ", tokens.Skip(currentTokenIndex).Take(tokens.Length - currentTokenIndex - 1).Select(v => $"'{v}'"))}");

            return result;
        }

        private ExpressionSyntax ParseParenExpression(MasmSyntaxKind openKind, MasmSyntaxKind closeKind)
        {
            var openParen = EatToken(openKind);
            var expression = ParseSubExpression(Precedence.Expression);
            var closeParen = EatToken(closeKind);

            return new ParenthesizedExpressionSyntax(openParen, expression, closeParen);
        }

        private ExpressionSyntax ParseSubExpression(Precedence precedence)
        {
            ExpressionSyntax leftOperand;
            var token = CurrentToken;

            var kind = token.Kind;

            if (MasmSyntaxFacts.IsPrefixUnaryExpression(kind))
            {
                var opKind = MasmSyntaxFacts.GetPrefixUnaryExpression(kind);
                var newPrecedence = GetPrecedence(opKind);
                var opToken = EatToken();
                var operand = ParseSubExpression(newPrecedence);
                leftOperand = new PrefixUnaryExpressionSyntax(opKind, opToken, operand);
            }
            else
            {
                leftOperand = ParseTerm();
            }

            return ParseExpressionContinued(leftOperand, precedence);
        }

        private ExpressionSyntax ParseTerm()
        {
            var kind = CurrentToken.Kind;

            switch (kind)
            {
                case MasmSyntaxKind.IdentifierToken:
                    return ParseIdentifier();

                case MasmSyntaxKind.NumericLiteralToken:
                case MasmSyntaxKind.RegisterLiteralToken:
                case MasmSyntaxKind.DotToken:
                    return new LiteralExpressionSyntax(MasmSyntaxFacts.GetLiteralExpression(kind), EatToken());

                case MasmSyntaxKind.OpenParenToken:
                    return ParseParenExpression(MasmSyntaxKind.OpenParenToken, MasmSyntaxKind.CloseParenToken);

                //[] means the same thing as (). It does not mean poi
                case MasmSyntaxKind.OpenBracketToken:
                    return ParseParenExpression(MasmSyntaxKind.OpenBracketToken, MasmSyntaxKind.CloseBracketToken);

                case MasmSyntaxKind.NotKeyword:
                case MasmSyntaxKind.HiKeyword:
                case MasmSyntaxKind.LowKeyword:
                case MasmSyntaxKind.ByKeyword:
                case MasmSyntaxKind.PhysicalByKeyword:
                case MasmSyntaxKind.WordKeyword:
                case MasmSyntaxKind.PhysicalWordKeyword:
                case MasmSyntaxKind.DoubleWordKeyword:
                case MasmSyntaxKind.PhysicalDoubleWordKeyword:
                case MasmSyntaxKind.QuadWordKeyword:
                case MasmSyntaxKind.PhysicalQuadWordKeyword:
                case MasmSyntaxKind.PoiKeyword:
                case MasmSyntaxKind.PhysicalPoiKeyword:
                    return new PseudoFunctionExpressionSyntax(MasmSyntaxKind.PseudoFunctionExpression, EatToken(), ParseSubExpression(Precedence.Expression));

                default:
                    if (MasmSyntaxFacts.IsKeyword(kind))
                        throw new NotImplementedException($"Don't know how to handle keyword '{kind}'");

                    switch (kind)
                    {
                        case MasmSyntaxKind.EndOfFileToken:
                            AddError("Numeric expression missing from '<EOL>'");
                            break;

                        //It's meant to be a symbol but there's no module? Unary not is not supported
                        case MasmSyntaxKind.ExclamationToken:
                            AddError("Couldn't resolve error at '{0}'");
                            break;

                        default:
                            AddError("Numeric expression missing from '{0}'");
                            break;
                    }
                    
                    return new LiteralExpressionSyntax(MasmSyntaxKind.NumericLiteralExpression, CurrentToken);
            }
        }

        private ExpressionSyntax ParseIdentifier()
        {
            //could be a variable in the local scope maybe?
            var moduleOrIdentifier = EatToken();

            if (CurrentToken.Kind == MasmSyntaxKind.ExclamationToken)
            {
                var exclamationMark = EatToken();

                //Can'tt have ntdll!1, it must be an identifier. So if the next token is a number, it'll be a NumericLiteralToken
                var function = EatToken(MasmSyntaxKind.IdentifierToken, $"Couldn't resolve error at '{moduleOrIdentifier}!{CurrentToken}'");

                return new SymbolFunctionExpressionSyntax(moduleOrIdentifier, exclamationMark, function);
            }

            return new SymbolIdentifierExpressionSyntax(moduleOrIdentifier);
        }

        private ExpressionSyntax ParseExpressionContinued(ExpressionSyntax leftOperand, Precedence precedence)
        {
            while (currentTokenIndex < tokens.Length)
            {
                var kind = CurrentToken.Kind;
                MasmSyntaxKind opKind; //We break out if we can't set this to anything

                if (MasmSyntaxFacts.IsBinaryExpression(kind))
                    opKind = MasmSyntaxFacts.GetBinaryExpression(kind);
                else
                    break;

                var newPrecedence = GetPrecedence(opKind);

                //When we do 1 + 2 * 3 + 4, we break here while handling the 3 + and handle the + on the next loop
                if (newPrecedence < precedence)
                    break;

                //1 + 2 * 3 / 4. We break here while handling the 3 / and then handle the / on the next loop
                if (newPrecedence == precedence)
                    break;

                var opToken = EatToken();

                var leftPrecedence = GetPrecedence(leftOperand.Kind);

                if (newPrecedence > leftPrecedence)
                    throw new NotImplementedException($"New precedence {newPrecedence} was greater than left precedence {leftPrecedence}. Don't know how to handle this");

                leftOperand = new BinaryExpressionSyntax(opKind, leftOperand, opToken, ParseSubExpression(newPrecedence));
            }

            return leftOperand;
        }

        private MasmSyntaxToken EatToken()
        {
            var token = CurrentToken;
            MoveNext();
            return token;
        }

        public MasmSyntaxToken EatToken(MasmSyntaxKind expectedKind, string errorMessage = null)
        {
            var token = CurrentToken;

            //When a given token MUST exist, Roslyn handles by this by forcefully creating the required token so that parsing
            //can continue, logs the error and reports it at the end
            if (token.Kind != expectedKind)
            {
                AddError(errorMessage ?? "Syntax error at '<EOL>'");
                return new MasmSyntaxToken(expectedKind, string.Empty, null);
            }

            MoveNext();
            return token;
        }

        private void MoveNext()
        {
            currentTokenIndex++;
        }

        private void AddError(string message)
        {
            errors.Add(message);
        }

        private enum Precedence : uint
        {
            Expression = 0, // Loosest possible precedence, used to accept all expressions
            LogicalOr,
            LogicalXor,
            LogicalAnd,
            Equality,
            Shift,
            Additive,
            Multiplicative,
            Primary,
        }

        private static Precedence GetPrecedence(MasmSyntaxKind op)
        {
            switch (op)
            {
                case MasmSyntaxKind.BitwiseOrExpression:
                    return Precedence.LogicalOr;
                case MasmSyntaxKind.ExclusiveOrExpression:
                    return Precedence.LogicalXor;
                case MasmSyntaxKind.BitwiseAndExpression:
                    return Precedence.LogicalAnd;
                case MasmSyntaxKind.EqualsExpression:
                case MasmSyntaxKind.NotEqualsExpression:
                    return Precedence.Equality;
                case MasmSyntaxKind.LessThanExpression:
                case MasmSyntaxKind.LessThanOrEqualExpression:
                case MasmSyntaxKind.GreaterThanExpression:
                case MasmSyntaxKind.GreaterThanOrEqualExpression:
                case MasmSyntaxKind.LeftShiftExpression:
                case MasmSyntaxKind.RightShiftExpression:
                case MasmSyntaxKind.UnsignedRightShiftExpression:
                    return Precedence.Shift;
                case MasmSyntaxKind.AddExpression:
                case MasmSyntaxKind.SubtractExpression:
                    return Precedence.Additive;
                case MasmSyntaxKind.MultiplyExpression:
                case MasmSyntaxKind.DivideExpression:
                case MasmSyntaxKind.ModuloExpression:
                    return Precedence.Multiplicative;
                case MasmSyntaxKind.UnaryPlusExpression:
                case MasmSyntaxKind.UnaryMinusExpression:
                case MasmSyntaxKind.NumericLiteralExpression:
                case MasmSyntaxKind.RegisterLiteralExpression:
                case MasmSyntaxKind.ParenthesizedExpression:
                case MasmSyntaxKind.SymbolFunctionExpression:
                case MasmSyntaxKind.SymbolIdentifierExpression:
                    return Precedence.Primary;
                default:
                    throw new NotImplementedException($"Don't know what the precedence of '{nameof(MasmSyntaxKind)}.{op}' should be.");
            }
        }
    }
}
