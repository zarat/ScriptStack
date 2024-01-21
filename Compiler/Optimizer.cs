using System;
using System.Collections.Generic;
using System.Text;

using ScriptStack.Runtime;

namespace ScriptStack.Compiler
{

    internal class Optimizer
    {

        #region Private Variables

        private Executable executable;
        private bool verbose;
        private bool done;

        #endregion

        #region Private Methods

        private bool Tripleinstruction(int i)
        {
            return i < executable.InstructionsInternal.Count - 2;
        }

        private bool DoubleInstruction(int i)
        {
            return i < executable.InstructionsInternal.Count - 1;
        }

        private bool IsTemporaryVariable(string identifier)
        {
            return identifier.StartsWith("[");
        }

        private bool IsTemporaryVariable(Operand operand)
        {

            if (operand.Type != OperandType.Variable)
                return false;

            return IsTemporaryVariable(operand.Value.ToString());

        }

        private bool IsTemporaryVariableIndex(Operand operand)
        {

            if (operand.Type != OperandType.Pointer)
                return false;

            return operand.Pointer.StartsWith("[");

        }

        private bool IsUnaryOperator(OpCode opcode)
        {

            switch (opcode)
            {

                case OpCode.INC:
                case OpCode.DEC:
                case OpCode.NEG:
                case OpCode.NOT:
                    return true;

                default:
                    return false;

            }

        }

        private bool IsBinaryOperator(OpCode opcode)
        {

            switch (opcode)
            {

                case OpCode.ADD:
                case OpCode.SUB:
                case OpCode.MUL:
                case OpCode.DIV:
                case OpCode.CEQ:
                case OpCode.CNE:
                case OpCode.CG: 
                case OpCode.CGE:
                case OpCode.CL: 
                case OpCode.CLE:
                case OpCode.OR: 
                case OpCode.AND:
                    return true;

                default:
                    return false;

            }

        }

        private void InsertOptimiserInfo(int i, string message)
        {

            if (!verbose)
                return;

            executable.InstructionsInternal.Insert(i, new Instruction(OpCode.DBG, Operand.Literal(0), Operand.Literal("OPTIMIZER: " + message)));

        }

        private void OptimiseBinaryExpressionEvaluation(int i)
        {

            List<Instruction> instructions = executable.InstructionsInternal;

            Instruction instruction0 = instructions[i];

            Instruction instruction1 = instructions[i + 1];

            Instruction instruction2 = instructions[i + 2];

            if (instruction0.OpCode != OpCode.MOV)
                return;

            if (instruction1.OpCode != OpCode.MOV)
                return;

            if (!IsBinaryOperator(instruction2.OpCode))
                return;

            if (!IsTemporaryVariable(instruction0.First))
                return;

            if (!IsTemporaryVariable(instruction1.First))
                return;

            if (!IsTemporaryVariable(instruction2.First))
                return;

            if (!IsTemporaryVariable(instruction2.Second))
                return;

            if (instruction0.First.Value.ToString() == instruction1.First.Value.ToString())
                return;

            if (instruction0.First.Value.ToString() != instruction2.First.Value.ToString())
                return;

            if (instruction1.First.Value.ToString() != instruction2.Second.Value.ToString())
                return;

            InsertOptimiserInfo(i, "Binary Expression Evaluation");

            instruction1.OpCode = instruction2.OpCode;

            instruction1.First = instruction0.First;

            instruction2.OpCode = OpCode.NOP;

            instruction2.First = null;

            instruction2.Second = null;

            done = false;

        }

        private void OptimiseUnaryExpressionAssignment(int iIndex)
        {

            List<Instruction> listInstructions
                = executable.InstructionsInternal;

            Instruction scriptInstruction0 = listInstructions[iIndex];

            Instruction scriptInstruction1 = listInstructions[iIndex + 1];

            Instruction scriptInstruction2 = listInstructions[iIndex + 2];

            if (scriptInstruction0.OpCode != OpCode.MOV)
                return;

            if (!IsUnaryOperator(scriptInstruction1.OpCode))
                return;

            if (scriptInstruction2.OpCode != OpCode.MOV)
                return;

            if (!IsTemporaryVariable(scriptInstruction0.First))
                return;

            if (!IsTemporaryVariable(scriptInstruction1.First))
                return;

            if (!IsTemporaryVariable(scriptInstruction2.Second))
                return;

            if (scriptInstruction0.First.Value.ToString() != scriptInstruction1.First.Value.ToString())
                return;

            if (scriptInstruction0.First.Value.ToString() != scriptInstruction2.Second.Value.ToString())
                return;

            InsertOptimiserInfo(iIndex, "Unary Expression Assignment");

            scriptInstruction0.First = scriptInstruction2.First;

            scriptInstruction1.First = scriptInstruction2.First;

            scriptInstruction2.OpCode = OpCode.NOP;

            scriptInstruction2.First = null;

            scriptInstruction2.Second = null;

            done = false;

        }

