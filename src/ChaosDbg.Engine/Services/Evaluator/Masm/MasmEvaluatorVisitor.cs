using System;
using System.Globalization;
using ChaosDbg.Evaluator.Masm.Syntax;
using Iced.Intel;

namespace ChaosDbg.Evaluator.Masm
{
    class MasmEvaluatorVisitor
    {
        private IEvaluatorContext context;

        public MasmEvaluatorVisitor(IEvaluatorContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            this.context = context;
        }

        public long Visit(MasmSyntaxNode syntax) => syntax.Accept(this);

        public long VisitBinaryExpression(BinaryExpressionSyntax syntax)
        {
            var left = Visit(syntax.Left);
            var right = Visit(syntax.Right);

            return syntax.Kind switch
            {
                MasmSyntaxKind.BitwiseOrExpression          => left | right,
                MasmSyntaxKind.ExclusiveOrExpression        => left ^ right,
                MasmSyntaxKind.BitwiseAndExpression         => left & right,
                MasmSyntaxKind.EqualsExpression             => left == right ? 1 : 0,
                MasmSyntaxKind.NotEqualsExpression          => left != right ? 1 : 0,
                MasmSyntaxKind.LessThanExpression           => left < right ? 1 : 0,
                MasmSyntaxKind.LessThanOrEqualExpression    => left <= right ? 1 : 0,
                MasmSyntaxKind.GreaterThanExpression        => left > right ? 1 : 0,
                MasmSyntaxKind.GreaterThanOrEqualExpression => left >= right ? 1 : 0,
                MasmSyntaxKind.LeftShiftExpression          => left << (int) right,
                MasmSyntaxKind.RightShiftExpression         => left >> (int) right,
                MasmSyntaxKind.UnsignedRightShiftExpression => (long) ((ulong) left >> (int) right),
                MasmSyntaxKind.AddExpression                => left + right,
                MasmSyntaxKind.SubtractExpression           => left - right,
                MasmSyntaxKind.MultiplyExpression           => left * right,
                MasmSyntaxKind.DivideExpression             => left / right,
                MasmSyntaxKind.ModuloExpression             => left % right,
                _ => throw new NotImplementedException($"Don't know how to handle {nameof(MasmSyntaxKind)} '{syntax.Kind}'")
            };
        }

        public long VisitLiteralExpression(LiteralExpressionSyntax syntax)
        {
            switch (syntax.Kind)
            {
                case MasmSyntaxKind.NumericLiteralExpression:
                    return (long) syntax.Token.Value;

                case MasmSyntaxKind.RegisterLiteralExpression:
                    return context.GetRegisterValue((Register) syntax.Token.Value);

                case MasmSyntaxKind.CurrentIPExpression:
                    return context.GetCurrentIP();

                default:
                    throw new NotImplementedException($"Don't know how to handle literal of type '{syntax.Kind}'");
            }
        }

        public long VisitParenthesizedExpression(ParenthesizedExpressionSyntax syntax) =>
            Visit(syntax.Expression);

        public long VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax syntax)
        {
            var operand = Visit(syntax.Operand);

            switch (syntax.Kind)
            {
                case MasmSyntaxKind.UnaryMinusExpression:
                    return -operand;

                case MasmSyntaxKind.UnaryPlusExpression:
                    return operand;

                default:
                    throw new NotImplementedException($"Don't know how to handle unary syntax of type '{syntax.Kind}'");
            }
        }

        public long VisitPseudoFuntionExpression(PseudoFunctionExpressionSyntax syntax)
        {
            var operand = Visit(syntax.Operand);

            switch (syntax.Token.Kind)
            {
                case MasmSyntaxKind.NotKeyword:
                    return operand == 1 ? 0 : 1;

                case MasmSyntaxKind.PoiKeyword:
                    if (context.TryGetPointerValue(operand, out var result))
                        return result;

                    throw new InvalidExpressionException($"Memory access error at '{operand:x}'");

                default:
                    throw new NotImplementedException($"Handling pseudo function {syntax.Token.Kind} is not implemented");
            }
        }

        public long VisitSymbolModuleQualifiedExpression(SymbolModuleQualifiedExpressionSyntax syntax)
        {
            if (context.TryGetModuleQualifiedSymbolValue(syntax.Module.ToString(), syntax.Expression.ToString(), out var address))
                return address;

            throw new InvalidExpressionException($"Couldn't resolve error at '{syntax}'");
        }

        public long VisitSymbolIdentifierExpression(SymbolIdentifierExpressionSyntax syntax)
        {
            var str = syntax.ToString();

            if (context.TryGetSimpleSymbolValue(str, out var address))
                return address;

            //Maybe it's actually a hexadecimal value (e.g. 'a' or 'ah')
            if (long.TryParse(str.TrimEnd('h'), NumberStyles.AllowHexSpecifier, null, out var result))
                return result;

            throw new InvalidExpressionException($"Couldn't resolve error at '{syntax}'");
        }
    }
}
