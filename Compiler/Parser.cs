using System;
using System.Collections.Generic;
using System.Text;

using ScriptStack;
using ScriptStack.Runtime;

namespace ScriptStack.Compiler
{

    /// <summary>
    /// The parser builds an <see cref="ScriptStack.Runtime.Executable"/> out of the Token stream returned from the <see cref="ScriptStack.Compiler.Lexer"/> while checking for correct syntax.
    /// 
    /// The parser takes the output from the Lexer in the form of a Token stream and matches it against syntax rules to detect any errors.
    /// The output is an abstract syntax tree in form of Instruction's which can be executed by the <see cref="ScriptStack.Runtime.Interpreter"/>. More details are coming soon.
    /// 
    /// Not all methods are well documented yet but please be patient - i am working on it.
    /// 
    /// See https://en.wikipedia.org/wiki/Parsing
    /// </summary>
    public class Parser
    {

        #region Private Structs

        private struct Variable
        {

            public string name;
            public Scope scope;
            public Type derivatedType;

            public Variable(string name, Scope scope, Type derivatedType)
            {

                this.name = name;

                this.scope = scope;

                this.derivatedType = derivatedType;

            }

        }

        private struct FunctionDescriptor
        {
            public string name;
            public uint parameterCount;
            public Instruction instruction;
        }

        private struct LoopControl
        {
            public Instruction Break;
            public Instruction Continue;
        }

        #endregion

        #region Private Variables

        private Script script;
        private bool debugMode;
        private List<Token> tokenStream;
        private int nextToken;
        private Dictionary<string, bool> variables;
        private Dictionary<string, bool> localVariables;
        private int functionFrameIndex;
        private Dictionary<Instruction, FunctionDescriptor> forwardDeclarations;
        private Stack<LoopControl> loopControl;
        private Derivation derivation;
        private Executable executable;

        #endregion

        #region Private Methods

        /// <summary>
        /// Check if there are more tokens available
        /// </summary>
        /// <returns></returns>
        private bool More()
        {
            return nextToken < tokenStream.Count;
        }

        /// <summary>
        /// Get the next available token
        /// </summary>
        /// <returns></returns>
        private Token ReadToken()
        {

            if (!More())
                throw new ParserException("Es sind keine weiteren Token vorhanden.");

            return tokenStream[nextToken++];

        }

        /// <summary>
        /// Get the next available token without actually increasing the tokenstream index
        /// </summary>
        /// <returns></returns>
        private Token LookAhead()
        {

            if (!More())
                throw new ParserException("Es sind keine weiteren Token vorhanden.");

            return tokenStream[nextToken];

        }

        /// <summary>
        /// Get the token 'n' steps forward without actually increasing the tokenstream index
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        private Token LookAhead(int i)
        {

            if (!More() || null == tokenStream[nextToken + i] )
                throw new ParserException("Es sind keine weiteren Token vorhanden.");

            return tokenStream[nextToken + i];

        }

        /// <summary>
        /// If you read a token wrong, push it back so the stream stays intact
        /// </summary>
        private void UndoToken()
        {

            if (nextToken <= 0)
                throw new ParserException("Es sind keine vorangehenden Token mehr vorhanden.");

            --nextToken;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        private bool AssignmentOperator(TokenType tokenType)
        {

            switch (tokenType)
            {

                case TokenType.Assign:
                case TokenType.AssignPlus:
                case TokenType.AssignMinus:
                case TokenType.AssignMultiply:
                case TokenType.AssignDivide:
                case TokenType.AssignBinaryAnd:
                case TokenType.AssignBinaryOr:
                case TokenType.AssignXor:
                case TokenType.AssignBinaryNot:
                case TokenType.AssignModulo:
                    return true;

                default:
                    return false;

            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        private OpCode AssignmentOpcode(TokenType tokenType)
        {

            switch (tokenType)
            {

                case TokenType.Assign: return OpCode.MOV;
                case TokenType.AssignPlus: return OpCode.ADD;
                case TokenType.AssignMinus: return OpCode.SUB;
                case TokenType.AssignMultiply: return OpCode.MUL;
                case TokenType.AssignDivide: return OpCode.DIV;
                case TokenType.AssignBinaryAnd: return OpCode.ANDB;
                case TokenType.AssignBinaryOr: return OpCode.ORB;
                case TokenType.AssignXor: return OpCode.XOR;
                case TokenType.AssignBinaryNot: return OpCode.NOTB;
                case TokenType.AssignModulo: return OpCode.MOD;
                default:
                    throw new ParserException("Der Token '" + tokenType + "' ist kein Zuweisungsoperator.");

            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        private bool RelationalOperator(TokenType tokenType)
        {

            switch (tokenType)
            {

                case TokenType.Equal:
                case TokenType.NotEqual:
                case TokenType.Greater:
                case TokenType.GreaterEqual:
                case TokenType.Less:
                case TokenType.LessEqual:
                    return true;

                default:
                    return false;

            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        private OpCode RelationalOpcode(TokenType tokenType)
        {

            switch (tokenType)
            {

                case TokenType.Equal: return OpCode.CEQ;
                case TokenType.NotEqual: return OpCode.CNE;
                case TokenType.Greater: return OpCode.CG;
                case TokenType.GreaterEqual: return OpCode.CGE;
                case TokenType.Less: return OpCode.CL;
                case TokenType.LessEqual: return OpCode.CLE;
                default:
                    throw new ParserException("Der Token '" + tokenType + "' ist kein relationaler Operator.");

            }

        }

        /// <summary>
        /// Get the literal type of a token
        /// </summary>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        /// \todo float <> double
        private Type Literal(TokenType tokenType)
        {

            switch (tokenType)
            {

                case TokenType.Integer:
                    return typeof(int);
                case TokenType.Float:
                    return typeof(float);
                case TokenType.Boolean:
                    return typeof(bool);
                case TokenType.String:
                    return typeof(string);
                case TokenType.Char:
                    return typeof(char);
                case TokenType.Double:
                    return typeof(double);
                case TokenType.Decimal:
                    return typeof(decimal);
                default:
                    throw new ParserException( "Der Token '" + tokenType.ToString() + "' kann keinem Literal zugeordnet werden.");

            }

        }

        /// <summary>
        /// Get the resulting type of two computed types
        /// </summary>
        /// <param name="token"></param>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        private Type Derivate(Token token, Type first, Type second)
        {

            return derivation.Derivate(token, first, second);

        }

        /// <summary>
        /// Allocate a local variable
        /// </summary>
        /// <param name="identifier"></param>
        private void AllocateVariable(string identifier)
        {

            if (variables.ContainsKey(identifier))
                throw new ParserException("Die Variable '" + identifier + "' ist in einem umschliessenden Bereich bereits deklariert.");

            if (localVariables.ContainsKey(identifier))
                throw new ParserException("Die Variable '" + identifier + "' ist in diesem Bereich bereits deklariert.");

            localVariables[identifier] = true;

            executable.InstructionsInternal.Add(new Instruction(OpCode.DB, Operand.Variable(identifier)));

        }

        /// <summary>
        /// Increase the function frame index 
        /// </summary>
        private void AllocateFunctionFrame()
        {

            functionFrameIndex++;

        }

        /// <summary>
        /// Add a temporary variable to the current function frames local memory
        /// </summary>
        /// <returns></returns>
        /// \todo performance?
        private string AllocateTemporaryVariable()
        {

            int index = 0;

            while (true)
            {

                string identifier = "[" + functionFrameIndex + ":" + index + "]";

                if (!localVariables.ContainsKey(identifier) && !variables.ContainsKey(identifier))
                {

                    localVariables[identifier] = true;

                    return identifier;

                }

                ++index;

            }

        }

        /// <summary>
        /// Decrease the function frame index
        /// </summary>
        private void FreeFunctionFrame()
        {

            if (functionFrameIndex == 0)
                return;

            List<string> candidates = new List<string>();

            foreach (string identifier in localVariables.Keys)
                if (identifier.StartsWith("[" + functionFrameIndex + ":"))
                    candidates.Add(identifier);

            foreach (string identifier in candidates)
                localVariables.Remove(identifier);

            --functionFrameIndex;

        }

        /// <summary>
        /// 
        /// </summary>
        private void ReadSemicolon()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.SemiColon)
                throw new ParserException("Semicolon ';' erwartet.", token);

        }
        
        /// <summary>
        /// 
        /// </summary>
        private void ReadComma()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Comma)
                throw new ParserException("Comma ',' erwartet.", token);

        }

        /// <summary>
        /// 
        /// </summary>
        private void ReadLeftParenthesis()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.LeftParen)
                throw new ParserException("Klammer '(' erwartet.", token);

        }

        /// <summary>
        /// 
        /// </summary>
        private void ReadRightParenthesis()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.RightParen)
                throw new ParserException("Klammer ')' erwartet.", token);

        }

