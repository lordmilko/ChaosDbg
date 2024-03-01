namespace ChaosDbg.Evaluator.Masm
{
    //https://learn.microsoft.com/en-us/windows-hardware/drivers/debuggercmds/masm-numbers-and-operators
    public enum MasmSyntaxKind
    {
        None = 0,

        IdentifierToken,
        ExclamationToken,
        NumericLiteralToken,
        RegisterLiteralToken,

        AsteriskToken, //*
        SlashToken, // /
        PercentToken, //%
        PlusToken, //+
        MinusToken, //-
        LessThanToken, //<
        LessThanLessThanToken, //<<
        GreaterThanToken, //>
        GreaterThanGreaterThanToken, //>>
        GreaterThanGreaterThanGreaterThanToken, //>>>
        EqualsToken, //=
        EqualsEqualsToken, //==
        LessThanEqualsToken, //<=
        GreaterThanEqualsToken, //>=
        ExclamationEqualsToken, //!=
        OpenParenToken, //(
        CloseParenToken, //)
        OpenBracketToken, //[
        CloseBracketToken, //]
        ColonToken, //:
        SemicolonToken,
        AmpersandToken, //&
        CaretToken, //^
        BarToken, //|
        DotToken, //.
        DollarToken, //$
        AtToken, //@

        AndKeyword, //and
        XorKeyword, //xor
        OrKeyword, //or
        ModKeyword, //mod

        NotKeyword, //not
        HiKeyword, //hi
        LowKeyword, //low
        ByKeyword, //by
        PhysicalByKeyword, //$ followed by pby
        WordKeyword, //wo
        PhysicalWordKeyword, //$ followed by pwo
        DoubleWordKeyword, //dwo
        PhysicalDoubleWordKeyword, //$ followed by pdwo
        QuadWordKeyword, //qwo
        PhysicalQuadWordKeyword, //$ followed by pqwo
        PoiKeyword, //poi
        PhysicalPoiKeyword, //$ followed by ppoi

        EndOfFileToken,

        PseudoFunctionExpression,
        SymbolIdentifierExpression,
        SymbolFunctionExpression,

        ParenthesizedExpression,

        UnaryPlusExpression,
        UnaryMinusExpression,

        NumericLiteralExpression,
        RegisterLiteralExpression,
        CurrentIPExpression,

        BitwiseOrExpression,
        ExclusiveOrExpression,
        BitwiseAndExpression,
        EqualsExpression,
        NotEqualsExpression,
        LessThanExpression,
        LessThanOrEqualExpression,
        GreaterThanExpression,
        GreaterThanOrEqualExpression,
        LeftShiftExpression,
        RightShiftExpression,
        UnsignedRightShiftExpression,
        AddExpression,
        SubtractExpression,
        MultiplyExpression,
        DivideExpression,
        ModuloExpression,
    }
}
