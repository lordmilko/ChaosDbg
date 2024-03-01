namespace ChaosDbg.Evaluator.Masm
{
    class MasmSyntaxFacts
    {
        public static bool IsHexDigit(char c) =>
            c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';

        public static bool IsDecimalDigit(char c) =>
            c is >= '0' and <= '9';

        public static bool IsOctalDigit(char c) =>
            c is >= '0' and <= '7';

        public static bool IsBinaryDigit(char c) =>
            c == '0' | c == '1';

        public static bool IsPrefixUnaryExpression(MasmSyntaxKind token) =>
            GetPrefixUnaryExpression(token) != MasmSyntaxKind.None;

        public static MasmSyntaxKind GetPrefixUnaryExpression(MasmSyntaxKind tokenKind)
        {
            switch (tokenKind)
            {
                case MasmSyntaxKind.PlusToken:
                    return MasmSyntaxKind.UnaryPlusExpression;
                case MasmSyntaxKind.MinusToken:
                    return MasmSyntaxKind.UnaryMinusExpression;
                default:
                    return MasmSyntaxKind.None;
            }
        }

        public static bool IsBinaryExpression(MasmSyntaxKind tokenKind) =>
            GetBinaryExpression(tokenKind) != MasmSyntaxKind.None;

        public static MasmSyntaxKind GetBinaryExpression(MasmSyntaxKind tokenKind)
        {
            switch (tokenKind)
            {
                case MasmSyntaxKind.BarToken:
                case MasmSyntaxKind.OrKeyword:
                    return MasmSyntaxKind.BitwiseOrExpression;
                case MasmSyntaxKind.CaretToken:
                case MasmSyntaxKind.XorKeyword:
                    return MasmSyntaxKind.ExclusiveOrExpression;
                case MasmSyntaxKind.AmpersandToken:
                case MasmSyntaxKind.AndKeyword:
                    return MasmSyntaxKind.BitwiseAndExpression;
                case MasmSyntaxKind.EqualsEqualsToken:
                    return MasmSyntaxKind.EqualsExpression;
                case MasmSyntaxKind.ExclamationEqualsToken:
                    return MasmSyntaxKind.NotEqualsExpression;
                case MasmSyntaxKind.LessThanToken:
                    return MasmSyntaxKind.LessThanExpression;
                case MasmSyntaxKind.LessThanEqualsToken:
                    return MasmSyntaxKind.LessThanOrEqualExpression;
                case MasmSyntaxKind.GreaterThanToken:
                    return MasmSyntaxKind.GreaterThanExpression;
                case MasmSyntaxKind.GreaterThanEqualsToken:
                    return MasmSyntaxKind.GreaterThanOrEqualExpression;
                case MasmSyntaxKind.LessThanLessThanToken:
                    return MasmSyntaxKind.LeftShiftExpression;
                case MasmSyntaxKind.GreaterThanGreaterThanToken:
                    return MasmSyntaxKind.RightShiftExpression;
                case MasmSyntaxKind.GreaterThanGreaterThanGreaterThanToken:
                    return MasmSyntaxKind.UnsignedRightShiftExpression;
                case MasmSyntaxKind.PlusToken:
                    return MasmSyntaxKind.AddExpression;
                case MasmSyntaxKind.MinusToken:
                    return MasmSyntaxKind.SubtractExpression;
                case MasmSyntaxKind.AsteriskToken:
                    return MasmSyntaxKind.MultiplyExpression;
                case MasmSyntaxKind.SlashToken:
                    return MasmSyntaxKind.DivideExpression;
                case MasmSyntaxKind.PercentToken:
                case MasmSyntaxKind.ModKeyword:
                    return MasmSyntaxKind.ModuloExpression;
                default:
                    return MasmSyntaxKind.None;
            }
        }

        public static bool IsLiteralExpression(MasmSyntaxKind tokenKind) =>
            GetLiteralExpression(tokenKind) != MasmSyntaxKind.None;

        public static MasmSyntaxKind GetLiteralExpression(MasmSyntaxKind tokenKind)
        {
            return tokenKind switch
            {
                MasmSyntaxKind.NumericLiteralToken => MasmSyntaxKind.NumericLiteralExpression,
                MasmSyntaxKind.RegisterLiteralToken => MasmSyntaxKind.RegisterLiteralExpression,
                MasmSyntaxKind.DotToken => MasmSyntaxKind.CurrentIPExpression,

                _ => MasmSyntaxKind.None,
            };
        }

        public static bool IsKeyword(MasmSyntaxKind tokenKind)
        {
            switch (tokenKind)
            {
                case MasmSyntaxKind.AndKeyword:
                case MasmSyntaxKind.XorKeyword:
                case MasmSyntaxKind.OrKeyword:
                case MasmSyntaxKind.ModKeyword:
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
                    return true;

                default:
                    return false;
            }
        }
    }
}
