using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using ScriptStack.Compiler;

namespace ScriptStack.Runtime
{

    /// <summary>
    /// 
    /// </summary>
    public class Executable
    {

        #region Private Variables

        private Script script;
        private List<Instruction> instructions;
        private Dictionary<String, Function> functions;
        private Memory scriptMemory;

        #endregion

        #region Private Methods

        private void Clean(Operand operand)
        {

            if (operand == null)
                return;

            if (operand.Type != OperandType.InstructionPointer)
                return;

            Instruction instruction = operand.InstructionPointer;

            if (instruction.OpCode != OpCode.NOP && instruction.OpCode != OpCode.DBG)
                return;

            Instruction nextInstruction = instruction;

            while (nextInstruction.OpCode == OpCode.NOP || nextInstruction.OpCode == OpCode.DBG)
                nextInstruction = instructions[(int)nextInstruction.Address + 1];

            operand.InstructionPointer = nextInstruction;

        }

        #endregion

        #region Internal Methods

        internal void Clean()
        {

            Sort();

            foreach (Instruction instruction in instructions)
            {
                Clean(instruction.First);
                Clean(instruction.Second);
            }

            foreach (Function function in functions.Values)
            {

                Instruction instruction = function.EntryPoint;

                Instruction nextInstruction = instruction;

                while (nextInstruction.OpCode == OpCode.NOP || nextInstruction.OpCode == OpCode.DBG)
                    nextInstruction = instructions[(int)nextInstruction.Address + 1];

                function.EntryPoint = nextInstruction;

            }

            for (int i = instructions.Count - 1; i >= 0; i--)
                if (instructions[i].OpCode == OpCode.NOP)
                    instructions.RemoveAt(i);

            Sort();

        }

        internal void Sort()
        {
            for (int i = 0; i < instructions.Count; i++)
                instructions[i].Address = (uint)i;
        }

        internal List<Instruction> InstructionsInternal
        {
            get { return instructions; }
        }

        #endregion

        #region Public Methods

        public Executable(Script script) {

            this.script = script;

            instructions = new List<Instruction>();

            functions = new Dictionary<string, Function>();

            scriptMemory = Memory.AllocateScriptMemory(script.Manager.SharedMemory);

        }

        public bool FunctionExists(string function)
        {

            return functions.ContainsKey(function);

        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Access the script from which the executable was created.
        /// </summary>
        public Script Script
        {
            get { return script; }
        }

        public ReadOnlyCollection<Instruction> Runnable
        {
            get { return instructions.AsReadOnly(); }
        }

        public Dictionary<String, Function> Functions
        {
            get { return functions; }
        }

        public Function MainFunction
        {

            get
            {

                if (!functions.ContainsKey("main"))
                    throw new ParserException("Das Script hat keinen Einstiegspunkt.");

                return functions["main"];

            }

        }

        public Memory ScriptMemory
        {
            get { return scriptMemory; }
        }

        #endregion

    }

}
