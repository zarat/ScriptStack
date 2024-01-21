using System;
using System.Collections.Generic;
using System.Text;

using ScriptStack.Compiler;
using ScriptStack.Runtime;

namespace ScriptStack.Runtime
{

    public enum OperandType
    {
        Literal,
        Variable,
        Member,
        Pointer,
        InstructionPointer,
        FunctionPointer,
        RoutinePointer
    }

    public class Operand
    {

        #region Private Variables

        private OperandType m_operandType;
        private object m_objectValue;
        private object m_objectIndex;

        #endregion

        #region Private Methods

        private Operand(OperandType operandType, object objectValue, object objectIndex)
        {
            m_operandType = operandType;
            m_objectValue = objectValue;
            m_objectIndex = objectIndex;
        }

        private string ToString(object objectValue)
        {
            if (objectValue.GetType() == typeof(string))
                return "\"" + objectValue + "\"";
            else
                return objectValue.ToString();
        }

        #endregion

        #region Public Static Methods

        public static Operand Literal(object val)
        {
            return new Operand(OperandType.Literal, val, null);
        }

        public static Operand Variable(string identifier)
        {
            return new Operand(OperandType.Variable, identifier, null);
        }

        public static Operand MemberVariable(string identifier, object val)
        {
            return new Operand(OperandType.Member, identifier, val);
        }

        public static Operand CreatePointer(string identifier, string pointer)
        {
            return new Operand(OperandType.Pointer, identifier, pointer);
        }

        public static Operand AllocateInstructionPointer(Instruction instruction)
        {
            return new Operand(OperandType.InstructionPointer, instruction, null);
        }

        public static Operand AllocateFunctionPointer(Function function)
        {
            return new Operand(OperandType.FunctionPointer, function, null);
        }

        public static Operand AllocateRoutinePointer(Routine routine)
        {
            return new Operand(OperandType.RoutinePointer, routine, null);
        }

        #endregion

        #region Public Methods

        private string ToLiteral(string input)
        {

            var literal = new StringBuilder(input.Length + 2);

            foreach (var c in input)
            {

                switch (c)
                {

                    case '\"':
                        literal.Append("\\\"");
                        break;

                    case '\a':
                        literal.Append("\\a");
                        break;

                    case '\b':
                        literal.Append("\\b");
                        break;

                    case '\n':
                        literal.Append("\\n");
                        break;

                    case '\r':
                        literal.Append("\\r");
                        break;

                    case '\t':
                        literal.Append("\\t");
                        break;

                    default:
                        if (char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Control)
                            literal.Append(c);
                        else
                        {
                            literal.Append(@"\u");
                            literal.Append(((ushort)c).ToString("x4"));
                        }
                        break;

                }

            }

            return literal.ToString();

        }

        public override string ToString()
        {

            switch (m_operandType)
            {

                case OperandType.Literal:
                    {

                        // \todo improve
                        if (m_objectValue.GetType().ToString() == "System.String")
                            return ToLiteral((string)m_objectValue);

                        else if (m_objectValue.GetType().ToString() == "System.Char")
                            return ToLiteral("" + (char)m_objectValue);

                        else
                            return ToString(m_objectValue);

                    }

                case OperandType.Variable:
                    return m_objectValue.ToString();

                case OperandType.Member:
                    return m_objectValue + "[" + ToString(m_objectIndex) + "]";

                case OperandType.Pointer:
                    return m_objectValue + "[" + m_objectIndex + "]";

                case OperandType.InstructionPointer:
                    return "[" + ((Instruction) m_objectValue).Address.ToString("X8") + "]";

                case OperandType.FunctionPointer:
                    {
                        Function scriptFunction = (Function)m_objectValue;
                        return "<" + scriptFunction.Name + "@" + scriptFunction.EntryPoint.Address.ToString("X8") + ">";
                    }

                case OperandType.RoutinePointer:
                    return m_objectValue.ToString();

                default:
                    return ToLiteral(m_operandType.ToString());

            }
        }

        #endregion

        #region Public Properties

        public OperandType Type
        {
            get { return m_operandType; }
            set { m_operandType = value; }
        }

        public object Value
        {
            get { return m_objectValue; }
        }

        public object Member
        {
            get
            {
                if (m_operandType != OperandType.Member)
                    throw new ExecutionException("Error in member access.");
                return m_objectIndex;
            }
            set
            {
                if (m_operandType != OperandType.Member)
                    throw new ExecutionException("Error in member access.");
                m_objectIndex = value;
            }
        }

        public string Pointer
        {
            get
            {
                if (m_operandType != OperandType.Pointer)
                    throw new ExecutionException("Error in array access.");
                return (string) m_objectIndex; 
            }
            set
            {
                if (m_operandType != OperandType.Pointer)
                    throw new ExecutionException("Error in array access.");
                m_objectIndex = value;
            }
        }

        public Instruction InstructionPointer
        {
            get
            {
                if (m_operandType != OperandType.InstructionPointer)
                    throw new ParserException("Error in instruction access.");

                return (Instruction) m_objectValue;
            }
            set
            {
                if (m_operandType != OperandType.InstructionPointer)
                    throw new ParserException("Error in instruction access.");

                m_objectValue = value;
            }
        }

        public Function FunctionPointer
        {
            get
            {
                if (m_operandType != OperandType.FunctionPointer)
                    throw new ParserException("Error in function call.");

                return (Function)m_objectValue;
            }
            set
            {
                if (m_operandType != OperandType.FunctionPointer)
                    throw new ParserException("Error in function call.");

                m_objectValue = value;
            }
        }

        public Routine RoutinePointer
        {

            get
            {
                if (m_operandType != OperandType.RoutinePointer)
                    throw new ParserException("Error in routine access.");

                return (Routine)m_objectValue;
            }
            set
            {
                if (m_operandType != OperandType.RoutinePointer)
                    throw new ParserException("Error in routine access.");

                m_objectValue = value;
            }

        }

        #endregion

    }

}
