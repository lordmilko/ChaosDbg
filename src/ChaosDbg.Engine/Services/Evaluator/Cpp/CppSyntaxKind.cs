namespace ChaosDbg.Evaluator.Cpp
{
    public enum CppSyntaxKind
    {
        #region Punctuation

        TildeToken, //~
        ExclamationToken, //!
        PercentToken, //%
        CaretToken, //^
        AmpersandToken, //&
        AsteriskToken, //*
        OpenParenToken, //(
        CloseParenToken, //)
        MinusToken, //-
        PlusToken, //+
        EqualsToken, //=
        OpenBraceToken, //{
        CloseBraceToken, //}
        OpenBracketToken, //[
        CloseBracketToken, //]
        BarToken, //|
        BackslashToken, //\
        ColonToken, //:
        SemicolonToken, //;
        DoubleQuoteToken, //"
        SingleQuoteToken, //'
        LessThanToken, //<
        CommaToken, //,
        GreaterThanToken, //>
        DotToken, //.
        SlashToken, // /
        QuestionToken, //?
        HashToken, //#

        #endregion
        #region Compound Punctuation

        BarBarToken, //||
        AmpersandAmpersandToken, //&&
        MinusMinusToken, //--
        PlusPlusToken, //++
        ColonColonToken, //::
        MinusGreaterThanToken, //->
        ExclamationEqualsToken, //!=
        EqualsEqualsToken, //==
        LessThanEqualsToken, //<=
        LessThanLessThanToken, //<<
        LessThanLessThanEqualsToken, //<<=
        GreaterThanEqualsToken, //>=
        GreaterThanGreaterThanToken, //>>
        GreaterThanGreaterThanEqualsToken, //>>=
        SlashEqualsToken, // /=
        AsteriskEqualsToken, //*=
        BarEqualsToken, //|=
        AmpersandEqualsToken, //&=
        PlusEqualsToken, //+=
        MinusEqualsToken, //-=
        CaretEqualsToken, //^=
        PercentEqualsToken, //%=

        #endregion,

        EndOfFileToken,

        BadToken,
        IdentifierToken,
        NumericLiteralToken,
        CharacterLiteralToken,
        StringLiteralToken,

        EndOfLineTrivia,
        WhitespaceTrivia,
        DirectiveTrivia, //Model any and all directives under a single directive type
        SingleLineCommentTrivia,
        MultiLineCommentTrivia,
    }
}