        private void OptimiseBinaryExpressionAssignment(int iIndex)
        {

            List<Instruction> listInstructions = executable.InstructionsInternal;

            Instruction scriptInstruction0 = listInstructions[iIndex];
            Instruction scriptInstruction1 = listInstructions[iIndex + 1];
            Instruction scriptInstruction2 = listInstructions[iIndex + 2];

            if (scriptInstruction0.OpCode != OpCode.MOV) return;
            if (!IsBinaryOperator(scriptInstruction1.OpCode)) return;
            if (scriptInstruction2.OpCode != OpCode.MOV) return;
            if (!IsTemporaryVariable(scriptInstruction0.First)) return;
            if (!IsTemporaryVariable(scriptInstruction1.First)) return;
            if (!IsTemporaryVariable(scriptInstruction2.Second)) return;
            if (scriptInstruction0.First.Value.ToString()
                != scriptInstruction1.First.Value.ToString())
                return;
            if (scriptInstruction0.First.Value.ToString()
                != scriptInstruction2.Second.Value.ToString())
                return;

            InsertOptimiserInfo(iIndex, "Binary Expression Assignment");

            scriptInstruction0.First = scriptInstruction2.First;
            scriptInstruction1.First = scriptInstruction2.First;
            scriptInstruction2.OpCode = OpCode.NOP;
            scriptInstruction2.First = null;
            scriptInstruction2.Second = null;

            done = false;
        }

        private void OptimiseInstructionTriples(int iIndex)
        {
            OptimiseUnaryExpressionAssignment(iIndex);

            OptimiseBinaryExpressionEvaluation(iIndex);
            OptimiseBinaryExpressionAssignment(iIndex);
        }

        private void OptimisePushOperation(int iIndex)
        {

            List<Instruction> listInstructions
                = executable.InstructionsInternal;

            Instruction scriptInstruction0
                = listInstructions[iIndex];
            Instruction scriptInstruction1
                = listInstructions[iIndex + 1];

            if (scriptInstruction0.OpCode != OpCode.MOV) return;
            if (scriptInstruction1.OpCode != OpCode.PUSH) return;

            if (!IsTemporaryVariable(scriptInstruction0.First)) return;
            if (!IsTemporaryVariable(scriptInstruction1.First)) return;
            if (scriptInstruction0.First.Value.ToString()
                != scriptInstruction1.First.Value.ToString())
                return;
            
            InsertOptimiserInfo(iIndex, "Push Operation");

            scriptInstruction0.OpCode = OpCode.PUSH;
            scriptInstruction0.First = scriptInstruction0.Second;
            scriptInstruction0.Second = null;
            scriptInstruction1.OpCode = OpCode.NOP;
            scriptInstruction1.First = null;
            scriptInstruction1.Second = null;

            done = false;
        }

        private void OptimisePopOperation(int iIndex)
        {

            List<Instruction> listInstructions
                = executable.InstructionsInternal;

            Instruction scriptInstruction0
                = listInstructions[iIndex];
            Instruction scriptInstruction1
                = listInstructions[iIndex + 1];

            if (scriptInstruction0.OpCode != OpCode.POP) return;
            if (scriptInstruction1.OpCode != OpCode.MOV) return;

            if (!IsTemporaryVariable(scriptInstruction0.First)) return;
            if (!IsTemporaryVariable(scriptInstruction1.Second)) return;
            if (scriptInstruction0.First.Value.ToString()
                != scriptInstruction1.Second.Value.ToString())
                return;

            InsertOptimiserInfo(iIndex, "Pop Operation");

            scriptInstruction0.OpCode = OpCode.POP;
            scriptInstruction0.First = scriptInstruction1.First;
            scriptInstruction1.OpCode = OpCode.NOP;
            scriptInstruction1.First = null;
            scriptInstruction1.Second = null;

            done = false;
        }

