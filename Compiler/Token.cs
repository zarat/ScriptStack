using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptStack.Compiler
{

    /// <summary>
    /// Known types of Token
    /// </summary>
    public enum TokenType
    {
        Include,
        Shared,
        Var,
        LeftBrace,
        RightBrace,
        LeftParen,
        RightParen,
        LeftBracket,
        RightBracket,
        Period,
        Comma,
        SemiColon,
        Increment,
        Decrement,
        Plus,
        Minus,
        Multiply,
        Divide,
        Modulo,
        Assign,
        AssignPlus,
        AssignMinus,
        AssignMultiply,
        AssignDivide,
        AssignBinaryAnd, 
        AssignBinaryOr, 
        AssignXor, 
        AssignBinaryNot, 
        BinaryAnd, // &
        BinaryOr,  // |
        Xor,       // ^
        AssignModulo,
        And,
        Or,
        Not,
        Equal,
        NotEqual,
        Greater,
        GreaterEqual,
        Less,
        LessEqual, 
        If,
        Else,
        Switch,
        Case,
        Default,
        Colon,
        While,
        For,
        Foreach,
        In,
        Break,
        Continue,
        Function,
        Return,
        Identifier,
        Null,
        Integer,
        Float,
        Boolean,
        String,
        Char,
        Double,
        Decimal,
        ShiftLeft,
        ShiftRight,
        Run,
        Yield,
        Lock,
        Wait,
        Notify,
        BinaryNot // ~
    }

     public class SerializableToken
     {
         public TokenType Type { get; set; }
         public string Lexeme { get; set; }
         public int Line { get; set; }
         public int Column { get; set; }
         public string Text { get; set; }
     }

    /// <summary>
    /// A lexical token or simply token is a string with an assigned and thus identified meaning. 
    /// </summary>
    [Serializable]
    public class Token
    {

        #region Private Variables

        private TokenType tokenType;
        private object lexeme;
        private int line;
        private int column;
        private String text;

        #endregion

        #region Public Methods

        public Token(TokenType tokenType, object lexeme, int line, int column, String sourceLine)
        {
            this.tokenType = tokenType;
            this.lexeme = lexeme;
            this.line = line;
            this.column = Math.Max(0, column - lexeme.ToString().Length - 1);
            this.text = sourceLine;
        }

        public override string ToString()
        {
            return "Token(" + tokenType + ", \"" + lexeme.ToString() + "\")";
        }

        #endregion

        #region Public Methods

        public TokenType Type
        {
            get { return tokenType; }
        }

        public object Lexeme
        {
            get { return lexeme; }
        }

        public int Line
        {
            get { return line; }
        }

        public int Column
        {
            get { return column; }
        }

        public string Text
        {
            get { return text; }
        }

        #endregion

    }

}


