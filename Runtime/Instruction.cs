using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptStack.Runtime
{

    /// <summary>
    /// An instruction in a virtual intermediate language
    /// </summary>
    public class Instruction
    {

        #region Private Variables

        private uint address;
        private OpCode opcode;
        private Operand first;
        private Operand second;

        #endregion

        #region private Methods

        private string ToLiteral(string input)
        {
            var literal = new StringBuilder(input.Length + 2);
            //literal.Append("\"");
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
                        if (Char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Control)
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
            //literal.Append("\"");
            return literal.ToString();
        }

        #endregion

        #region Public Methods

        public Instruction(OpCode opcode, Operand first, Operand second)
        {
            address = 0;
            this.opcode = opcode;
            this.first = first;
            this.second = second;
        }

        public Instruction(OpCode opcode, Operand operand0)
            : this(opcode, operand0, null)
        {
        }

        public Instruction(OpCode opcode)
            : this(opcode, null, null)
        {
        }

        public override string ToString()
        {

            if (opcode == OpCode.DBG)
            {

                int lineNumber = (int)first.Value;
                return "Verarbeite Zeile: " + lineNumber + "\n" + second.Value;

            }

            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append("["+string.Format(address.ToString("X8"))+"]");
            stringBuilder.Append("    ");

            stringBuilder.Append(opcode.ToString());
            int iOpcodeLength = opcode.ToString().Length;
            if (iOpcodeLength == 2)
                stringBuilder.Append("  ");
            if (iOpcodeLength == 3)
                stringBuilder.Append(" ");

            if (first != null)
            {
                stringBuilder.Append(" ");
                stringBuilder.Append(first.ToString());
            }

            if (second != null)
            {
                stringBuilder.Append(", ");
                stringBuilder.Append(second.ToString());
            }

            return stringBuilder.ToString();
        }

        #endregion

        #region Public Properties

        public uint Address
        {
            get { return address; }
            set { address = value; }
        }

        public OpCode OpCode
        {
            get { return opcode; }
            set { opcode = value; }
        }

        public Operand First
        {
            get { return first; }
            set { first = value; }
        }

        public Operand Second
        {
            get { return second; }
            set { second = value; }
        }

        #endregion

    }

}