        private void OptimiseLiteralAssignment(int iIndex)
        {

            List<Instruction> listInstructions
                = executable.InstructionsInternal;

            Instruction scriptInstruction0
                = listInstructions[iIndex];
            Instruction scriptInstruction1
                = listInstructions[iIndex + 1];
            if (scriptInstruction0.OpCode != OpCode.MOV) return;
            if (scriptInstruction1.OpCode != OpCode.MOV) return;
            if (!IsTemporaryVariable(scriptInstruction0.First)) return;
            if (!IsTemporaryVariable(scriptInstruction1.Second)) return;
            if (scriptInstruction0.First.Value.ToString()
                != scriptInstruction1.Second.Value.ToString())
                return;

            InsertOptimiserInfo(iIndex, "Literal Assignment");

            scriptInstruction0.First = scriptInstruction1.First;
            scriptInstruction1.OpCode = OpCode.NOP;
            scriptInstruction1.First = null;
            scriptInstruction1.Second = null;

            done = false;
        }

        private void OptimiseConditionalJumps(int iIndex)
        {

            List<Instruction> listInstructions
                = executable.InstructionsInternal;

            Instruction scriptInstruction0
                = listInstructions[iIndex];
            Instruction scriptInstruction1
                = listInstructions[iIndex + 1];
            if (scriptInstruction0.OpCode != OpCode.MOV) return;
            if (scriptInstruction1.OpCode != OpCode.JZ
                && scriptInstruction1.OpCode != OpCode.JNZ) return;
            if (!IsTemporaryVariable(scriptInstruction0.First)) return;
            if (!IsTemporaryVariable(scriptInstruction1.First)) return;
            if (scriptInstruction0.First.Value.ToString()
                != scriptInstruction1.First.Value.ToString())
                return;

            InsertOptimiserInfo(iIndex, "Conditional Jump Expressions");

            scriptInstruction0.OpCode = scriptInstruction1.OpCode;
            scriptInstruction0.First = scriptInstruction0.Second;
            scriptInstruction0.Second = scriptInstruction1.Second;
            scriptInstruction1.OpCode = OpCode.NOP;
            scriptInstruction1.First = null;
            scriptInstruction1.Second = null;

            done = false;
        }

        private void OptimiseArrayIndices(int iIndex)
        {

            List<Instruction> listInstructions
                = executable.InstructionsInternal;

            Instruction scriptInstruction0
                = listInstructions[iIndex];
            Instruction scriptInstruction1
                = listInstructions[iIndex + 1];
            if (scriptInstruction0.OpCode != OpCode.MOV) return;
            if (scriptInstruction1.OpCode != OpCode.MOV) return;
            if (scriptInstruction0.Second.Type != OperandType.Variable) return;
            if (scriptInstruction1.First.Type
                != OperandType.Pointer) return;
            if (!IsTemporaryVariable(scriptInstruction0.First)) return;
            if (!IsTemporaryVariableIndex(scriptInstruction1.First)) return;
            if (scriptInstruction0.First.Value.ToString()
                != scriptInstruction1.First.Pointer)
                return;

            InsertOptimiserInfo(iIndex, "Array Index");

            scriptInstruction0.First = scriptInstruction1.First;
            scriptInstruction0.First.Pointer
                = scriptInstruction0.Second.Value.ToString();
            scriptInstruction0.Second = scriptInstruction1.Second;
            scriptInstruction1.OpCode = OpCode.NOP;
            scriptInstruction1.First = null;
            scriptInstruction1.Second = null;

            done = false;
        }

        private void EliminateSequentialJumps(int iIndex)
        {

            List<Instruction> listInstructions
                = executable.InstructionsInternal;

            Instruction scriptInstruction0
                = listInstructions[iIndex];
            Instruction scriptInstruction1
                = listInstructions[iIndex + 1];
            if (scriptInstruction0.OpCode != OpCode.JMP) return;
            if (scriptInstruction0.First.InstructionPointer
                != scriptInstruction1) return;

            InsertOptimiserInfo(iIndex, "Sequentual Jump Elimination");

            scriptInstruction0.OpCode = OpCode.NOP;
            scriptInstruction0.First = null;
            scriptInstruction0.Second = null;

            done = false;
        }

        private void OptimiseInstructionPairs(int iIndex)
        {
            OptimisePushOperation(iIndex);
            OptimisePopOperation(iIndex);
            OptimiseLiteralAssignment(iIndex);
            OptimiseConditionalJumps(iIndex);
            OptimiseArrayIndices(iIndex);
            EliminateSequentialJumps(iIndex);
        }