        /// <summary>
        /// 
        /// </summary>
        private void ReadLeftBrace()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.LeftBrace)
                throw new ParserException("Geschwungene Klammer '{' erwartet.", token);

        }

        /// <summary>
        /// 
        /// </summary>
        private void ReadRightBrace()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.RightBrace)
                throw new ParserException("Geschwungene Klammer '}' erwartet.", token);

        }

        /// <summary>
        /// 
        /// </summary>
        private void ReadLeftBracket()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.LeftBracket)
                throw new ParserException("Eckige Klammer '[' erwartet.", token);

        }

        /// <summary>
        /// 
        /// </summary>
        private void ReadRightBracket()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.RightBracket)
                throw new ParserException("Eckige Klammer ']' erwartet.", token);

        }

        /// <summary>
        /// 
        /// </summary>
        private void ReadPeriod()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Period)
                throw new ParserException("Punkt '.' erwartet.", token);

        }

        private void InsertDebugInfo(Token token)
        {

            if (!debugMode)
                return;

            string text = token.Text;

            //text = text.Replace("\r", "").Replace("\n", "");

            executable.InstructionsInternal.Add(new Instruction(OpCode.DBG, Operand.Literal(token.Line), Operand.Literal(text)));

        }

        /// <summary>
        /// Read a new (previously NOT declared) identifier
        /// </summary>
        /// <returns></returns>
        private string ReadIdentifier()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Identifier)
                throw new ParserException("Ein Keyword oder eine Variable wurde erwartet.", token);

            return token.Lexeme.ToString();

        }

        /// <summary>
        /// Read an expected (previously declared) identifier
        /// </summary>
        /// <returns></returns>
        private string ExpectIdentifier()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Identifier)
                throw new ParserException("Ein Keyword oder eine Variable wurde erwartet.", token);

            string identifier = token.Lexeme.ToString();

            if (!variables.ContainsKey(identifier) && !localVariables.ContainsKey(identifier))
                throw new ParserException("Ein nicht vorhandener Identifier '" + identifier + "' wurde referenziert.", token);

            return identifier;

        }

        /// <summary>
        /// Shared or local variable declaration
        /// </summary>
        private void VariableDeclaration()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Shared && token.Type != TokenType.Var)
                throw new ParserException( "Variablen werden mit 'shared' oder 'var' deklariert.", token);

            InsertDebugInfo(token);

            OpCode opcode = OpCode.DB;

            if(token.Type == TokenType.Shared)
                opcode = OpCode.DSB;

            string identifier = ReadIdentifier();

            while (true)
            {

                if (variables.ContainsKey(identifier))
                    throw new ParserException( "Die Variable '" + identifier + "' ist bereits vorhanden.", token);

                variables[identifier] = true;

                executable.InstructionsInternal.Add(new Instruction(opcode, Operand.Variable(identifier)));

                if (opcode == OpCode.DSB)
                    script.Manager.SharedMemory[identifier] = NullReference.Instance;

                else
                    executable.ScriptMemory[identifier] = NullReference.Instance;

                token = ReadToken();

                if (token.Type == TokenType.SemiColon)
                    return;

                if (token.Type != TokenType.Comma)
                    throw new ParserException("Comma ',' erwartet.", token);

                identifier = ReadIdentifier();

            }

        }

        /// <summary>
        /// In fact a struct by now is just an array with pre defined member names. It is stored at the scripts local memory.
        /// 
        /// \todo struct is in beta
        /// </summary>
        private Variable StructDeclaration()
        {

            AllocateFunctionFrame();

            Token token = ReadToken();

            if (token.Type != TokenType.Struct)
                throw new ParserException("Strukturen werden mit 'struct' deklariert.", token);

            InsertDebugInfo(token);

            string identifier = ReadIdentifier();

            executable.InstructionsInternal.Add(new Instruction(OpCode.DCO, Operand.Variable(identifier)));

            string alloc = AllocateTemporaryVariable();

            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(alloc), Operand.Variable(identifier)));

            variables[identifier] = true;

            ArrayList array = new ArrayList();

            ReadLeftBrace();

            int i = 0;

            while (true)
            {

                Token tok = ReadToken();

                if (tok.Type == TokenType.RightBrace)
                {

                    UndoToken();
                    break;

                }

                if (LookAhead().Type == TokenType.RightBrace)
                {

                    executable.InstructionsInternal.Add(new Instruction(OpCode.ADD, Operand.CreatePointer(identifier, (string)tok.Lexeme), Operand.Literal(0)));
                    array.Add(tok.Lexeme, 0);
                    break;

                }

                else if (LookAhead().Type == TokenType.Colon)
                {

                    ReadToken();

                    Token tmpToken = ReadToken();

                    executable.InstructionsInternal.Add(new Instruction(OpCode.ADD, Operand.CreatePointer(identifier, (string)tok.Lexeme), Operand.Variable(tmpToken.Lexeme.ToString())));

                    array.Add(tok.Lexeme, tmpToken.Lexeme);

                }

                else
                {

                    executable.InstructionsInternal.Add(new Instruction(OpCode.ADD, Operand.CreatePointer(identifier, (string)tok.Lexeme), Operand.Literal(0)));

                    array.Add(tok.Lexeme, 0);

                }

                if (LookAhead().Type == TokenType.RightBrace)
                    break;

                ReadComma();

                i++;

            }

            ReadRightBrace();

            executable.ScriptMemory[identifier] = array;

            FreeFunctionFrame();

            return new Variable(identifier, Scope.Local, typeof(ArrayList));

        }

        /// <summary>
        /// Enumeration stored in scripts local memory
        /// 
        /// \todo Enum is in beta
        /// </summary>
        /// <returns></returns>
        private Variable EnumDeclaration()
        {

            AllocateFunctionFrame();

            Token token = ReadToken();

            if (token.Type != TokenType.Enum)
                throw new ParserException("Enumerationen werden mit 'enum' deklariert.", token);

            InsertDebugInfo(token);

            string identifier = ReadIdentifier();

            executable.InstructionsInternal.Add(new Instruction(OpCode.DCO, Operand.Variable(identifier)));

            string alloc = AllocateTemporaryVariable();

            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(alloc), Operand.Variable(identifier)));

            variables[identifier] = true;

            ArrayList array = new ArrayList();

            ReadLeftBrace();

            int i = 0;

            while (true)
            {

                Token tok = ReadToken();

                if (tok.Type == TokenType.RightBrace)
                {

                    UndoToken();
                    break;

                }

                if (LookAhead().Type == TokenType.RightBrace)
                {

                    executable.InstructionsInternal.Add(new Instruction(OpCode.ADD, Operand.CreatePointer(identifier, (string)tok.Lexeme), Operand.Literal(i)));
                    array.Add(tok.Lexeme, i);
                    break;

                }
                else
                {

                    executable.InstructionsInternal.Add(new Instruction(OpCode.ADD, Operand.CreatePointer(identifier, (string)tok.Lexeme), Operand.Literal(i)));

                    array.Add(tok.Lexeme, i);

                }

                if (LookAhead().Type == TokenType.RightBrace)
                    break;

                ReadComma();

                i++;

            }

            ReadRightBrace();

            executable.ScriptMemory[identifier] = array;

            FreeFunctionFrame();

            return new Variable(identifier, Scope.Local, typeof(ArrayList));

        }
        
        /// <summary>
        /// 
        /// </summary>
        private void LocalVariableDeclaration()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Var)
                throw new ParserException( "Lokale Variablen werden mit 'var' deklariert.", token);

            string identifier = ReadIdentifier();

            while (true)
            {

                AllocateVariable(identifier);

                token = ReadToken();

                if (token.Type == TokenType.SemiColon)
                    return;

                if (token.Type == TokenType.Assign)
                {

                    UndoToken();

                    UndoToken();

                    Assignment();

                    token = ReadToken();

                    if (token.Type == TokenType.SemiColon)
                        return;

                }

                if (token.Type != TokenType.Comma)
                    throw new ParserException( "Comma ',' erwartet.", token);

                identifier = ReadIdentifier();

            }

        }

        /// <summary>
        /// 
        /// </summary>
        private void Run()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Run)
                throw new ParserException("Keyword 'run' erwartet.", token);

            FunctionCall(true);

            ReadSemicolon();

        }

        /// <summary>
        /// 
        /// \todo Run routines in background? bad for cloud
        /// </summary>
        /// <returns></returns>
        private Variable RoutineCall()
        {

            string identifier = ReadIdentifier();

            ReadLeftParenthesis();

            List<object> parameters = new List<object>();

            int parameterCount = 0;

            if (LookAhead().Type != TokenType.RightParen)
            {

                while (true)
                {

                    Variable parameter = Expression();

                    executable.InstructionsInternal.Add(new Instruction( OpCode.PUSH, Operand.Variable(parameter.name)));

                    ++parameterCount;

                    if (LookAhead().Type == TokenType.RightParen)
                        break;

                    else
                        ReadComma();

                }

            }

            ReadRightParenthesis();

            Manager manager = executable.Script.Manager;

            if (!manager.Routines.ContainsKey(identifier))
                throw new ParserException("Die Routine '" + identifier + "' ist nicht vorhanden.");

            Routine routine = manager.Routines[identifier];

            if (routine.ParameterTypes.Count > parameterCount)
                throw new ParserException("Der Aufruf der Routine '" + identifier + "' hat fehlende Parameter. Erwartet werden " + routine.ParameterTypes.Count + " Parameter.\nBeschreibung der Routine: " + routine.Description().ToString());

            if (routine.ParameterTypes.Count < parameterCount)
                throw new ParserException("Der Aufruf der Routine '" + identifier + "' hat zu viele Parameter. Erwartet werden " + routine.ParameterTypes.Count + " Parameter.\nBeschreibung der Routine: " + routine.Description().ToString());

            executable.InstructionsInternal.Add(new Instruction(OpCode.INV, Operand.AllocateRoutinePointer(routine)));

            Variable variable = new Variable
            {
                name = AllocateTemporaryVariable(),
                scope = Scope.Local,
                derivatedType = null
            };

            executable.InstructionsInternal.Add(new Instruction(OpCode.POP, Operand.Variable(variable.name)));

            return variable;

        }
        
        /// <summary>
        /// Array access
        /// 
        /// like
        /// 
        /// ```
        /// var arr = [1, 2, 3];
        /// print(arr[1]); // the brackets are the array access
        /// ```
        /// 
        /// \todo rename this! its not a pointer
        /// </summary>
        /// <returns></returns>
        private Variable Pointer()
        {

            string identifier = ReadIdentifier();

            string tmp = null;

            while (LookAhead().Type == TokenType.LeftBracket)
            {

                ReadLeftBracket();

                Variable index = Expression();

                ReadRightBracket();

                tmp = AllocateTemporaryVariable();

                executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(tmp), Operand.CreatePointer(identifier, index.name)));

                identifier = tmp;

            }

            return new Variable(tmp, Scope.Local, null);

        }
        
        /// <summary>
        /// Member access
        /// </summary>
        /// <returns></returns>
        private Variable Member()
        {

            string arrayIdentifier = ReadIdentifier();

            string tmp = null;

            while (LookAhead().Type == TokenType.Period)
            {

                ReadPeriod();

                string member = ReadIdentifier();

                tmp = AllocateTemporaryVariable();

                executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(tmp), Operand.MemberVariable(arrayIdentifier, member)));

                arrayIdentifier = tmp;

            }

            return new Variable(tmp, Scope.Local, null);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable PreIncrement()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Increment)
                throw new ParserException( "Pre-increment '++' erwartet.", token);

            string identifier = ExpectIdentifier();

            executable.InstructionsInternal.Add(new Instruction(OpCode.INC, Operand.Variable(identifier)));

            return new Variable(identifier, Scope.Local, null);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable PreDecrement()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Decrement)
                throw new ParserException("Pre-decrement '--' erwartet.", token);

            string identifier = ExpectIdentifier();

            executable.InstructionsInternal.Add(new Instruction(OpCode.DEC, Operand.Variable(identifier)));

            return new Variable(identifier, Scope.Local, null);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable PostIncrement()
        {

            string identifier = ExpectIdentifier();

            Token token = ReadToken();

            if (token.Type != TokenType.Increment)
                throw new ParserException("Post-increment '++' erwartet.", token);

            string tmp = AllocateTemporaryVariable();

            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(tmp), Operand.Variable(identifier)));

            executable.InstructionsInternal.Add(new Instruction(OpCode.INC, Operand.Variable(identifier)));

            return new Variable(tmp, Scope.Local, null);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable PostDecrement()
        {

            string identifier = ExpectIdentifier();

            Token token = ReadToken();

            if (token.Type != TokenType.Decrement)
                throw new ParserException("Post-decrement '--' erwartet.", token);

            string tmp = AllocateTemporaryVariable();

            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(tmp), Operand.Variable(identifier)));

            executable.InstructionsInternal.Add(new Instruction(OpCode.DEC, Operand.Variable(identifier)));

            return new Variable(tmp, Scope.Local, null);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable ShiftLeft()
        {

            string left = ExpectIdentifier();

            Token token = ReadToken();

            if (token.Type != TokenType.ShiftLeft)
                throw new ParserException("Shift Left '<<' erwartet.", token);

            Variable right = Factor();

            executable.InstructionsInternal.Add(new Instruction(OpCode.SHL, Operand.Variable(left), Operand.Variable(right.name)));

            return right;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable ShiftRight()
        {

            string left = ExpectIdentifier();

            Token token = ReadToken();

            if (token.Type != TokenType.ShiftRight)
                throw new ParserException("Shift Right '>>' erwartet.", token);

            Variable right = Factor();

            executable.InstructionsInternal.Add(new Instruction(OpCode.SHR, Operand.Variable(left), Operand.Variable(right.name)));

            return right;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable BinaryAnd()
        {

            string left = ExpectIdentifier();

            Token token = ReadToken();

            if (token.Type != TokenType.AssignBinaryAnd)
                throw new ParserException("Binary AND '&=' erwartet.", token);

            Variable right = Factor();

            executable.InstructionsInternal.Add(new Instruction(OpCode.ANDB, Operand.Variable(left), Operand.Variable(right.name)));

            return right;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable BinaryOr()
        {

            string left = ExpectIdentifier();

            Token token = ReadToken();

            if (token.Type != TokenType.AssignBinaryOr)
                throw new ParserException("Binary OR '|=' erwartet.", token);

            Variable right = Factor();

            executable.InstructionsInternal.Add(new Instruction(OpCode.ORB, Operand.Variable(left), Operand.Variable(right.name)));

            return right;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable BinaryNotAssign()
        {

            string left = ExpectIdentifier();

            Token token = ReadToken();

            if (token.Type != TokenType.AssignBinaryNot)
                throw new ParserException("Binary NEG '~=' erwartet.", token);

            Variable right = Factor();

            executable.InstructionsInternal.Add(new Instruction(OpCode.NOTB, Operand.Variable(left), Operand.Variable(right.name)));

            return right;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable BinaryNot()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.AssignBinaryNot)
                throw new ParserException("Binary NOT '~=' erwartet.", token);

            Variable right = Factor();

            executable.InstructionsInternal.Add(new Instruction(OpCode.NOTB, Operand.Variable(right.name)));

            return right;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable Xor()
        {

            string left = ExpectIdentifier();

            Token token = ReadToken();

            if (token.Type != TokenType.AssignXor)
                throw new ParserException("Binary XOR '~=' erwartet.", token);

            Variable right = Factor();

            executable.InstructionsInternal.Add(new Instruction(OpCode.XOR, Operand.Variable(left), Operand.Variable(right.name)));

            return right;

        }

        public string ToLiteral(string input)
        {
            var literal = new StringBuilder(input.Length + 2);
            literal.Append("\"");
            foreach (var c in input)
            {
                switch (c)
                {
                    case '\'': literal.Append(@"\'"); break;
                    case '\"': literal.Append("\\\""); break;
                    case '\\': literal.Append(@"\\"); break;
                    case '\0': literal.Append(@"\0"); break;
                    case '\a': literal.Append(@"\a"); break;
                    case '\b': literal.Append(@"\b"); break;
                    case '\f': literal.Append(@"\f"); break;
                    case '\n': literal.Append(@"\n"); break;
                    case '\r': literal.Append(@"\r"); break;
                    case '\t': literal.Append(@"\t"); break;
                    case '\v': literal.Append(@"\v"); break;
                    default:
                        if (char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Control)
                        {
                            literal.Append(c);
                        }
                        else
                        {
                            literal.Append(@"\u");
                            literal.Append(((ushort)c).ToString("x4"));
                        }
                        break;
                }
            }
            literal.Append("\"");
            return literal.ToString();
        }

        /// <summary>
        /// The smallest unit
        /// </summary>
        /// <returns></returns>
        private Variable Atom()
        {

            Token token = ReadToken();

            Variable variable = new Variable();

            switch (token.Type)
            {

                case TokenType.Minus:

                    variable = Atom();

                    string tmpIdentifier = AllocateTemporaryVariable();

                    executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(tmpIdentifier), Operand.Variable(variable.name)));

                    executable.InstructionsInternal.Add(new Instruction(OpCode.NEG, Operand.Variable(tmpIdentifier)));

                    variable.name = tmpIdentifier;

                    return variable;

                case TokenType.Null:

                    variable.name = AllocateTemporaryVariable();

                    variable.scope = Scope.Local;

                    variable.derivatedType = typeof(NullReference);

                    executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(variable.name), Operand.Literal(NullReference.Instance)));

                    return variable;

                case TokenType.Integer:
                case TokenType.Float:
                case TokenType.Boolean:
                case TokenType.String:
                case TokenType.Char:
                case TokenType.Double:
                case TokenType.Decimal:

                    variable.name = AllocateTemporaryVariable();

                    variable.scope = Scope.Local;

                    variable.derivatedType = Literal(token.Type);

                    executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(variable.name), Operand.Literal(token.Lexeme)));

                    return variable;

                case TokenType.Increment:

                    UndoToken();

                    variable = PreIncrement();

                    return variable;

                case TokenType.Decrement:

                    UndoToken();

                    variable = PreDecrement();

                    return variable;

                case TokenType.Identifier:

                    string identifier = token.Lexeme.ToString();

                    switch (LookAhead().Type)
                    {

                        case TokenType.Increment:

                            UndoToken();

                            return PostIncrement();

                        case TokenType.Decrement:

                            UndoToken();

                            return PostDecrement();

                        case TokenType.LeftBracket:

                            UndoToken();

                            return Pointer();

                        case TokenType.Period:

                            UndoToken();

                            return Member();

                        case TokenType.LeftParen:

                            UndoToken();

                            return FunctionCall();

                        case TokenType.ShiftLeft:

                            UndoToken();

                            return ShiftLeft();

                        case TokenType.ShiftRight:

                            UndoToken();

                            return ShiftRight();

                        default:

                            UndoToken();

                            identifier = ExpectIdentifier();

                            variable.name = AllocateTemporaryVariable();

                            variable.scope = Scope.Local;

                            variable.derivatedType = null;

                            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(variable.name), Operand.Variable(identifier)));

                            return variable;

                    }

                case TokenType.LeftParen:

                    variable = Expression();

                    ReadRightParenthesis();

                    return variable;

                default:

                    throw new ParserException( "Fehlerhafter Token '" + token + "'.", token);

            }

        }

        /// <summary>
        /// An array enclosed in braces
        /// </summary>
        /// <returns></returns>
        private Variable BraceArray()
        {

            ReadLeftBrace();

            int index = 0;

            string identifier = AllocateTemporaryVariable();

            string indexIdentifier = AllocateTemporaryVariable();

            executable.InstructionsInternal.Add(new Instruction(OpCode.DC, Operand.Variable(identifier)));

            if (LookAhead().Type != TokenType.RightBrace)
            {

                while (true)
                {

                    Variable tmp = Expression();

                    Token token = LookAhead();

                    if (token.Type == TokenType.Comma || token.Type == TokenType.SemiColon || token.Type == TokenType.RightBrace)
                    {

                        executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.MemberVariable(identifier, index++), Operand.Variable(tmp.name)));

                        if (token.Type == TokenType.RightBrace)
                            break;

                        /* todo: could be comma or semicolon, depends on current cultureinfo */
                        if (token.Type == TokenType.Comma)
                            ReadComma();

                        if (token.Type == TokenType.SemiColon)
                            ReadSemicolon();

                    }

                    else if (token.Type == TokenType.Colon)
                    {

                        ReadToken();

                        Variable variableKey = tmp;

                        tmp = Expression();

                        executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.CreatePointer(identifier, variableKey.name), Operand.Variable(tmp.name)));

                        if (LookAhead().Type == TokenType.RightBrace)
                            break;

                        /* todo: could be comma or semicolon, depends on current cultureinfo */
                        if (LookAhead().Type == TokenType.Comma)
                            ReadComma();

                        if (LookAhead().Type == TokenType.SemiColon)
                            ReadSemicolon();

                    }

                    else
                        throw new ParserException( "Ein Comma ',', Semicolon ';' oder Colon ':' wurde erwartet.");

                }

            }

            ReadRightBrace();

            return new Variable(identifier, Scope.Local, typeof(ArrayList));

        }

        /// <summary>
        /// An array enclosed in brackets
        /// </summary>
        /// <returns></returns>
        private Variable BracketArray()
        {

            ReadLeftBracket();

            int index = 0;

            string identifier = AllocateTemporaryVariable();

            string indexIdentifier = AllocateTemporaryVariable();

            executable.InstructionsInternal.Add(new Instruction(OpCode.DC, Operand.Variable(identifier)));

            if (LookAhead().Type != TokenType.RightBracket)
            {

                while (true)
                {

                    Variable tmp = Expression();

                    Token token = LookAhead();

                    if (token.Type == TokenType.Comma || token.Type == TokenType.RightBracket)
                    {

                        executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.MemberVariable(identifier, index++), Operand.Variable(tmp.name)));

                        if (token.Type == TokenType.RightBracket)
                            break;

                        ReadComma();

                    }

                    else if (token.Type == TokenType.Colon)
                    {

                        ReadToken();

                        Variable key = tmp;

                        tmp = Expression();

                        executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.CreatePointer(identifier, key.name), Operand.Variable(tmp.name)));

                        if(LookAhead().Type == TokenType.RightBracket)
                            break;

                        ReadComma();

                    }

                    else
                        throw new ParserException( "Ein Comma ',' oder Colon ':' wurde erwartet.");

                }

            }

            ReadRightBracket();

            return new Variable(identifier, Scope.Local, typeof(ArrayList));

        }

        /// <summary>
        /// Atom | Array
        /// </summary>
        /// <returns></returns>
        private Variable Factor()
        {


            if (LookAhead().Type == TokenType.LeftBrace)
                return BraceArray();

            // test
            if (LookAhead().Type == TokenType.LeftBracket)
                return BracketArray();

            // unary bitwise NOT: "~x"
            if (LookAhead().Type == TokenType.BinaryNot)
            {
                ReadToken(); // "~"

                // binds tight and is right-associative: "~~x" etc.
                Variable right = Factor();

                string tmp = AllocateTemporaryVariable();

                executable.InstructionsInternal.Add(new Instruction(
                    OpCode.NOTB,
                    Operand.Variable(tmp),
                    Operand.Variable(right.name)
                ));

                // type: same as right, but practically int for bitwise
                Variable result = new Variable(tmp, Scope.Local, typeof(int));
                result.derivatedType = typeof(int);

                return result;
            }

            Variable variable = Atom();

            return variable;

        }

        /// <summary>
        /// Factor ( [*|/|%] Factor )
        /// </summary>
        /// <returns></returns>
        private Variable Term()
        {

            List<Instruction> listInstructions = executable.InstructionsInternal;

            Variable first = Factor();

            Variable second = new Variable();

            while (true)
            {

                Token token = ReadToken();

                switch (token.Type)
                {

                    case TokenType.Multiply:
                        second = Factor();

                        listInstructions.Add(new Instruction(OpCode.MUL, Operand.Variable(first.name), Operand.Variable(second.name)));

                        first.derivatedType = Derivate(token, first.derivatedType, second.derivatedType);

                        break;

                    case TokenType.Divide:

                        second = Factor();

                        listInstructions.Add(new Instruction(OpCode.DIV, Operand.Variable(first.name), Operand.Variable(second.name)));

                        first.derivatedType = Derivate(token, first.derivatedType, second.derivatedType);

                        break;

                    case TokenType.Modulo:
                        second = Factor();

                        listInstructions.Add(new Instruction(OpCode.MOD, Operand.Variable(first.name), Operand.Variable(second.name)));

                        first.derivatedType = Derivate(token, first.derivatedType, second.derivatedType);

                        break;

                    default:
                        UndoToken();
                        return first;

                }

            }

        }

        /// <summary>
        /// Multiplication and division before Addition and substraction.
        /// 
        /// Refer to https://en.wikipedia.org/wiki/Order_of_operations
        /// </summary>
        /// <returns></returns>
        private Variable Arithmetic()
        {

            List<Instruction> listInstructions = executable.InstructionsInternal;

            Variable first = Term();

            Variable second = new Variable();

            while(true)
            {

                Token token = ReadToken();

                switch (token.Type)
                {

                    case TokenType.Plus:

                        second = Term();

                        listInstructions.Add(new Instruction(OpCode.ADD, Operand.Variable(first.name), Operand.Variable(second.name)));

                        first.derivatedType = Derivate(token, first.derivatedType, second.derivatedType);

                        break;

                    case TokenType.Minus:

                        second = Term();

                        listInstructions.Add(new Instruction(OpCode.SUB, Operand.Variable(first.name), Operand.Variable(second.name)));

                        break;

                    default:

                        UndoToken();

                        return first;

                }

            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable Relation()
        {

            List<Instruction> instructions = executable.InstructionsInternal;

            Variable first = Arithmetic();

            Token token = ReadToken();

            if (RelationalOperator(token.Type)) {

                Variable second = Arithmetic();

                instructions.Add(new Instruction(RelationalOpcode(token.Type), Operand.Variable(first.name), Operand.Variable(second.name)));

                first.derivatedType = Derivate(token, first.derivatedType, second.derivatedType);

            }

            else
                UndoToken();

            return first;

        }

        /// <summary>
        /// Proposition
        /// 
        /// May be a signed atom or a relation
        /// </summary>
        /// <returns></returns>
        private Variable Not()
        {

            Variable proposition = new Variable();

            if (LookAhead().Type == TokenType.Not)
            {

                ReadToken();

                if (LookAhead().Type == TokenType.LeftParen)
                    proposition = Expression();

                else
                    proposition = Relation();

                executable.InstructionsInternal.Add(new Instruction(OpCode.NOT, Operand.Variable(proposition.name)));

                return proposition;

            }

            else
                return Relation();

        }

        /// <summary>
        /// Conjunction 
        /// 
        /// Check for proposition (a signed atom)
        /// </summary>
        /// <returns></returns>
        private Variable And()
        {

            List<Instruction> instructions = executable.InstructionsInternal;

            Variable first = Not();
            
            while (true)
            {

                Token token = ReadToken();

                if (token.Type == TokenType.And)
                {

                    Variable second = Not();

                    instructions.Add(new Instruction(OpCode.AND, Operand.Variable(first.name), Operand.Variable(second.name)));

                    first.derivatedType = Derivate(token, first.derivatedType, second.derivatedType);

                    break;

                }

                else
                {

                    UndoToken();

                    return first;

                }

            }

            return first;

        }

        /// <summary>
        /// Disjunction (not exclusive)
        /// 
        /// Check for conjunction
        /// </summary>
        /// <returns></returns>
        private Variable Or()
        {

            List<Instruction> instructions = executable.InstructionsInternal;

            Variable first = And();

            while (true)
            {

                Token token = ReadToken();

                if (token.Type == TokenType.Or)
                {

                    Variable second = And();

                    instructions.Add( new Instruction(OpCode.OR, Operand.Variable(first.name), Operand.Variable(second.name)));

                    first.derivatedType = Derivate(token, first.derivatedType, second.derivatedType);

                    break;

                }

                else
                {

                    UndoToken();

                    return first;

                }

            }

            return first;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable VariableAssignment()
        {

            string identifier = ExpectIdentifier();

            Token token = ReadToken();

            if (!AssignmentOperator(token.Type))
                throw new ParserException("Ein Zuweisungsoperator wurde erwartet erwartet.", token);

            Variable expression = Expression();

            executable.InstructionsInternal.Add(new Instruction(AssignmentOpcode(token.Type), Operand.Variable(identifier), Operand.Variable(expression.name)));

            string tmpIdentifier = AllocateTemporaryVariable();

            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(tmpIdentifier), Operand.Variable(identifier)));

            return new Variable(tmpIdentifier, Scope.Local, expression.derivatedType);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable ArrayAssignment()
        {

            string identifier = ExpectIdentifier();

            List<Instruction> listInstructions = executable.InstructionsInternal;

            Variable tmp = new Variable();

            string src = identifier;

            string dst = null;

            while (!AssignmentOperator(LookAhead().Type))
            {

                ReadLeftBracket();

                tmp = Expression();

                ReadRightBracket();

                if (!AssignmentOperator(LookAhead().Type))
                {

                    dst = AllocateTemporaryVariable();

                    listInstructions.Add(new Instruction(OpCode.MOV, Operand.Variable(dst), Operand.CreatePointer(src, tmp.name)));

                    src = dst;

                }

            }

            Token tok = ReadToken();

            Variable expression = Expression();

            if (dst == null)
                dst = identifier;

            listInstructions.Add(new Instruction(AssignmentOpcode(tok.Type), Operand.CreatePointer(dst, tmp.name), Operand.Variable(expression.name)));

            string tmpIdentifier = AllocateTemporaryVariable();

            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(tmpIdentifier), Operand.CreatePointer(dst, tmp.name)));

            return new Variable(tmpIdentifier, Scope.Local, expression.derivatedType);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Variable MemberAssignment()
        {

            string identifier = ExpectIdentifier();

            List<string> members = new List<string>();

            while (LookAhead().Type == TokenType.Period)
            {

                ReadPeriod();

                members.Add(ReadIdentifier());

            }

            Token tok = ReadToken();

            if (!AssignmentOperator(tok.Type))
                throw new ParserException("Ein Zuweisungsoperator wurde erwartet.", tok);

            Variable expression = Expression();

            string dst = null;

            string src = identifier;

            for (int i = 0; i < members.Count - 1; i++)
            {

                dst = AllocateTemporaryVariable();

                executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(dst), Operand.MemberVariable(src, members[i])));

                src = dst;

            }

            if (dst == null)
                dst = identifier;

            executable.InstructionsInternal.Add(new Instruction(AssignmentOpcode(tok.Type), Operand.MemberVariable(dst, members[members.Count - 1]), Operand.Variable(expression.name)));

            string tmpIdentifier = AllocateTemporaryVariable();

            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(tmpIdentifier), Operand.MemberVariable(dst, members[members.Count - 1])));

            Variable variable = new Variable(tmpIdentifier, Scope.Local, expression.derivatedType);

            return variable;

        }

        /// <summary>
        /// An assignment can be a variable assignment, an array assignment or a member assignment
        /// </summary>
        /// <returns></returns>
        private Variable Assignment()
        {

            string identifier = ExpectIdentifier();

            Token token = LookAhead();

            switch (token.Type)
            {

                case TokenType.Assign:
                case TokenType.AssignPlus:
                case TokenType.AssignMinus:
                case TokenType.AssignMultiply:
                case TokenType.AssignDivide:
                case TokenType.AssignBinaryAnd:
                case TokenType.AssignBinaryOr:
                case TokenType.AssignXor:
                case TokenType.AssignBinaryNot:
                case TokenType.AssignModulo:
                    UndoToken();
                    return VariableAssignment();

                case TokenType.LeftBracket:
                    UndoToken();
                    return ArrayAssignment();

                case TokenType.Period:
                    UndoToken();
                    return MemberAssignment();

                default:
                    throw new ExecutionException("Es wurde ein Zuweisungoperator erwartet.");

            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool IsPointer()
        {

            Token start = LookAhead();

            if (start.Type != TokenType.Identifier)
                return false;

            int iInstructionCheckpoint = executable.InstructionsInternal.Count;

            string identifier = ReadIdentifier();

            while (LookAhead().Type == TokenType.LeftBracket)
            {

                ReadLeftBracket();
                Expression();
                ReadRightBracket();

            }

            Token tok = ReadToken();

            while (LookAhead() != start)
                UndoToken();

            executable.InstructionsInternal.RemoveRange(iInstructionCheckpoint, executable.InstructionsInternal.Count - iInstructionCheckpoint);

            return AssignmentOperator(tok.Type);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool IsMember()
        {

            Token start = LookAhead();

            if (start.Type != TokenType.Identifier)
                return false;

            string identifier = ReadIdentifier();

            while (LookAhead().Type == TokenType.Period)
            {

                ReadPeriod();

                Token token = ReadToken();

                if (token.Type != TokenType.Identifier)
                {

                    while (LookAhead() != start)
                        UndoToken();

                    return false;

                }

            }

            Token tok = ReadToken();

            while (LookAhead() != start)
                UndoToken();

            return AssignmentOperator(tok.Type);

        }

        /// <summary>
        /// An expression is an assignment or a disjunction
        /// </summary>
        /// <returns></returns>
        private Variable Expression()
        {

            if (IsPointer() || IsMember())
                return Assignment();

            else
                return Or();

        }

        /// <summary>
        /// 
        /// </summary>
        private void If()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.If)
                throw new ParserException( "Keyword 'if' erwartet.", token);

            ReadLeftParenthesis();

            Variable condition = Expression();

            ReadRightParenthesis();

            Instruction start = new Instruction(OpCode.NOP);

            Instruction end = new Instruction(OpCode.NOP);

            executable.InstructionsInternal.Add(new Instruction(OpCode.JNZ, Operand.Variable(condition.name), Operand.AllocateInstructionPointer(start)));

            StatementList();

            executable.InstructionsInternal.Add(new Instruction(OpCode.JMP, Operand.AllocateInstructionPointer(end)));

            executable.InstructionsInternal.Add(start);

            if (LookAhead().Type == TokenType.Else)
            {

                ReadToken();

                StatementList();
            }

            executable.InstructionsInternal.Add(end);

        }

        /// <summary>
        /// 
        /// </summary>
        private void While()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.While)
                throw new ParserException("Keyword 'while' erwartet.", token);

            Instruction start = new Instruction(OpCode.NOP);

            executable.InstructionsInternal.Add(start);

            ReadLeftParenthesis();

            Variable condition = Expression();

            if (condition.derivatedType != null && condition.derivatedType != typeof(bool))
                throw new ParserException("In While Loops wird ein logischer Ausdruck erwartet.", token);

            ReadRightParenthesis();

            Instruction end = new Instruction(OpCode.NOP);

            executable.InstructionsInternal.Add(new Instruction(OpCode.JNZ, Operand.Variable(condition.name), Operand.AllocateInstructionPointer(end)));

            LoopControl loopControl = new LoopControl();

            loopControl.Break = end;

            loopControl.Continue = start;

            this.loopControl.Push(loopControl);

            StatementList();

            this.loopControl.Pop();

            executable.InstructionsInternal.Add(new Instruction(OpCode.JMP, Operand.AllocateInstructionPointer(start)));

            executable.InstructionsInternal.Add(end);

        }

        /// <summary>
        /// 
        /// </summary>
        private void For()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.For)
                throw new ParserException("Keyword 'for' erwartet.", token);

            ReadLeftParenthesis();

            if (LookAhead().Type == TokenType.SemiColon)
                ReadSemicolon();

            else if (LookAhead().Type == TokenType.Var)
                LocalVariableDeclaration();

            else
            {

                Assignment();

                ReadSemicolon();

            }

            Instruction start = new Instruction(OpCode.NOP);

            executable.InstructionsInternal.Add(start);

            Instruction continueInstruction = new Instruction(OpCode.NOP);

            Variable condition = new Variable();

            if (LookAhead().Type == TokenType.SemiColon)
            {

                condition = new Variable(AllocateTemporaryVariable(), Scope.Local, typeof(bool));

                executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(condition.name), Operand.Literal(true)));

                ReadSemicolon();

            }

            else
            {

                condition = Expression();

                if (condition.derivatedType != null && condition.derivatedType != typeof(bool))
                    throw new ParserException("In For Loops wird ein logischer Ausdruck oder 'null' erwartet.", token);

                ReadSemicolon();

            }

            List<Instruction> expression = null;

            if (LookAhead().Type != TokenType.RightParen)
            {

                int loopStart = executable.InstructionsInternal.Count;

                Expression();

                int loopCount = executable.InstructionsInternal.Count - loopStart; 

                expression = executable.InstructionsInternal.GetRange(loopStart, loopCount);

                executable.InstructionsInternal.RemoveRange(loopStart, loopCount);

            }

            else
                expression = new List<Instruction>();

            ReadRightParenthesis();

            Instruction end = new Instruction(OpCode.NOP);

            executable.InstructionsInternal.Add(new Instruction(OpCode.JNZ, Operand.Variable(condition.name), Operand.AllocateInstructionPointer(end)));

            LoopControl loopControl = new LoopControl();

            loopControl.Break = end;

            loopControl.Continue = continueInstruction;

            this.loopControl.Push(loopControl);

            StatementList();

            this.loopControl.Pop();

            executable.InstructionsInternal.Add(continueInstruction);

            executable.InstructionsInternal.AddRange(expression);

            executable.InstructionsInternal.Add(new Instruction(OpCode.JMP, Operand.AllocateInstructionPointer(start)));

            executable.InstructionsInternal.Add(end);

        }

        /// <summary>
        /// 
        /// </summary>
        private void ForEach()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Foreach)
                throw new ParserException("Keyword 'foreach' erwartet.", token);

            ReadLeftParenthesis();

            string key = null;

            string val = ExpectIdentifier();

            token = ReadToken();

            if (token.Type == TokenType.Comma)
            {

                key = val;

                val = ExpectIdentifier();

                token = ReadToken();

            }

            if (token.Type != TokenType.In)
                throw new ParserException("Keyword 'in' erwartet.", token);

            if (key == null)
                key = AllocateTemporaryVariable();

            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(key), Operand.Literal(NullReference.Instance)));

            Instruction start = new Instruction(OpCode.NOP);

            executable.InstructionsInternal.Add(start);

            Variable array = Expression();

            if (array.derivatedType != null && array.derivatedType != typeof(ArrayList))
                throw new ParserException("In ForEach Loops wird ein logischer Ausdruck erwartet.", token);

            ReadRightParenthesis();

            executable.InstructionsInternal.Add(new Instruction(OpCode.PTR, Operand.Variable(key), Operand.Variable(array.name)));

            string identifier = AllocateTemporaryVariable();

            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(identifier), Operand.Variable(key)));

            //executable.InstructionsInternal.Add(new Instruction(OpCode.CEQ, Operand.Variable(identifier), Operand.Literal(true)));
            executable.InstructionsInternal.Add(new Instruction(OpCode.TEST, Operand.Variable(identifier)));

            Instruction end = new Instruction(OpCode.NOP);

            executable.InstructionsInternal.Add(new Instruction(OpCode.JZ, Operand.Variable(identifier), Operand.AllocateInstructionPointer(end)));

            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(val), Operand.CreatePointer(array.name, key)));

            LoopControl loopControl = new LoopControl();

            loopControl.Break = end;

            loopControl.Continue = start;

            this.loopControl.Push(loopControl);

            StatementList();

            this.loopControl.Pop();

            executable.InstructionsInternal.Add(new Instruction(OpCode.JMP, Operand.AllocateInstructionPointer(start)));

            executable.InstructionsInternal.Add(end);

        }

        /// <summary>
        /// 
        /// </summary>
        private void Break()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Break)
                throw new ParserException("Keyword 'break' erwartet.", token);

            ReadSemicolon();

            if (loopControl.Count == 0)
                throw new ParserException("Das Keyword 'break' kann nur innerhalb von Loops verwendet werden.", token);

            Instruction breakInstruction = loopControl.Peek().Break;

            executable.InstructionsInternal.Add(new Instruction(OpCode.JMP, Operand.AllocateInstructionPointer(breakInstruction)));

        }

        /// <summary>
        /// 
        /// </summary>
        private void Continue()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Continue)
                throw new ParserException("Keyword 'continue' erwartet.", token);

            ReadSemicolon();

            if (loopControl.Count == 0)
                throw new ParserException("Das Keyword 'continue' kann nur innerhalb von Loops verwendet werden.", token);

            Instruction continueInstruction = loopControl.Peek().Continue;

            executable.InstructionsInternal.Add(new Instruction(OpCode.JMP, Operand.AllocateInstructionPointer(continueInstruction)));

        }

        /// <summary>
        /// 
        /// </summary>
        private void Switch()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Switch)
                throw new ParserException("Keyword 'switch' erwartet.", token);

            ReadLeftParenthesis();

            string switchIdentifier = ExpectIdentifier();

            ReadRightParenthesis();

            ReadLeftBrace();

            token = LookAhead();

            if (token.Type != TokenType.Case && token.Type != TokenType.Default)
                throw new ParserException("Keyword 'case' oder 'default' erwartet.", token);

            string tmpIdentifier = AllocateTemporaryVariable();

            string identifier = AllocateTemporaryVariable();

            Instruction end = new Instruction(OpCode.NOP);

            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(tmpIdentifier), Operand.Literal(false)));

            while (LookAhead().Type != TokenType.Default && LookAhead().Type != TokenType.RightBrace)
            {

                token = ReadToken();

                if (token.Type != TokenType.Case)
                    throw new ParserException("Keyword 'case' erwartet.", token);

                InsertDebugInfo(token);

                Variable expression = Expression();

                token = ReadToken();

                if (token.Type != TokenType.Colon)
                    throw new ParserException("Colon ':' erwartet.", token);

                executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(identifier), Operand.Variable(switchIdentifier)));

                executable.InstructionsInternal.Add(new Instruction(OpCode.CEQ, Operand.Variable(identifier), Operand.Variable(expression.name)));

                executable.InstructionsInternal.Add(new Instruction(OpCode.OR,  Operand.Variable(tmpIdentifier),  Operand.Variable(identifier)));

                if (LookAhead().Type != TokenType.Case)
                {
                    Instruction switchInstruction = new Instruction(OpCode.NOP);

                    executable.InstructionsInternal.Add(new Instruction(OpCode.JNZ, Operand.Variable(tmpIdentifier), Operand.AllocateInstructionPointer(switchInstruction)));

                    Statement();

                    executable.InstructionsInternal.Add(new Instruction(OpCode.JMP, Operand.AllocateInstructionPointer(end)));

                    executable.InstructionsInternal.Add(switchInstruction);

                    executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(tmpIdentifier), Operand.Literal(false)));

                }
            }

            token = ReadToken();

            if (token.Type == TokenType.RightBrace)
            {

                executable.InstructionsInternal.Add(end);

                return;

            }

            if (token.Type != TokenType.Default)
                throw new ParserException("Das Keyword 'default' oder eine schliessende geschwungene Klammer '}' wird am Ende einer 'switch' Anweisung erwartet.", token);

            token = ReadToken();

            if (token.Type != TokenType.Colon)
                throw new ParserException("Ein Colon ':' wurde erwartet.", token);

            Statement();

            ReadRightBrace();

            executable.InstructionsInternal.Add(end);

        }

        /// <summary>
        /// By default null is returned
        /// </summary>
        private void Return()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Return)
                throw new ParserException("Keyword 'return' erwartet.", token);

            if (LookAhead().Type != TokenType.SemiColon)
                executable.InstructionsInternal.Add(new Instruction(OpCode.PUSH, Operand.Variable(Expression().name)));

            else
                executable.InstructionsInternal.Add(new Instruction(OpCode.PUSH, Operand.Literal(NullReference.Instance)));

            ReadSemicolon();

            executable.InstructionsInternal.Add(new Instruction(OpCode.RET));

            return;

        }

        /// <summary>
        /// Parameter variables are already set to true but not assigned yet. Pop all of them in reverse order onto the stack.
        /// </summary>
        private void FunctionDeclaration()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Function)
                throw new ParserException("Funktion werden mit 'function' deklariert.", token);

            string functionName = ReadIdentifier();

            if (executable.Functions.ContainsKey(functionName))
                throw new ParserException("Die Funktion '" + functionName + "' ist bereits vorhanden.", token);

            ReadLeftParenthesis();

            InsertDebugInfo(token);

            List<string> parameters = new List<string>();

            if (LookAhead().Type != TokenType.RightParen)
            {

                while (true)
                {

                    token = ReadToken();

                    if (token.Type != TokenType.Identifier)
                        throw new ParserException("Unerwarteter Token '" + token.Lexeme + "'.", token);

                    string parameter = token.Lexeme.ToString();

                    AllocateVariable(parameter);

                    parameters.Add(parameter);

                    token = LookAhead();

                    if (token.Type == TokenType.Comma)
                        ReadComma();

                    else if (token.Type == TokenType.RightParen)
                        break;

                    else
                        throw new ParserException("Comma ',' oder schliessende Klammer ')' erwartet.");

                }

            }

            ReadRightParenthesis();

            Instruction scriptInstructionFunctionEntry = new Instruction(OpCode.NOP);

            executable.InstructionsInternal.Add(scriptInstructionFunctionEntry);

            Function scriptFunction = new Function(executable, functionName, parameters, scriptInstructionFunctionEntry);

            executable.Functions[functionName] = scriptFunction;

            parameters.Reverse();

            foreach (string parameter in parameters)
            {

                Instruction scriptInstructionPop = new Instruction(OpCode.POP, Operand.Variable(parameter));

                executable.InstructionsInternal.Add(scriptInstructionPop);

            }

            StatementList();

            executable.InstructionsInternal.Add(new Instruction( OpCode.PUSH, Operand.Literal(NullReference.Instance)));

            executable.InstructionsInternal.Add(new Instruction(OpCode.RET));

            localVariables.Clear();

        }

        /// <summary>
        /// Call a forward declared function
        /// Push all parameter identifier onto the stack and call the function/routine
        /// Only functions can run in background because routines are not translated!
        /// \todo translate routines to msil?
        /// </summary>
        /// <param name="background"></param>
        /// <returns></returns>
        private Variable FunctionCall(bool background)
        {

            string name = ReadIdentifier();

            // lets expect it is a function
            bool forwardDeclared = true;

            // if it is registered to the manager it is a routine of course
            if (!executable.Functions.ContainsKey(name))
                forwardDeclared = false;

            ReadLeftParenthesis();

            uint parameterCount = 0;

            if (LookAhead().Type != TokenType.RightParen)
            {

                // parameters are on the stack, already set to true so we just assign them
                while (true)
                {

                    Variable parameter = Expression();

                    executable.InstructionsInternal.Add(new Instruction(OpCode.PUSH, Operand.Variable(parameter.name)));

                    ++parameterCount;

                    if (LookAhead().Type == TokenType.RightParen)
                        break;

                    else
                        ReadComma();

                }

            }

            ReadRightParenthesis();

            Instruction instruction = null;

            Function function = null;

            if (forwardDeclared)
            {

                function = executable.Functions[name];

                if (function.ParameterCount > parameterCount)
                    throw new ParserException("Der Aufruf der Funktion '" + name + "' hat fehlende Parameter. Erwartet werden " + function.ParameterCount + " Parameter.");

                // \todo should we just throw the rest?
                if (function.ParameterCount < parameterCount)
                    throw new ParserException("Der Aufruf der Funktion '" + name + "' hat zu viele Parameter. Erwartet werden " + function.ParameterCount + " Parameter.");

            }

            Variable variable = new Variable();

            if (background)
            {

                instruction = new Instruction(OpCode.RUN, Operand.AllocateFunctionPointer(function));

                executable.InstructionsInternal.Add(instruction);

            }

            else
            {

                instruction = new Instruction(OpCode.CALL, Operand.AllocateFunctionPointer(function));

                executable.InstructionsInternal.Add(instruction);

                // the result is popped onto the stack so a temp variable is created like '[0:0]'
                variable.name = AllocateTemporaryVariable();

                variable.scope = Scope.Local;

                variable.derivatedType = null;

                executable.InstructionsInternal.Add(new Instruction(OpCode.POP, Operand.Variable(variable.name)));

            }

            if (!forwardDeclared)
            {

                FunctionDescriptor functionDescriptor = new FunctionDescriptor();

                functionDescriptor.name = name;

                functionDescriptor.parameterCount = parameterCount;

                functionDescriptor.instruction = null;

                forwardDeclarations[instruction] = functionDescriptor;

            }

            return variable;

        }

        /// <summary>
        /// Can be a call to a forward declared Function or a Routine. 
        /// To check what it is we look if it is a registered routine, if not it must be a function.
        /// </summary>
        /// <returns></returns>
        private Variable FunctionCall()
        {

            string functionName = ReadIdentifier();

            UndoToken();

            if (executable.Script.Manager.IsRegistered(functionName))
                return RoutineCall();

            else
                return FunctionCall(false);

        }

        /// <summary>
        /// A statement can be a local variable declaration, a statement list, an expression or a keyword 
        /// </summary>
        private void Statement()
        {

            AllocateFunctionFrame();

            Token token = LookAhead();

            InsertDebugInfo(token);

            switch (token.Type)
            {

                case TokenType.SemiColon:
                    ReadToken();
                    break;

                case TokenType.Var:
                    LocalVariableDeclaration();
                    break;

                case TokenType.LeftBrace:
                    StatementList();
                    break;

                case TokenType.Increment:
                case TokenType.Decrement:
                case TokenType.LeftParen:
                case TokenType.Identifier:
                case TokenType.Null:
                case TokenType.Integer:
                case TokenType.Float:
                case TokenType.Boolean:
                case TokenType.String:
                case TokenType.Char:
                    Expression();
                    if(LookAhead().Type == TokenType.SemiColon)
                        ReadSemicolon();
                    break;

                case TokenType.If:
                    If();
                    break;
                case TokenType.While:
                    While();
                    break;
                case TokenType.For:
                    For();
                    break;
                case TokenType.Foreach:
                    ForEach();
                    break;
                case TokenType.Break:
                    Break();
                    break;
                case TokenType.Continue:
                    Continue();
                    break;
                case TokenType.Switch:
                    Switch();
                    break;
                case TokenType.Return:
                    Return();
                    break;

                case TokenType.Run:
                    Run();
                    break;
                case TokenType.Yield:
                    Yield();
                    break;
                case TokenType.Wait:
                    Wait();
                    break;
                case TokenType.Notify:
                    Notify();
                    break;
                case TokenType.Lock:
                    LockedStatementList();
                    break;

                default: throw new ParserException("ParserException::Statement: Ein unerwarteter Token '" + token.Lexeme + "' wurde gefunden.", token);

            }

            FreeFunctionFrame();

        }

        /// <summary>
        /// A list of statements. If its not a list (not in braces) just return a single statement
        /// 
        /// </summary>
        private void StatementList()
        {

            // if there are no braces, just read a single statement
            if (LookAhead().Type != TokenType.LeftBrace)
            {

                Statement();

                return;

            }

            ReadToken();

            while (LookAhead().Type != TokenType.RightBrace)
                Statement();

            ReadRightBrace();

        }

        /// <summary>
        /// 
        /// </summary>
        private void LockedStatementList()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Lock)
                throw new ExecutionException("ParserException: Keyword 'lock' erwartet.");

            Variable variable = Expression();

            executable.InstructionsInternal.Add(new Instruction(OpCode.LOCK, Operand.Variable(variable.name)));

            Statement();

            executable.InstructionsInternal.Add(new Instruction(OpCode.FREE, Operand.Variable(variable.name)));

        }

        /// <summary>
        /// 
        /// </summary>
        private void Yield()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Yield)
                throw new ParserException("ParserException: Keyword 'yield' erwartet.", token);

            ReadSemicolon();

            executable.InstructionsInternal.Add(new Instruction(OpCode.INT));

        }

        /// <summary>
        /// Wait for a locked secion of code to be freed
        /// 
        /// \todo
        /// </summary>
        private void Wait()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Wait)
                throw new ParserException("Keyword 'wailt' erwartet.", token);

            string identifier = ExpectIdentifier();

            ReadSemicolon();

            List<Instruction> instructions = executable.InstructionsInternal;

            Instruction arrived = new Instruction(OpCode.NOP);

            Instruction waiting = new Instruction(OpCode.JZ, Operand.Variable(identifier), Operand.AllocateInstructionPointer(arrived));

            instructions.Add(waiting);

            instructions.Add(new Instruction(OpCode.INT));

            instructions.Add(new Instruction(OpCode.JMP, Operand.AllocateInstructionPointer(waiting)));

            instructions.Add(arrived);

        }

        /// <summary>
        /// 
        /// \todo
        /// </summary>
        private void Notify()
        {

            Token token = ReadToken();

            if (token.Type != TokenType.Notify)
                throw new ParserException("Keyword 'notify' erwartet.", token);

            string identifier = ExpectIdentifier();

            ReadSemicolon();

            executable.InstructionsInternal.Add(new Instruction(OpCode.MOV, Operand.Variable(identifier), Operand.Literal(true)));

        }

        /// <summary>
        /// After the first function declaration no more variable, struct or enum declarations are allowed anymore.
        /// 
        /// \todo
        /// </summary>
        private void ParseScript()
        {

            while (More())
            {

                Token token = LookAhead();

                if (token.Type == TokenType.Shared || token.Type == TokenType.Var)
                    VariableDeclaration();

                else if (token.Type == TokenType.Struct)
                    StructDeclaration();

                else if (token.Type == TokenType.Enum)
                    EnumDeclaration();

                else
                    break;

            }

            if (!More())
                return;

            while (More())
            {

                Token token = LookAhead();

                if (token.Type != TokenType.Function)
                    throw new ParserException( "Ausserhalb von Funktionen sind keine Anweisungen erlaubt.", token);

                FunctionDeclaration();

            }

        }

        /// <summary>
        /// Resolve unresolved, forward declared functions
        /// </summary>
        private void ResolveForwardFunctionDeclarations()
        {

            foreach (Instruction instruction in forwardDeclarations.Keys)
            {

                FunctionDescriptor functionDescriptor = forwardDeclarations[instruction];

                string name = functionDescriptor.name;

                if (!executable.Functions.ContainsKey(name))
                    throw new ParserException("Eine nicht deklarierte Funktion '" + name + "' wurde referenziert.");

                Function function = executable.Functions[name];

                instruction.First.FunctionPointer = function;

            }

        }

        #endregion

        #region Internal Properties

        internal bool DebugMode
        {
            get { return debugMode; }
            set { debugMode = value; }
        }

        #endregion

        #region Public Methods

        public Parser(Script script, List<Token> tokenStream)
        {
            this.script = script;
            debugMode = false;
            nextToken = 0;
            variables = new Dictionary<String, bool>();
            localVariables = new Dictionary<String, bool>();
            functionFrameIndex = 0;
            forwardDeclarations = new Dictionary<Instruction, FunctionDescriptor>();
            loopControl = new Stack<LoopControl>();
            this.tokenStream = new List<Token>(tokenStream);
            derivation = new Derivation();
            executable = null;
        }

        /// <summary>
        /// Parse the token stream into an executable.
        /// </summary>
        /// <returns></returns>
        public Executable Parse()
        {
            nextToken = 0;
            variables.Clear();
            localVariables.Clear();
            functionFrameIndex = -1;
            forwardDeclarations.Clear();
            loopControl.Clear();

            executable = new Executable(script);

            ParseScript();
            ResolveForwardFunctionDeclarations();
            executable.Clean();

            return executable;
        }

        #endregion

    }

}
