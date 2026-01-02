using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptStack.Compiler
{

    /// <summary>
    /// The lexical analyzer (Lexer) breaks code (written in sentences) into a series of known Token and pass the token stream to the <see cref="ScriptStack.Compiler.Parser"/>.
    /// 
    /// It is reading the source file character by character and removing any whitespace or comments.
    /// If the lexical analyzer finds a token invalid, it generates an error.
    /// More information is coming.
    /// 
    /// See https://en.wikipedia.org/wiki/Lexical_analysis
    /// 
    /// \todo binary not '~'
    /// \todo keyword "new"
    /// </summary>
    public class Lexer
    {

        #region Private Enumerated Types

        private enum State
        {
            None,
            Divide,
            InlineComment,
            BlockComment,
            Assign,
            Plus,
            Minus,
            Multiply,
            Xor,
            Modulo,
            And,
            Or,
            Not,
            Greater,
            Less,
            Identifier,
            String,
            EscapeString,
            Number,
            Float,
            Double,
            Decimal,
            Hex,
            Bin,
            Oct,
            Char,
            BinaryNot,
            // Multiline strings
            MultiLineString,
            MultiLineStringQuote1,
            MultiLineStringQuote2,
            EscapeMultiLineString
        }

        #endregion

        #region public variables

        public enum DefaultRealType
        {
            Decimal,
            Double,
            Float
        }

        public DefaultRealType DefaultReal { get; set; } = DefaultRealType.Decimal;

        #endregion

        #region Private Variables

        private List<string> lines;
        private int line;
        private int column;
        private State state;

        // Multiline strings
        private int stringStartLine;
        private int stringStartColumn;
        private string stringStartLineText;

        #endregion

        #region Private Methods

        private void InvalidCharacter(char ch)
        {
            throw new LexerException("Unerwartetes Zeichen '" + ch + "'.\n", line, column, lines[line]);
        }

        private bool EndOfSource
        {
            get { return line >= lines.Count; }
        }

        private char ReadChar()
        {

            if (EndOfSource)
                throw new LexerException("Das Ende des TokenStream wurde erreicht.");

            char ch = lines[line][column++];

            if (column >= lines[line].Length)
            {

                column = 0;

                ++line;

            }

            return ch;
        }

        private void UndoChar()
        {

            if (line == 0 && column == 0)
                throw new LexerException("Der Anfang des TokenStream wurde erreicht.");

            --column;

            if (column < 0)
            {

                --line;

                column = lines[line].Length - 1;

            }

        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Split a source string into lines that can be fed into the lexer.
        /// Normalizes line endings (\r\n and \r become \n).
        /// </summary>
        public static List<string> ToLines(string source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            // Normalize line endings so we can split reliably
            source = source.Replace("\r\n", "\n").Replace("\r", "\n");

            // Keep trailing empty lines (Split would otherwise drop them)
            var parts = source.Split('\n');
            return new List<string>(parts);
        }

        /// <summary>
        /// Convenience helper: tokenize a full source string.
        /// </summary>
        public static List<Token> Tokenize(string source, DefaultRealType defaultReal = DefaultRealType.Decimal)
        {
            var lexer = new Lexer(ToLines(source))
            {
                DefaultReal = defaultReal
            };

            return lexer.GetTokens();
        }

        public Lexer(List<string> lines)
        {

            this.lines = new List<string>();

            foreach (string line in lines)
                this.lines.Add(line + "\r\n");
           
            line = 0;

            column = 0;

            state = State.None;

        }

        private bool TryReadChar(out char ch)
        {
            if (EndOfSource)
            {
                ch = '\0';
                return false;
            }

            ch = ReadChar();
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<Token> GetTokens()
        {

            line = 0;

            column = 0;

            state = State.None;

            string lexeme = null;

            List<Token> tokenStream = new List<Token>();

            while (!EndOfSource)
            {

                string currentLine = lines[line];

                char ch = ReadChar();

                switch (state)
                {

                    case State.None:
                        switch (ch)
                        {

                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                                break;
                           
                            case '(':
                                tokenStream.Add(new Token(TokenType.LeftParen, "(", line, column, currentLine));
                                break;
                            case ')':
                                tokenStream.Add(new Token(TokenType.RightParen, ")", line, column, currentLine));
                                break;
                            case '[':
                                tokenStream.Add(new Token(TokenType.LeftBracket, "[", line, column, currentLine));
                                break;
                            case ']':
                                tokenStream.Add(new Token(TokenType.RightBracket, "]", line, column, currentLine));
                                break;
                            case '{':
                                tokenStream.Add(new Token(TokenType.LeftBrace, "{", line, column, currentLine));
                                break;
                            case '}':
                                tokenStream.Add(new Token(TokenType.RightBrace, "}", line, column, currentLine));
                                break;                               
                            case '.':
                                tokenStream.Add(new Token(TokenType.Period, ".", line, column, currentLine));
                                break;
                            case ':':
                                tokenStream.Add(new Token(TokenType.Colon, ":", line, column, currentLine));
                                break;
                            case ',':
                                tokenStream.Add(new Token(TokenType.Comma, ",", line, column, currentLine));
                                break;
                            case ';':
                                tokenStream.Add(new Token(TokenType.SemiColon, ";", line, column, currentLine));
                                break;


                            case '=':
                                state = State.Assign;
                                break;
                            case '+':
                                state = State.Plus;
                                break;
                            case '-':
                                state = State.Minus;
                                break;
                            case '*':
                                state = State.Multiply;
                                break;
                            case '/':
                                state = State.Divide;
                                break;
                            case '%':
                                state = State.Modulo;
                                break;
                            case '^':
                                state = State.Xor;
                                break;
                            case '&':
                                state = State.And;
                                break;
                            case '|':
                                state = State.Or;
                                break;
                            case '!':
                                state = State.Not;
                                break;
                            case '>':
                                state = State.Greater;
                                break;
                            case '<':
                                state = State.Less;
                                break;
                            case '\"':
                                {
                                    // Startposition sauber merken (wir sind gerade 1 Zeichen drüber)
                                    UndoChar();
                                    stringStartLine = line;
                                    stringStartColumn = column;
                                    stringStartLineText = lines[line];
                                    ReadChar(); // das " nochmal konsumieren

                                    lexeme = "";

                                    // Lookahead: """ ?
                                    if (!TryReadChar(out char n1))
                                    {
                                        // " am Ende der Datei => unterminiert
                                        state = State.String;
                                        break;
                                    }

                                    if (n1 == '\"')
                                    {
                                        if (!TryReadChar(out char n2))
                                        {
                                            // "" am Ende => leerer String
                                            tokenStream.Add(new Token(TokenType.String, "", stringStartLine, stringStartColumn, stringStartLineText));
                                            state = State.None;
                                            break;
                                        }

                                        if (n2 == '\"')
                                        {
                                            // """ => MultiLineString Start
                                            state = State.MultiLineString;
                                        }
                                        else
                                        {
                                            // "" => leerer String, n2 gehört schon zum nächsten Token
                                            UndoChar(); // n2 zurück
                                            tokenStream.Add(new Token(TokenType.String, "", stringStartLine, stringStartColumn, stringStartLineText));
                                            state = State.None;
                                        }
                                    }
                                    else
                                    {
                                        // normaler "..." String, n1 ist erstes Zeichen im String
                                        UndoChar(); // n1 zurück
                                        state = State.String;
                                    }

                                    break;
                                }
                            case '\'':
                                lexeme = "";
                                state = State.Char;
                                break;
                            case '~':
                                state = State.BinaryNot;
                                break;

                            default:
                                if (char.IsLetter(ch) || ch == '_')
                                {
                                    state = State.Identifier;
                                    lexeme = "" + ch;
                                }
                                else if (char.IsDigit(ch))
                                {
                                    lexeme = "" + ch;
                                    state = State.Number;
                                }
                                else
                                    InvalidCharacter(ch);
                                break;

                        }

                        break;

                    case State.BinaryNot:
                        if (ch == '=')
                        {
                            tokenStream.Add(new Token(TokenType.AssignBinaryNot, "~=", line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            tokenStream.Add(new Token(TokenType.BinaryNot, "~", line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Divide:
                        switch (ch)
                        {
                            case '/':
                                state = State.InlineComment;
                                break;
                            case '*':
                                state = State.BlockComment;
                                break;
                            case '=':
                                tokenStream.Add(new Token(TokenType.AssignDivide, "/=", line, column, currentLine));
                                state = State.None;
                                break;
                            default:
                                tokenStream.Add(new Token(TokenType.Divide, "/", line, column, currentLine));
                                UndoChar();
                                state = State.None;
                                break;
                        }
                        break;

                    case State.InlineComment:
                        // just read until a new line is encountered
                        if (ch == '\n')
                            state = State.None;
                        break;

                    case State.BlockComment:
                        if (ch == '*')
                        {

                            char next = ReadChar();

                            if (next == '/')
                            {
                                state = State.None;
                                break;
                            }

                        }
                        break;

                    case State.Assign:
                        if (ch == '=')
                        {
                            tokenStream.Add(new Token(TokenType.Equal, "==", line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            tokenStream.Add(new Token(TokenType.Assign, "=", line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Plus:
                        if (ch == '+')
                        {
                            tokenStream.Add(new Token(TokenType.Increment, "++", line, column, currentLine));
                            state = State.None;
                        }
                        else if (ch == '=')
                        {
                            tokenStream.Add(new Token(TokenType.AssignPlus, "+=", line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            tokenStream.Add(new Token(TokenType.Plus, "+", line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Minus:
                        if (ch == '-')
                        {
                            tokenStream.Add(new Token(TokenType.Decrement, "--", line, column, currentLine));
                            state = State.None;
                        }
                        else if (ch == '=')
                        {
                            tokenStream.Add(new Token(TokenType.AssignMinus, "-=", line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            tokenStream.Add(new Token(TokenType.Minus, "-", line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Multiply:
                        if (ch == '=')
                        {
                            tokenStream.Add(new Token(TokenType.AssignMultiply, "*=", line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            tokenStream.Add(new Token(TokenType.Multiply, "*", line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Xor:
                        if (ch == '=')
                        {
                            tokenStream.Add(new Token(TokenType.AssignXor, "^=", line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            // bitwise XOR '^'
                            tokenStream.Add(new Token(TokenType.Xor, "^", line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Modulo:
                        if (ch == '=')
                        {
                            tokenStream.Add(new Token(TokenType.AssignModulo, "%=", line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            tokenStream.Add(new Token(TokenType.Modulo, "%", line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.And:
                        if (ch == '&')
                        {
                            tokenStream.Add(new Token(TokenType.And, "&&", line, column, currentLine));
                            state = State.None;
                        }
                        else if (ch == '=')
                        {
                            tokenStream.Add(new Token(TokenType.AssignBinaryAnd, "&=", line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            // bitwise AND '&'
                            tokenStream.Add(new Token(TokenType.BinaryAnd, "&", line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Or:
                        if (ch == '|')
                        {
                            tokenStream.Add(new Token(TokenType.Or, "||", line, column, currentLine));
                            state = State.None;
                        }
                        else if (ch == '=')
                        {
                            tokenStream.Add(new Token(TokenType.AssignBinaryOr, "|=", line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            // bitwise OR '|'
                            tokenStream.Add(new Token(TokenType.BinaryOr, "|", line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Not:
                        if (ch == '=')
                        {
                            tokenStream.Add(new Token(TokenType.NotEqual, "!=", line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            tokenStream.Add(new Token(TokenType.Not, "!", line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Greater:
                        if (ch == '=')
                        {
                            tokenStream.Add(new Token(TokenType.GreaterEqual, ">=", line, column, currentLine));
                            state = State.None;
                        }
                        else if (ch == '>')
                        {
                            tokenStream.Add(new Token(TokenType.ShiftRight, ">>", line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            tokenStream.Add(new Token(TokenType.Greater, ">", line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Less:
                        if (ch == '=')
                        {
                            tokenStream.Add(new Token(TokenType.LessEqual, "<=", line, column, currentLine));
                            state = State.None;
                        }
                        else if (ch == '<')
                        {
                            tokenStream.Add(new Token(TokenType.ShiftLeft, "<<", line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            tokenStream.Add(new Token(TokenType.Less, "<", line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Identifier:

                        if (char.IsLetterOrDigit(ch) || ch == '_')
                            lexeme += ch;

                        else
                        {

                            TokenType tokenType;

                            if (lexeme == "null")
                                tokenType = TokenType.Null;
                            else if (lexeme == "true" || lexeme == "false")
                                tokenType = TokenType.Boolean;
                            else if (lexeme == "if")
                                tokenType = TokenType.If;
                            else if (lexeme == "else")
                                tokenType = TokenType.Else;
                            else if (lexeme == "while")
                                tokenType = TokenType.While;
                            else if (lexeme == "for")
                                tokenType = TokenType.For;
                            else if (lexeme == "foreach")
                                tokenType = TokenType.Foreach;
                            else if (lexeme == "in")
                                tokenType = TokenType.In;
                            else if (lexeme == "switch")
                                tokenType = TokenType.Switch;
                            else if (lexeme == "case")
                                tokenType = TokenType.Case;
                            else if (lexeme == "default")
                                tokenType = TokenType.Default;
                            else if (lexeme == "break")
                                tokenType = TokenType.Break;
                            else if (lexeme == "continue")
                                tokenType = TokenType.Continue;
                            else if (lexeme == "function")
                                tokenType = TokenType.Function;
                            else if (lexeme == "return")
                                tokenType = TokenType.Return;

                            else if (lexeme == "shared")
                                tokenType = TokenType.Shared;
                            else if (lexeme == "var")
                                tokenType = TokenType.Var;

                            else if (lexeme == "include")
                                tokenType = TokenType.Include;
                            else if (lexeme == "lock")
                                tokenType = TokenType.Lock;
                            else if (lexeme == "run")
                                tokenType = TokenType.Run;
                            else if (lexeme == "yield")
                                tokenType = TokenType.Yield;
                            else if (lexeme == "notify")
                                tokenType = TokenType.Notify;
                            else if (lexeme == "wait")
                                tokenType = TokenType.Wait;
                            else
                                tokenType = TokenType.Identifier;

                            if (tokenType == TokenType.Boolean)
                            {

                                bool val = false;

                                if (lexeme == "true")
                                    val = true;

                                tokenStream.Add(new Token(tokenType, val, line, column, currentLine));

                            }

                            else
                                tokenStream.Add(new Token(tokenType, lexeme, line, column, currentLine));

                            UndoChar();

                            state = State.None;

                        }
                        break;

                    case State.Char:
                        /* \Todo */
                        while (ch != '\'')
                        {
                            lexeme += ch;
                            ch = ReadChar();
                        }
                        if (ch == '\'')
                        {
                            char c;
                            if (lexeme == "\\n") c = '\n';
                            else if (lexeme == "\\t") c = '\t';
                            else if (lexeme == "\\b") c = '\b';
                            else if (lexeme == "\\r") c = '\r';
                            else if (lexeme == "\\f") c = '\f';
                            else if (lexeme == "\\\'") c = '\'';
                            else if (lexeme == "\\\"") c = '\"';
                            else if (lexeme == "\\\\") c = '\\';
                            else c = char.Parse(lexeme);
                            tokenStream.Add(new Token(TokenType.Char, c, line, column, currentLine));
                            state = State.None;
                        }
                        else
                            throw new LexerException("Ein 'Character' darf genau ein Zeichen lang sein - ausgenommen Steuerzeichen!", line, column, lines[line]);
                        break;

                    case State.String:
                        if (ch == '\"') // string is ready!
                        {
                            tokenStream.Add(new Token(TokenType.String, lexeme, line, column, currentLine));
                            state = State.None;
                        }
                        else if (ch == '\\') // escape character, start string escape
                        {
                            state = State.EscapeString;
                        }
                        else if (ch == '\r' || ch == '\n') // if there is actually a line break inside the string..
                        {
                            throw new LexerException("Ein String darf sich nicht auf mehrere Zeilen erstrecken.", line, column, lines[line]);
                        }
                        else // just add the character
                        {
                            lexeme += ch;
                        }
                        break;

                    case State.EscapeString:
                        /*
                         * Always return to TokenState.String because we are inside a string!
                         */
                        if (ch == '"')
                        {
                            lexeme += '\"';
                            state = State.String;
                        }
                        else if (ch == '\\')
                        {
                            lexeme += ch;
                            state = State.String;
                        }
                        else if (ch == 'n')
                        {
                            lexeme += '\n';
                            state = State.String;
                        }
                        else if (ch == 't')
                        {
                            lexeme += '\t';
                            state = State.String;
                        }
                        else if (ch == 'r')
                        {
                            lexeme += '\r';
                            state = State.String;
                        }
                        else if (ch == 'n')
                        {
                            lexeme += '\n';
                            state = State.String;
                        }
                        else if (ch == 'b')
                        {
                            lexeme += '\b';
                            state = State.String;
                        }
                        else
                            throw new LexerException("Das Escapezeichen '\\" + ch + "' kann in Strings nicht verarbeitet werden.", line, column, lines[line]);
                        
                        break;

                    case State.MultiLineString:
                        if (ch == '\"')
                        {
                            state = State.MultiLineStringQuote1;
                        }
                        else if (ch == '\\')
                        {
                            state = State.EscapeMultiLineString;
                        }
                        else if (ch == '\r')
                        {
                            // ignorieren (du hast \r\n in den lines)
                        }
                        else
                        {
                            // inkl. '\n' erlaubt
                            lexeme += ch;
                        }
                        break;

                    case State.MultiLineStringQuote1:
                        if (ch == '\"')
                        {
                            state = State.MultiLineStringQuote2;
                        }
                        else
                        {
                            // war kein Abschluss, nur ein "
                            lexeme += '\"';
                            if (ch != '\r') lexeme += ch;
                            state = State.MultiLineString;
                        }
                        break;

                    case State.MultiLineStringQuote2:
                        if (ch == '\"')
                        {
                            // """ beendet
                            tokenStream.Add(new Token(TokenType.String, lexeme, stringStartLine, stringStartColumn, stringStartLineText));
                            state = State.None;
                        }
                        else
                        {
                            // war kein Abschluss, nur ""
                            lexeme += "\"\"";
                            if (ch != '\r') lexeme += ch;
                            state = State.MultiLineString;
                        }
                        break;

                    case State.EscapeMultiLineString:
                        // gleiche Escape-Logik wie bei normalen Strings, aber Rückkehr zu MultiLineString
                        if (ch == '"') { lexeme += '\"'; state = State.MultiLineString; }
                        else if (ch == '\\') { lexeme += '\\'; state = State.MultiLineString; }
                        else if (ch == 'n') { lexeme += '\n'; state = State.MultiLineString; }
                        else if (ch == 't') { lexeme += '\t'; state = State.MultiLineString; }
                        else if (ch == 'r') { lexeme += '\r'; state = State.MultiLineString; }
                        else if (ch == 'b') { lexeme += '\b'; state = State.MultiLineString; }
                        else
                            throw new LexerException("Das Escapezeichen '\\" + ch + "' kann in Strings nicht verarbeitet werden.", line, column, lines[line]);
                        break;

                    case State.Number:
                        /*
                         * In the phase of lexing numbers are also strings
                         * They are casted later on
                         */
                        if (char.IsDigit(ch))
                            lexeme += ch;
                        else if (ch == '.') // culture?!?
                        {
                            lexeme += '.';
                            switch (DefaultReal)
                            {
                                case DefaultRealType.Float:
                                    state = State.Float;
                                    break;
                                case DefaultRealType.Double:
                                    state = State.Double;
                                    break;
                                case DefaultRealType.Decimal:
                                default:
                                    state = State.Decimal;
                                    break;
                            }
                        }
                        else if (ch == 'x')
                        {
                            lexeme += ch;
                            state = State.Hex;
                        }
                        else if (ch == 'b')
                        {
                            int intValue = Convert.ToInt32(lexeme, 2);
                            // \todo in fact this is a 32 bit integer
                            tokenStream.Add(new Token(TokenType.Integer, intValue, line, column, currentLine));
                            state = State.None;
                        }
                        else if (ch == 'o')
                        {
                            int intValue = Convert.ToInt32(lexeme, 8);
                            tokenStream.Add(new Token(TokenType.Integer, intValue, line, column, currentLine));
                            state = State.None;
                        }
                        else
                        {
                            int intValue = int.Parse(lexeme);
                            tokenStream.Add(new Token(TokenType.Integer, intValue, line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Float:
                        if (char.IsDigit(ch))
                            lexeme += ch;
                        else
                        {
                            float floatValue = float.Parse(lexeme, System.Globalization.CultureInfo.InvariantCulture);
                            tokenStream.Add(new Token(TokenType.Float, floatValue, line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Double:
                        if (char.IsDigit(ch))
                            lexeme += ch;
                        else
                        {
                            double doubleValue = double.Parse(lexeme, System.Globalization.CultureInfo.InvariantCulture);
                            tokenStream.Add(new Token(TokenType.Double, doubleValue, line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Decimal:
                        if (char.IsDigit(ch))
                            lexeme += ch;
                        else if (ch == 'f')
                            state = State.Float;
                        else if (ch == 'd')
                            state = State.Double;
                        else
                        {
                            decimal decimalValue = decimal.Parse(lexeme, System.Globalization.CultureInfo.InvariantCulture);
                            tokenStream.Add(new Token(TokenType.Decimal, decimalValue, line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    case State.Hex:
                        if (char.IsDigit(ch) || char.IsLetter(ch))
                        {
                            if (char.IsLetter(ch) && !(ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F'))
                                throw new LexerException("Ein hexadezimaler Wert darf ausser Zahlen nur Buchstaben von 'a' - 'f' bzw. 'A' - 'F' enthalten.", line, column, currentLine);
                            lexeme += ch;
                        }
                        else
                        {
                            int intValue = Convert.ToInt32(lexeme, 16);
                            tokenStream.Add(new Token(TokenType.Integer, intValue, line, column, currentLine));
                            UndoChar();
                            state = State.None;
                        }
                        break;

                    default:
                        throw new LexerException("Unbekannter Lexer Status '" + state + "'.");

                }

            }

            if (state != State.None)
                throw new LexerException("Unerwartetes Ende des TokenStream.");         

            return tokenStream;

        }

        #endregion

    }

}