        private void EliminateSelfAssignments(int iIndex)
        {

            Instruction scriptInstruction
                = executable.InstructionsInternal[iIndex];
            if (scriptInstruction.OpCode != OpCode.MOV) return;
            Operand operand0 = scriptInstruction.First;
            Operand operand1 = scriptInstruction.Second;
            if (operand0.Type != operand1.Type) return;
            if (operand0.Type != OperandType.Variable
                && operand0.Type != OperandType.Member
                && operand1.Type != OperandType.Pointer)
                return;
            if (operand0.Value.ToString() != operand1.Value.ToString())
                return;
            switch (operand0.Type)
            {
                case OperandType.Member:
                    if (operand0.Member.ToString()
                        != operand1.Member.ToString())
                        return;
                    break;
                case OperandType.Pointer:
                    if (operand0.Pointer != operand1.Pointer)
                        return;
                    break;
            }

            InsertOptimiserInfo(iIndex, "Self Assignment Removal");

            scriptInstruction.OpCode = OpCode.NOP;
            scriptInstruction.First = null;
            scriptInstruction.Second = null;

            done = false;
        }

        private void OptimiseConstantConditionalJumps(int iIndex)
        {

            List<Instruction> listInstructions
                = executable.InstructionsInternal;

            Instruction scriptInstruction
                = listInstructions[iIndex];
            if (scriptInstruction.OpCode != OpCode.JZ
                && scriptInstruction.OpCode != OpCode.JNZ) return;
            if (scriptInstruction.First.Type != OperandType.Literal) return;
            if (scriptInstruction.First.Value.GetType() != typeof(bool)) return;
            bool bCondition = (bool) scriptInstruction.First.Value;

            InsertOptimiserInfo(iIndex, "Constant Conditional Jump");

            OpCode opcodeJump = scriptInstruction.OpCode;
            if ((opcodeJump == OpCode.JZ && bCondition)
                || (opcodeJump == OpCode.JNZ && !bCondition))
            {
                scriptInstruction.OpCode = OpCode.JMP;
                scriptInstruction.First = scriptInstruction.Second;
                scriptInstruction.Second = null;
            }
            else
            {
                scriptInstruction.OpCode = OpCode.NOP;
                scriptInstruction.First = null;
                scriptInstruction.Second = null;
            }

            done = false;
        }

        private void OptimiseIncrementsAndDecrements(int iIndex)
        {

            Instruction scriptInstruction
                = executable.InstructionsInternal[iIndex];
            if (scriptInstruction.OpCode != OpCode.ADD
                && scriptInstruction.OpCode != OpCode.SUB) return;
            if (scriptInstruction.First.Type != OperandType.Variable
                && scriptInstruction.First.Type != OperandType.Member
                && scriptInstruction.First.Type != OperandType.Pointer)
                return;
            if (scriptInstruction.Second.Type != OperandType.Literal) return;
            object objectLiteral = scriptInstruction.Second.Value;
            Type typeLiteral = objectLiteral.GetType();
            if (typeLiteral != typeof(int) && typeLiteral != typeof(float)) return;
            if (typeLiteral == typeof(int) && Math.Abs((int)objectLiteral) != 1) return;
            if (typeLiteral == typeof(float) && Math.Abs((float)objectLiteral) != 1.0f) return;

            InsertOptimiserInfo(iIndex, "Increment/Decrement Optimisation");

            float fValue = 0.0f;

            if (typeLiteral == typeof(int))
                fValue = (float)(int)objectLiteral;
            else
                fValue = (float)objectLiteral;


            OpCode opcodeOld = scriptInstruction.OpCode;
            bool bIncrement = opcodeOld == OpCode.ADD && fValue > 0.0f
                || opcodeOld == OpCode.SUB && fValue < 0.0f;

            scriptInstruction.OpCode = bIncrement ? OpCode.INC : OpCode.DEC;
            scriptInstruction.Second = null;

            done = false;
        }

        private void OptimiseSingleInstructions(int iIndex)
        {
            EliminateSelfAssignments(iIndex);
            OptimiseConstantConditionalJumps(iIndex);
            OptimiseIncrementsAndDecrements(iIndex);
        }

        private void TraverseActiveInstructions(
            Dictionary<Instruction, bool> dictScriptInstructionsActive,
            int iNextInstuction)
        {
            while (true)
            {
                Instruction scriptInstruction
                    = executable.InstructionsInternal[iNextInstuction];
                if (dictScriptInstructionsActive.ContainsKey(scriptInstruction))
                    return;

                dictScriptInstructionsActive[scriptInstruction] = true;

                switch (scriptInstruction.OpCode)
                {
                    case OpCode.JMP:
                        iNextInstuction
                            = (int) scriptInstruction.First.InstructionPointer.Address;
                        break;
                    case OpCode.JZ:
                    case OpCode.JNZ:
                        TraverseActiveInstructions(dictScriptInstructionsActive,
                            (int)scriptInstruction.Second.InstructionPointer.Address);
                        ++iNextInstuction;
                        break;
                    case OpCode.CALL:
                    case OpCode.RUN:
                        TraverseActiveInstructions(dictScriptInstructionsActive,
                            (int)scriptInstruction.First.FunctionPointer.EntryPoint.Address);
                        ++iNextInstuction;
                        break;
                    case OpCode.RET:
                        return;
                    default:
                        ++iNextInstuction;
                        break;
                }
            }
        }

        private void EliminateDeadCode()
        {
            executable.Sort();

            Dictionary<Instruction, bool> dictScriptInstructionsActive
                = new Dictionary<Instruction, bool>();

            foreach (Function scriptFunction in executable.Functions.Values)
            {
                TraverseActiveInstructions(dictScriptInstructionsActive,
                    (int) scriptFunction.EntryPoint.Address);
            }

            foreach (Instruction scriptInstruction in executable.InstructionsInternal)
            {
                if (scriptInstruction.OpCode == OpCode.DBG) continue;
                if (scriptInstruction.OpCode == OpCode.DSB) continue;
                if (scriptInstruction.OpCode == OpCode.DB) continue;

                if (!dictScriptInstructionsActive.ContainsKey(scriptInstruction))
                {
                    scriptInstruction.OpCode = OpCode.NOP;
                    scriptInstruction.First = null;
                    scriptInstruction.Second = null;

                    done = false;
                }
            }
        }

        private void EliminateUnusedTempVariables()
        {
            Dictionary<String, Instruction> dictTempVariableAssignments
                = new Dictionary<string, Instruction>();

            foreach (Instruction scriptInstruction in executable.InstructionsInternal)
            {
                OpCode opcode = scriptInstruction.OpCode;
                if ((opcode == OpCode.MOV || opcode == OpCode.DC)
                    && IsTemporaryVariable(scriptInstruction.First))
                {
                    dictTempVariableAssignments[scriptInstruction.First.Value.ToString()]
                        = scriptInstruction;
                }

                Operand operandDest = scriptInstruction.First;
                if (operandDest == null) continue;
                if (opcode != OpCode.MOV && IsTemporaryVariable(operandDest))
                    dictTempVariableAssignments.Remove(
                        operandDest.Value.ToString());
                if ((operandDest.Type == OperandType.Member
                        || operandDest.Type == OperandType.Pointer)
                    && IsTemporaryVariable(operandDest.Value.ToString()))
                    dictTempVariableAssignments.Remove(
                        operandDest.Value.ToString());
                if (operandDest.Type == OperandType.Pointer
                    && IsTemporaryVariable(operandDest.Pointer))
                    dictTempVariableAssignments.Remove(
                        operandDest.Pointer);
                Operand operandSource = scriptInstruction.Second;
                if (operandSource == null) continue;
                switch (operandSource.Type)
                {
                    case OperandType.Literal: continue;
                    case OperandType.Variable:
                    case OperandType.Member:
                        if (IsTemporaryVariable(operandSource.Value.ToString()))
                            dictTempVariableAssignments.Remove(
                                operandSource.Value.ToString());
                        break;
                    case OperandType.Pointer:
                        if (IsTemporaryVariable(operandSource.Value.ToString()))
                            dictTempVariableAssignments.Remove(
                                operandSource.Value.ToString());
                        if (IsTemporaryVariable(operandSource.Pointer))
                            dictTempVariableAssignments.Remove(
                                operandSource.Pointer);
                        break;
                }
            }
            foreach (Instruction scriptInstruction
                in dictTempVariableAssignments.Values)
            {
                String strOldInstruction = scriptInstruction.ToString();

                scriptInstruction.OpCode = OpCode.NOP;
                scriptInstruction.First = null;
                scriptInstruction.Second = null;
            }

            if (dictTempVariableAssignments.Count > 0)
                done = false;
        }

        #endregion

        #region Public Methods

        public Optimizer(Executable executable)
        {

            verbose = false;
            this.executable = executable;

        }

        public void Optimize()
        {

            List<Instruction> instructions = executable.InstructionsInternal;

            done = false;

            while (!done)
            {
                done = true;

                for (int i = 0; i < instructions.Count; i++)
                {

                    if (Tripleinstruction(i))
                        OptimiseInstructionTriples(i);

                    if (DoubleInstruction(i))
                        OptimiseInstructionPairs(i);

                    OptimiseSingleInstructions(i);

                    executable.Clean();

                }
            }

            EliminateUnusedTempVariables();

            EliminateDeadCode();

            executable.Clean();

        }

        public bool OptimizerInfo
        {
            get { return verbose; }
            set { verbose = value; }
        }

        #endregion

    }

}
