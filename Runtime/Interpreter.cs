using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using ScriptStack.Compiler;
using ScriptStack.Runtime;

namespace ScriptStack.Runtime
{

    /// <summary>
    /// The Interpreter finally interprets the parse tree in form of a token stream returned from the <see cref="ScriptStack.Compiler.Parser"/>.
    /// 
    /// \todo Evaluate!!! member access
    /// </summary>
    public class Interpreter
    {

        #region Private Classes

        /// <summary>
        /// For every forward declared function, a new function frame is created including a memory object holding its local variables.
        /// The values are pushed on the stack before they are called.
        /// </summary>
        private class FunctionFrame
        {
            public Function function;
            public Memory localMemory;
            public int nextInstruction;
        }

        #endregion

        #region Private Variables
        
        private Function function;
        private Script script;
        private Executable executable;
        private Stack<FunctionFrame> functionStack;
        private Stack<object> parameterStack;
        private Dictionary<object, Instruction> locks;
        private List<Interpreter> jobs;
        private Instruction instruction;
        private Memory localMemory;
        private bool interrupt;
        private bool interrupted;
        private bool finished;

        #endregion

        #region Private Methods

        private Host host;

        // \todo make models accessible like members
        private object Evaluate(Operand operand)
        {

            object src = null;

            switch (operand.Type)
            {

                case OperandType.Literal:
                    return operand.Value;

                case OperandType.Variable:
                    return localMemory[(string) operand.Value];

                /* 
                 * Members are indexed with a '.' like
                 * 
                 * var a = [];
                 * a.b = 1;
                 * 
                 * currently only arrays can have members but strings can be numerically indexed 
                 * 
                 */
                case OperandType.Member:

                    src = localMemory[(string)operand.Value];

                    if (src.GetType() == typeof(ArrayList))
                    {

                        /*
                         * An array has an internal member "toString"
                         * \deprecated
                         */
                        if ((string)operand.Member == "toString")
                        {

                            ArrayList array = (ArrayList)src;

                            StringBuilder sb = new StringBuilder();

                            foreach (KeyValuePair<object, object> element in array)
                            {
                                sb.Append(element.Value.ToString());
                            }

                            return sb;

                        }

                        ArrayList associativeArray = (ArrayList)src;

                        object objectValue = associativeArray[operand.Member];

                        return objectValue;
                    }

                    else if (src.GetType() == typeof(string))
                    {

                        string strSource = (string)src;

                        object objectIndex = operand.Member;

                        if (objectIndex.GetType() == typeof(string))
                            if (((string)objectIndex) == "length")
                                return strSource.Length;

                        if (objectIndex.GetType() != typeof(int))
                            throw new ExecutionException("Ein String ist nur numerisch indexierbar.");

                        return strSource[(int)objectIndex] + "";

                    }

                    // \todo member access test
                    else
                    {

                        object objectIndex = operand.Member;

                        object res = false;

                        System.Reflection.MethodInfo method = ((object)src).GetType().GetMethod((string)objectIndex);

                        try
                        {
                            res = method.Invoke(src, new object[0]);
                        }
                        catch(Exception) {
                            parameterStack.Push(NullReference.Instance);
                            return null;
                        }

                        return res;

                    }                  
                    
                    //else throw new ExecutionException("Der Typ '"+ operand.Type + "' kann an dieser Stelle nicht verarbeitet werden.");

                /* 
                 * Arrays can be indexed with a "string" like
                 * 
                 * var a = [];
                 * a["str"] = "Hello world";
                 * 
                 * accually a pointer is an associative array
                 *
                 */
                case OperandType.Pointer:

                    src = localMemory[(string)operand.Value];

                    if (src.GetType() == typeof(ArrayList))
                    {

                        object objectIndex = localMemory[operand.Pointer];

                        return ((ArrayList)src)[objectIndex];

                    }

                    else if (src.GetType() == typeof(string))
                    {

                        string strSource = (string)src;

                        object objectIndex = localMemory[operand.Pointer];

                        if (objectIndex.GetType() != typeof(int))
                            throw new ExecutionException("Ein String ist nur numerisch indexierbar.");

                        return strSource[(int)objectIndex] + "";

                    }

                    else
                        throw new ExecutionException("Nur Arrays und Strings sind indexierbar.");

                default:
                    throw new ExecutionException("Der Typ '"+ operand.Type + "' kann an dieser Stelle nicht verarbeitet werden.");

            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dst"></param>
        /// <param name="val"></param>
        private void Assignment(Operand dst, object val)
        {

            string identifier = (string)dst.Value;

            switch (dst.Type)
            {

                case OperandType.Variable:
                    localMemory[identifier] = val;
                    break;

                case OperandType.Member:
                case OperandType.Pointer:
                    ArrayList array = null;

                    object tmp = null;

                    if (localMemory.Exists(identifier))
                        tmp = localMemory[identifier];

                    else
                        tmp = NullReference.Instance;

                    if (tmp.GetType() != typeof(ArrayList))
                        throw new ExecutionException("Das Ziel '" + dst + "' vom Typ '" + tmp.GetType().ToString() + "' ist nicht indexierbar.");

                    else
                        array = (ArrayList)tmp;

                    if (dst.Type == OperandType.Member)
                        array[dst.Member] = val;

                    else
                        array[localMemory[dst.Pointer]] = val;

                    break;

                case OperandType.Literal:
                    throw new ExecutionException("Einem Literal kann nichts zugewiesen werden.");

            }

        }

        /// <summary>
        /// Ausführung arithmetischer Operationen
        /// 
        /// dest = dest OP source
        /// 
        /// Special rules for strings and arrays
        /// </summary>
        private void Arithmetic()
        {

            String strIdentifierDest = (String)instruction.First.Value;

            object objectValueDest = Evaluate(instruction.First);
            object objectValueSource = Evaluate(instruction.Second);

            Type typeDest = objectValueDest.GetType();
            Type typeSource = objectValueSource.GetType();

            // handle array and string concatenation
            if (instruction.OpCode == OpCode.ADD)
            {
                if (typeDest == typeof(String))
                {
                    Assignment(instruction.First, objectValueDest.ToString() + objectValueSource.ToString());
                    return;
                }

                if (typeDest == typeof(ArrayList))
                {
                    ((ArrayList)objectValueDest).Add(objectValueSource);
                    return;
                }
            }

            // handle array and string subtraction
            if (instruction.OpCode == OpCode.SUB)
            {
                if (typeDest == typeof(String))
                {
                    Assignment(instruction.First,
                        objectValueDest.ToString().Replace(objectValueSource.ToString(), ""));
                    return;
                }
                if (typeDest == typeof(ArrayList))
                {
                    ((ArrayList)objectValueDest).Subtract(objectValueSource);
                    return;
                }
            }

            /*
            float fValueDest = 0.0f;
            float fValueSource = 0.0f;
            float fResult = 0.0f;
            */

            /*
            var fValueDest = 0.0;
            var fValueSource = 0.0;
            var fResult = 0.0;

            if (typeDest == typeof(int))
                fValueDest = (float)(int)objectValueDest;
            else if (typeDest == typeof(float))
                fValueDest = (float)objectValueDest;
            else if (typeDest == typeof(double))
                fValueDest = (double)objectValueDest;
            else if (typeDest == typeof(decimal))
                fValueDest = (decimal)objectValueDest;
            else
                throw new ScriptStackException("Values of type '" + typeDest.Name + "' cannot be used in arithmetic instructions.");

            if (typeSource == typeof(int))
                fValueSource = (float)(int)objectValueSource;
            else if (typeSource == typeof(float))
                fValueSource = (float)objectValueSource;
            else if (typeSource == typeof(float))
                fValueSource = (double)objectValueSource;
            else if (typeSource == typeof(decimal))
                fValueSource = (decimal)objectValueSource;
            else
                throw new ScriptStackException("Values of type '" + typeSource.Name + "' cannot be used in arithmetic instructions.");

            switch (instruction.OpCode)
            {
                case OpCode.ADD: fResult = fValueDest + fValueSource; break;
                case OpCode.SUB: fResult = fValueDest - fValueSource; break;
                case OpCode.MUL: fResult = fValueDest * fValueSource; break;
                case OpCode.DIV: fResult = fValueDest / fValueSource; break;
                case OpCode.MOD: fResult = fValueDest % fValueSource; break;
                default:
                    throw new ExecutionException("Invalid arithmetic instruction '" + instruction.OpCode + "'.");
            }

            if (typeDest == typeof(int) && typeSource == typeof(int))
                Assignment(instruction.First, (int)fResult);
            else if (typeDest == typeof(float) && typeSource == typeof(float))
                Assignment(instruction.First, (float)fResult);
            else if (typeDest == typeof(double) && typeSource == typeof(double))
                Assignment(instruction.First, (double)fResult);
            else
                Assignment(instruction.First, (decimal)fResult);
            */

            /*
            decimal a = ToDecimal(objectValueDest, typeDest);
            decimal b = ToDecimal(objectValueSource, typeSource);

            decimal r = instruction.OpCode switch
            {
                OpCode.ADD => a + b,
                OpCode.SUB => a - b,
                OpCode.MUL => a * b,
                OpCode.DIV => a / b,
                OpCode.MOD => a % b,
                _ => throw new ExecutionException($"Invalid arithmetic instruction '{instruction.OpCode}'.")
            };

            // Ergebnis in den Zieltyp (dest) zurück
            if (typeDest == typeof(int)) Assignment(instruction.First, (int)r);
            else if (typeDest == typeof(float)) Assignment(instruction.First, (float)r);
            else if (typeDest == typeof(double)) Assignment(instruction.First, (double)r);
            else if (typeDest == typeof(decimal)) Assignment(instruction.First, r);
            else throw new ScriptStackException($"Values of type '{typeDest.Name}' cannot be used in arithmetic instructions.");
            */

            decimal valueDest;
            decimal valueSource;
            decimal result;

            // DEST -> decimal
            switch (Type.GetTypeCode(typeDest))
            {
                case TypeCode.Int32: valueDest = (int)objectValueDest; break;
                case TypeCode.Single: valueDest = (decimal)(float)objectValueDest; break;
                case TypeCode.Double: valueDest = (decimal)(double)objectValueDest; break;
                case TypeCode.Decimal: valueDest = (decimal)objectValueDest; break;
                default:
                    throw new ScriptStackException("Values of type '" + typeDest.Name + "' cannot be used as destination in arithmetic instructions.");
            }

            // SOURCE -> decimal
            switch (Type.GetTypeCode(typeSource))
            {
                case TypeCode.Int32: valueSource = (int)objectValueSource; break;
                case TypeCode.Single: valueSource = (decimal)(float)objectValueSource; break;
                case TypeCode.Double: valueSource = (decimal)(double)objectValueSource; break;
                case TypeCode.Decimal: valueSource = (decimal)objectValueSource; break;
                default:
                    throw new ScriptStackException("Values of type '" + typeSource.Name + "' cannot be used as source in arithmetic instructions.");
            }

            // Arithmetic (decimal)
            switch (instruction.OpCode)
            {
                case OpCode.ADD: result = valueDest + valueSource; break;
                case OpCode.SUB: result = valueDest - valueSource; break;
                case OpCode.MUL: result = valueDest * valueSource; break;
                case OpCode.DIV: result = valueDest / valueSource; break;
                case OpCode.MOD: result = valueDest % valueSource; break;
                default:
                    throw new ExecutionException("Invalid arithmetic instruction '" + instruction.OpCode + "'.");
            }

            // Back to DEST type
            switch (Type.GetTypeCode(typeDest))
            {
                case TypeCode.Int32: Assignment(instruction.First, (int)result); break;
                case TypeCode.Single: Assignment(instruction.First, (float)result); break;
                case TypeCode.Double: Assignment(instruction.First, (double)result); break;
                case TypeCode.Decimal: Assignment(instruction.First, result); break;
                default:
                    // eigentlich schon oben abgefangen, aber zur Sicherheit:
                    throw new ScriptStackException("Values of type '" + typeDest.Name + "' cannot be used in arithmetic instructions.");
            }

        }

        /// <summary>
        /// Ausführung einer Vergleichsoperation
        /// 
        /// If one of both is of type 'null' only certain operations are allowed.
        /// 
        /// If one of both is a string, both are converted to string and alphabetically evaluated. 
        /// 
        /// Numbers are converted to type 'double'.
        /// 
        /// \todo separation of double and float, result is always double (if not int or string)
        /// </summary>
        private void Relation()
        {

            string identifier = (string)instruction.First.Value;

            object dst = Evaluate(instruction.First);

            object src = Evaluate(instruction.Second);

            Type typeDest = dst.GetType();

            Type typeSource = src.GetType();

            bool result = false;

            /**
             * Only equations may reference null
             * \todo check null
             */
            if (typeDest == typeof(NullReference) || typeSource == typeof(NullReference))
            {

                switch (instruction.OpCode)
                {

                    case OpCode.CEQ:
                        result = dst == src;
                        break;

                    case OpCode.CNE:
                        result = dst != src;
                        break;

                    default:
                        string message = "";

                        if (typeDest == typeof(NullReference) && typeSource == typeof(NullReference))
                            message = "Die Operation '" + instruction.OpCode + "' kann nicht auf den Typ 'null' angewendet werden.";

                        else if (typeDest == typeof(NullReference))
                            message = "Die Operation '" + instruction.OpCode + "' kann nicht auf den Typ 'null' als Ziel (links des Operators) angewendet werden.";

                        else
                            message = "Die Operation '" + instruction.OpCode + "' kann nicht auf den Typ 'null' als Quelle (rechts des Operators) angewendet werden.";

                        throw new ExecutionException(message);
                            
                        
                }

                // As a last resort, just assign it
                Assignment(instruction.First, result);

                return;

            }
           
            /**
             * If one of both is a string both are converted to string and alphabetically compared
             * \todo check string
             */
            if (typeDest == typeof(string) || typeSource == typeof(string))
            {

                string strDst = "" + dst;

                string strSrc = "" + src;

                switch (instruction.OpCode)
                {

                    case OpCode.CEQ:
                        result = strDst == strSrc;
                        break;

                    case OpCode.CNE:
                        result = strDst != strSrc;
                        break;

                    case OpCode.CG:
                        result = strDst.CompareTo(strSrc) > 0;
                        break;

                    case OpCode.CGE:
                        result = strDst.CompareTo(strSrc) >= 0;
                        break;

                    case OpCode.CL:
                        result = strDst.CompareTo(strSrc) < 0;
                        break;

                    case OpCode.CLE:
                        result = strDst.CompareTo(strSrc) <= 0;
                        break;

                    default:
                        throw new ExecutionException("Die Operation '" + instruction.OpCode + "' kann nicht auf den Typ 'String' angewendet werden.");

                }

                Assignment(instruction.First, result);

                return;

            }

            double dstVal = 0.0;

            double srcVal = 0.0;

            if (typeDest == typeof(int))
                dstVal = double.Parse("" + (int)dst);

            else if (typeDest == typeof(float))
                dstVal = double.Parse("" + (float)dst);

            else if (typeDest == typeof(char))
                dstVal = double.Parse("" + (char)dst);

            else if (typeDest == typeof(double))
                dstVal = (double)dst;

            else
                throw new ExecutionException("Der Typ '" + typeDest.Name + "' kann in relationalen Operationen als Ziel (links des Operators) nicht verarbeitet werden.");

            if (typeSource == typeof(int))
                srcVal = double.Parse("" + (int)src);

            else if (typeSource == typeof(float))
                srcVal = double.Parse("" + (float)src);

            else if (typeSource == typeof(char))
                srcVal = double.Parse("" + (char)src);

            else if (typeSource == typeof(double))
                srcVal = (double)src;

            else
                throw new ExecutionException("Der Typ '" + typeSource.Name + "' kann in relationalen Operationen als Quelle (rechts des Operators) nicht verarbeitet werden.");

            switch (instruction.OpCode)
            {

                case OpCode.CEQ:
                    result = dstVal == srcVal;
                    break;

                case OpCode.CNE:
                    result = dstVal != srcVal;
                    break;

                case OpCode.CG:
                    result = dstVal > srcVal;
                    break;

                case OpCode.CGE:
                    result = dstVal >= srcVal;
                    break;

                case OpCode.CL:
                    result = dstVal < srcVal;
                    break;

                case OpCode.CLE:
                    result = dstVal <= srcVal;
                    break;

                default:
                    throw new ExecutionException("Der OpCode '" + instruction.OpCode + "' kann in einer relationalen Operation nicht verarbeitet werden.");

            }

            Assignment(instruction.First, result);

        }

        /// <summary>
        /// Usually its a boolean operation but it allows numerics too
        /// 
        /// \todo what about numerics in relations?
        /// </summary>
        private void Logic()
        {

            string identifier = (string)instruction.First.Value;

            object dst = Evaluate(instruction.First);

            object src = Evaluate(instruction.Second);

            Type typeDest = dst.GetType();

            Type typeSource = src.GetType();

            bool result = false;

            bool dstVal = false; 

            bool srcVal = false; 

            if (typeSource == typeof(bool))
                srcVal = (bool)src;

            else if (typeSource == typeof(NullReference))
                srcVal = false;

            else
                srcVal = ((double)src != 0.0) ? true : false;


            if (typeDest == typeof(bool))
                dstVal = (bool)dst;

            else if (typeDest == typeof(NullReference))
                dstVal = false;

            else
                dstVal = ((double)dst != 0.0) ? true : false;

            switch (instruction.OpCode)
            {

                case OpCode.AND:
                    result = dstVal && srcVal;
                    break;

                case OpCode.OR:
                    result = dstVal || srcVal;
                    break;

                default:
                    throw new ExecutionException("Der OpCode '" + instruction.OpCode + "' kann in einer logischen Operation nicht verarbeitet werden.");

            }

            Assignment(instruction.First, result);

        }

        private void Iterator(ArrayList array)
        {

            if (array.Count == 0)
                return;

            object iterator = Evaluate(instruction.First);

            bool key = false;

            object next = null;

            foreach (object tmp in array.Keys)
            {

                if (key)
                {
                    next = tmp;
                    break;
                }

                if (tmp == iterator)
                    key = true;

            }

            if (!key)
            {

                Dictionary<object, object>.KeyCollection.Enumerator keys = array.Keys.GetEnumerator();

                keys.MoveNext();

                next = keys.Current;

            }

            if (next == null)
                next = NullReference.Instance;

            localMemory[instruction.First.Value.ToString()] = next;

        }

        private void Iterator(string str)
        {

            if (str.Length == 0)
                return;

            object iterator = Evaluate(instruction.First);

            if (iterator.GetType() != typeof(int))
            {

                localMemory[instruction.First.Value.ToString()] = 0;

                return;

            }

            int elements = (int)iterator;

            if (elements < str.Length - 1)
                localMemory[instruction.First.Value.ToString()] = elements + 1;

            else
                localMemory[instruction.First.Value.ToString()] = NullReference.Instance;

        }



        private void DBG()
        {
        }

        private void NOP()
        {
        }

        private void INT()
        {
            interrupted = true;
        }

        /// <summary>
        /// Return from current function frame to the last one on the stack, copying local memory to the new one
        /// </summary>
        private void RET()
        {

            functionStack.Pop();

            if (functionStack.Count == 0)
            {
                finished = true;
                return;
            }

            localMemory = functionStack.Peek().localMemory;

        }



        private void PUSH()
        {
            parameterStack.Push(Evaluate(instruction.First));
        }

        /// <summary>
        /// Pop a value from the stack into an atom
        /// </summary>
        private void POP()
        {

            object tmp = parameterStack.Pop();

            Operand operand = instruction.First;

            switch (operand.Type)
            {

                case OperandType.Variable:
                    localMemory[operand.Value.ToString()] = tmp;
                    break;

                case OperandType.Member:
                case OperandType.Pointer:
                    if (localMemory[operand.Value.ToString()].GetType() != typeof(ArrayList))
                        throw new ExecutionException("Ein 'Array' wurde erwartet.");

                    ArrayList array = (ArrayList)localMemory[operand.Value.ToString()];

                    if (operand.Type == OperandType.Member)
                        array[operand.Member] = tmp;

                    else
                        array[localMemory[operand.Pointer]] = tmp;

                    break;

                default:
                    throw new ExecutionException("Der Typ '" + operand.Type + "' kann an dieser Stelle nicht verarbeitet werden.");

            }

        }

        /// <summary>
        /// Basic assignment
        /// </summary>
        private void MOV()
        {

            Assignment(instruction.First, Evaluate(instruction.Second));
        }


        /// <summary>
        /// 
        /// </summary>
        private void ADD()
        {
            Arithmetic();
        }

        /// <summary>
        /// 
        /// </summary>
        private void SUB()
        {
            Arithmetic();
        }

        /// <summary>
        /// 
        /// </summary>
        private void MUL()
        {
            Arithmetic();
        }

        /// <summary>
        /// 
        /// </summary>
        private void DIV()
        {
            Arithmetic();
        }

        /// <summary>
        /// 
        /// </summary>
        private void MOD()
        {
            Arithmetic();
        }



        /// <summary>
        /// 
        /// </summary>
        private void INC()
        {

            string identifier = (string)instruction.First.Value;
            
            object val = Evaluate(instruction.First);
            
            Type typeDest = val.GetType();
            
            if (typeDest == typeof(char))
            {
                char c = (char)val;
                localMemory[identifier] = (char)(c + 1);
            }
            else if (typeDest == typeof(int))
                localMemory[identifier] = (int)val + 1;
            
            else if (typeDest == typeof(float))
                localMemory[identifier] = (float)val + 1;
            
            else if (typeDest == typeof(double))
                localMemory[identifier] = (double)val + 1;
            
            else if (typeDest == typeof(decimal))
                localMemory[identifier] = (decimal)val + 1;
            
            else
                throw new ExecutionException("Der Typ '" + typeDest.Name + "' kann nicht inkrementiert werden.");

        }

        /// <summary>
        /// 
        /// </summary>
        private void DEC()
        {

            string identifier = (string)instruction.First.Value;

            object val = Evaluate(instruction.First);

            Type typeDest = val.GetType();

            if (typeDest == typeof(char))
            {
                char c = (char)val;
                localMemory[identifier] = (char)(c - 1);
            }
            else if (typeDest == typeof(int))
                localMemory[identifier] = (int)val - 1;

            else if (typeDest == typeof(float))
                localMemory[identifier] = (float)val - 1;

            else if (typeDest == typeof(double))
                localMemory[identifier] = (double)val - 1;

            else if (typeDest == typeof(decimal))
                localMemory[identifier] = (decimal)val - 1;

            else
                throw new ExecutionException("Der Typ '" + typeDest.Name + "' kann nicht dekrementiert werden.");

        }

        /// <summary>
        /// Negate a literal (* -1)
        /// </summary>
        private void NEG()
        {

            string identifier = (string)instruction.First.Value;

            object val = Evaluate(instruction.First);

            Type typeDest = val.GetType();

            if (typeDest == typeof(int))
                localMemory[identifier] = (int)val * -1;
            
            else if (typeDest == typeof(float))
                localMemory[identifier] = (float)val * -1;

            else if (typeDest == typeof(double))
                localMemory[identifier] = (double)val * -1;

            else if (typeDest == typeof(char))
                localMemory[identifier] = (char)val * -1;           

            else
                throw new ExecutionException("Der Typ '" + typeDest.Name + "' kann nicht negiert werden.");

        }

        /// <summary>
        /// 
        /// </summary>
        private void SHL()
        {

            string identifier = null;

            object val = Evaluate(instruction.Second);

            Operand operand = instruction.First;

            switch (operand.Type)
            {

                case OperandType.Variable:
                    int res = (int)localMemory[(string)operand.Value] << (int)val;

                    identifier = operand.Value.ToString();

                    localMemory[identifier] = res;

                    break;

                default:
                    throw new ExecutionException("Der Typ '" + operand.Type + "' kann in Bitoperationen nicht verarbeitet werden.");

            }

        }

        /// <summary>
        /// 
        /// </summary>
        private void SHR()
        {

            string identifier = null;

            object val = Evaluate(instruction.Second);

            Operand operand = instruction.First;

            switch (operand.Type)
            {

                case OperandType.Variable:
                    int res = (int)localMemory[(string)operand.Value] >> (int)val;

                    identifier = operand.Value.ToString();

                    localMemory[identifier] = res;

                    break;

                default:
                    throw new ExecutionException("Der Typ '" + operand.Type + "' kann in Bitoperationen nicht verarbeitet werden.");

            }

        }



        private void TEST()
        {

            string identifier = (string)instruction.First.Value;

            object val = Evaluate(instruction.First);

            localMemory[identifier] = val == NullReference.Instance;

        }

        /// <summary>
        /// 
        /// </summary>
        private void CEQ()
        {
            Relation();
        }

        /// <summary>
        /// 
        /// </summary>
        private void CNE()
        {
            Relation();
        }

        /// <summary>
        /// 
        /// </summary>
        private void CG()
        {
            Relation();
        }

        /// <summary>
        /// 
        /// </summary>
        private void CGE()
        {
            Relation();
        }

        /// <summary>
        /// 
        /// </summary>
        private void CL()
        {
            Relation();
        }

        /// <summary>
        /// 
        /// </summary>
        private void CLE()
        {
            Relation();
        }



        private void OR()
        {
            Logic();
        }

        private void AND()
        {
            Logic();
        }

        /// <summary>
        /// Negate a boolean or int
        /// 
        /// \todo int?
        /// </summary>
        private void NOT()
        {

            string identifier = (string)instruction.First.Value;

            object val = Evaluate(instruction.First);
 
            Type typeDest = val.GetType();

            if (typeDest != typeof(bool) && typeDest != typeof(int))
                throw new ExecutionException("Der Typ '" + typeDest.Name + "' kann nicht negiert werden.");

            if (typeDest == typeof(bool))
                localMemory[identifier] = !((bool)val);

            else if (typeDest == typeof(int))
                localMemory[identifier] = (int)val * -1;

        }



        private void ORB()
        {

            string identifier = null;

            object val = Evaluate(instruction.Second);

            Operand operand = instruction.First;

            switch (operand.Type)
            {

                case OperandType.Variable:
                    int res = (int)val | (int)localMemory[(string)operand.Value];

                    identifier = operand.Value.ToString();

                    localMemory[identifier] = res;

                    break;

                default:
                    throw new ExecutionException("Der Typ '" + operand.Type + "' kann an dieser Stelle nicht verarbeitet werden.");

            }

        }

        private void ANDB()
        {

            string identifier = null;

            object val = Evaluate(instruction.Second);

            Operand operand = instruction.First;

            switch (operand.Type)
            {

                case OperandType.Variable:
                    int res = (int)localMemory[(string)operand.Value] & (int)val;

                    identifier = operand.Value.ToString();

                    localMemory[identifier] = res;

                    break;

                default:
                    throw new ExecutionException("Operand type '" + operand.Type + "' not supported by logical AND instruction.");

            }

        }

        private void NOTB()
        {

            if (instruction.First.Type != OperandType.Variable)
                throw new ExecutionException("Operand type '" + instruction.First.Type + "' not supported by NOTB instruction.");

            string dest = (string)instruction.First.Value;

            object srcObj = Evaluate(instruction.Second);

            // (aktuell unterstützt dein Interpreter bei BitOps sowieso int)
            int src = (int)srcObj;

            localMemory[dest] = ~src;

        }

        private void XOR()
        {
            
            string identifier = null;

            object val = Evaluate(instruction.Second);

            Operand operand = instruction.First;

            switch (operand.Type)
            {

                case OperandType.Variable:
                    int res = (int)val ^ (int)localMemory[(string)operand.Value];

                    identifier = operand.Value.ToString();

                    localMemory[identifier] = res;

                    break;

                default:
                    throw new ExecutionException("Der Typ '" + operand.Type + "' kann an dieser Stelle nicht verarbeitet werden.");

            }

        }

        /// <summary>
        /// Jump to the address the first operator points at
        /// </summary>
        private void JMP()
        {

            FunctionFrame frame = functionStack.Peek();

            frame.nextInstruction = (int)instruction.First.InstructionPointer.Address;

        }
        
        /// <summary>
        /// Jump to the instruction the second operand is pointing at if the first operand is true
        /// </summary>
        private void JZ()
        {

            if (!(bool)Evaluate(instruction.First))
                return;

            Instruction target = instruction.Second.InstructionPointer;

            FunctionFrame frame = functionStack.Peek();

            frame.nextInstruction = (int)target.Address;

        }

        /// <summary>
        /// Jump to the instruction the second operand is pointing at if the first operand is false
        /// </summary>
        private void JNZ()
        {
 
            if ((bool)Evaluate(instruction.First))
                return;

            FunctionFrame frame = functionStack.Peek();

            frame.nextInstruction = (int)instruction.Second.InstructionPointer.Address;

        }



        private void DSB()
        {

            throw new ExecutionException("DCG opcodes cannot be executed within a function frame.");

        }

        private void DB()
        {

            localMemory[(string)instruction.First.Value] = NullReference.Instance;

        }

        private void DC()
        {

            if (instruction.First.Type != OperandType.Variable)
                throw new ExecutionException("Error in array declaration.");

            ArrayList array = new ArrayList();

            localMemory[instruction.First.Value.ToString()] = array;

        }

        private void DCO()
        {
            
            string strIdentifier = null;

            object objectValue = Evaluate(instruction.Second);

            Operand operand = instruction.First;

            switch (operand.Type)
            {

                case OperandType.Variable:
                    int res = (int)objectValue ^ (int)localMemory[(string)operand.Value];

                    strIdentifier = operand.Value.ToString();

                    localMemory[strIdentifier] = res;

                    break;

                default:
                    throw new ExecutionException("Operand type '" + operand.Type + "' not supported by logical LNEG instruction.");

            }

        }

        /// <summary>
        /// A pointer in foreach loops
        /// </summary>
        /// \todo error messages
        private void PTR()
        {

            if (instruction.First.Type != OperandType.Variable)
                throw new ExecutionException("Error in PTR.");

            if (instruction.Second.Type != OperandType.Variable)
                throw new ExecutionException("Error in PTR.");

            object enumerable = localMemory[instruction.Second.Value.ToString()];

            if (enumerable.GetType() == typeof(ArrayList))
                Iterator((ArrayList)enumerable);

            else if (enumerable.GetType() == typeof(string))
                Iterator((string)enumerable);

            else
                throw new ExecutionException("Error in PTR.");

        }


        // if any errors add "localMemory = frame.localMemory;" after the allocation
        /// <summary>
        /// Call a Function
        /// </summary>
        private void CALL()
        {

            Function function = instruction.First.FunctionPointer;

            FunctionFrame frame = new FunctionFrame();

            frame.function = function;

            frame.localMemory = Memory.AllocateLocalMemory(executable.ScriptMemory);

            frame.nextInstruction = (int) function.EntryPoint.Address;

            functionStack.Push(frame);

        }

        /// <summary>
        /// Invoke a Routine, if no result is specified a null is pushed onto the stack
        /// 
        /// The Verify() function will skip null and void parameters.
        /// </summary>
        /// \todo get rid of try-catch
        private void INV()
        {

            Routine routine = instruction.First.RoutinePointer;

            Host stackHandler = null;

            if (routine.Handler == null)
            {
                if (host != null)
                    stackHandler = host;
            }

            else
                stackHandler = routine.Handler;

            List<object> parameters = new List<object>();

            for (int i = 0; i < routine.ParameterTypes.Count; i++)
                parameters.Insert(0, parameterStack.Pop());
           
            routine.Verify(parameters);

            object objectResult = null;

            if (stackHandler != null)
            {

                try {
                    objectResult = stackHandler.Invoke(routine.Name, parameters);
                }
                catch (Exception) {

                    parameterStack.Push(NullReference.Instance);
                    return;

                }

                routine.Verify(objectResult);

            }

            if (objectResult == null)
                objectResult = NullReference.Instance;

            parameterStack.Push(objectResult);

            if (interrupt)
                interrupted = true;

        }


        /// <summary>
        /// Run a Function in Background
        /// 
        /// An example
        /// 
        /// ```
        /// 
        /// var LOCK;
        /// var queue;
        /// 
        /// function enqueue()
        /// {
        /// 
        ///     var value = 0;
        /// 
        ///     while (true)
        ///     {
        /// 
        ///         lock LOCK
        ///         {
        /// 
        ///             queue += value;
        ///             print("Queued: " + value + "\n");
        ///             ++value;
        /// 
        ///         }
        /// 
        ///     }
        /// 
        /// }
        /// 
        /// function dequeue()
        /// {
        /// 
        ///     while (true)
        ///     {
        /// 
        ///         lock LOCK
        ///         {
        /// 
        ///             var index, value;
        /// 
        ///             foreach (index, value in queue)
        ///     {
        /// 
        ///                 queue -= value;
        ///                 print("Dequeued: " + value + "\n");
        /// 
        ///                 break;
        /// 
        ///             }
        /// 
        ///         }
        /// 
        ///     }
        /// 
        /// }
        /// 
        /// function main()
        /// {
        /// 
        ///     LOCK = "queue_lock";
        ///     queue = { };
        /// 
        ///     run enqueue();
        ///     run dequeue();
        /// 
        ///     while (true)
        ///         yield;
        /// 
        /// }
        /// 
        /// ```
        /// 
        /// </summary>
        private void RUN()
        {

            Function function = instruction.First.FunctionPointer;

            List<object> parameters = new List<object>();

            for (int i = 0; i < function.ParameterCount; i++)
                parameters.Insert(0, parameterStack.Pop());

            Interpreter job = new Interpreter(function, parameters);

            job.Handler = host;

            jobs.Add(job);

        }

        private void LOCK()
        {

            object first = Evaluate(instruction.First);

            if (first.GetType() == typeof(NullReference))
                throw new ExecutionException("Lock key must be a literal value.");

            if (script.Manager.Locks.ContainsKey(first))
            {

                Interpreter locked = script.Manager.Locks[first];

                if (locked == this && locks[first] != instruction)
                    throw new ExecutionException("Nested locks cannot share the same locking key.");

                FunctionFrame functionFrame = functionStack.Peek();
                --functionFrame.nextInstruction;
                interrupted = true;

            }

            else
            {

                script.Manager.Locks[first] = this;

                locks[first] = instruction;

            }

        }

        private void FREE()
        {

            object first = Evaluate(instruction.First);

            if (first.GetType() == typeof(NullReference))
                throw new ExecutionException("Lock key must be a literal value.");

            if (!script.Manager.Locks.ContainsKey(first))
                throw new ExecutionException("Lock '" + first + "' is already unlocked.");

            locks.Remove(first);

            script.Manager.Locks.Remove(first);

        }


        private uint ExecuteBackgroundJobs()
        {

            uint executed = 0;

            foreach (Interpreter job in jobs)
                if (!job.Finished)
                    executed += job.Interpret(1);

            for (int i = jobs.Count - 1; i >= 0; i--)
                if (jobs[i].Finished)
                    jobs.RemoveAt(i);

            return executed;

        }

        private void ExecuteInstruction()
        {

            instruction = executable.InstructionsInternal[functionStack.Peek().nextInstruction++];

            switch (instruction.OpCode)
            {

                case OpCode.DBG:  DBG();  break;
                case OpCode.NOP:  NOP();  break;
                
                case OpCode.INT:  INT();  break;
                case OpCode.RET: RET(); break;

                case OpCode.PUSH: PUSH(); break;
                case OpCode.POP: POP(); break;
                case OpCode.MOV:  MOV();  break;
                
                case OpCode.ADD:  Arithmetic();  break;
                case OpCode.SUB:  Arithmetic();  break;
                case OpCode.MUL:  Arithmetic();  break;
                case OpCode.DIV:  Arithmetic();  break;
                case OpCode.MOD:  Arithmetic(); break;

                case OpCode.INC: INC(); break;
                case OpCode.DEC: DEC(); break;
                case OpCode.NEG: NEG(); break;
                case OpCode.SHL: SHL(); break;
                case OpCode.SHR: SHR(); break;

                case OpCode.TEST:  TEST(); break;
                case OpCode.CEQ:  CEQ();  break;
                case OpCode.CNE:  CNE();  break;
                case OpCode.CG:   CG();   break;
                case OpCode.CGE:  CGE();  break;
                case OpCode.CL:   CL();   break;
                case OpCode.CLE:  CLE();  break;

                case OpCode.OR:   OR();   break;
                case OpCode.AND:  AND();  break;
                case OpCode.NOT:  NOT();  break;
              
                case OpCode.ORB: ORB(); break;
                case OpCode.ANDB: ANDB(); break;
                case OpCode.NOTB: NOTB(); break;
                case OpCode.XOR: XOR(); break;

                case OpCode.JMP: JMP(); break;
                case OpCode.JZ: JZ(); break;
                case OpCode.JNZ: JNZ(); break;

                case OpCode.DSB: DSB(); break;
                case OpCode.DB: DB(); break;
                case OpCode.DC: DC(); break;
                case OpCode.DCO: DCO(); break;
                case OpCode.PTR: PTR(); break;
                
                case OpCode.CALL: CALL(); break;                
                case OpCode.INV: INV(); break;
                case OpCode.RUN: RUN(); break;

                case OpCode.LOCK: LOCK(); break;
                case OpCode.FREE: FREE(); break;

            }

        }

        #endregion

        #region Public Methods

        public Interpreter(Function function, List<object> parameters)
        {

            if (function.ParameterCount != parameters.Count)
                throw new ExecutionException("Die Funktion '" + function.Name + "' wurde mit " + parameters.Count + " statt erwartet " + function.ParameterCount + " Parametern aufgerufen.");

            this.function = function;

            script = function.Executable.Script;

            executable = script.Executable;

            functionStack = new Stack<FunctionFrame>();

            parameterStack = new Stack<object>();

            locks = new Dictionary<object, Instruction>();

            jobs = new List<Interpreter>();

            host = null;

            interrupt = false;

            Reset();

            foreach (object parameter in parameters)
            {

                if (parameter == null)
                    parameterStack.Push(NullReference.Instance);

                else
                {

                    Type parameterType = parameter.GetType();

                    if (parameterType == typeof(NullReference))
                        parameterStack.Push(NullReference.Instance);

                    else if (parameterType == typeof(int)
                        || parameterType == typeof(float)
                        || parameterType == typeof(double)
                        || parameterType == typeof(bool)
                        || parameterType == typeof(string)
                        || parameterType == typeof(char)
                        || parameterType == typeof(ArrayList))
                        parameterStack.Push(parameter);

                    else
                        throw new ExecutionException("Der Typ '" + parameterType.Name + "' ist kein generischer Typ.");

                }

            }

        }

        public Interpreter(Function function) : 
            this(function, new List<object>())
        {
        }

        public Interpreter(Script script, List<object> parameters) : 
            this(script.Executable.MainFunction, parameters)
        {
        }

        public Interpreter(Script script) : 
            this(script.Executable.MainFunction, new List<object>())
        {
        }

        public void Reset()
        {

            functionStack.Clear();

            FunctionFrame functionFrame = new FunctionFrame();

            functionFrame.function = function;

            functionFrame.localMemory = Memory.AllocateLocalMemory(executable.ScriptMemory);

            functionFrame.nextInstruction = (int) function.EntryPoint.Address;

            functionStack.Push(functionFrame);

            parameterStack.Clear();

            instruction = null;

            localMemory = functionFrame.localMemory;

            foreach (object currentLock in locks.Keys)
                script.Manager.Locks.Remove(currentLock);

            locks.Clear();

            finished = false;

            interrupted = false;

        }

        public uint Interpret(uint instructions)
        {

            localMemory.ExposeTemporaryVariables();

            interrupted = false;

            uint executed = 0;

            while (!Finished && !interrupted && executed < instructions)
            {

                ExecuteInstruction();

                ++executed;

                executed += ExecuteBackgroundJobs();

            }

            localMemory.HideTemporaryVariables();

            return executed;

        }

        public uint Interpret(TimeSpan interval)
        {

            DateTime end = DateTime.Now + interval;

            localMemory.ExposeTemporaryVariables();

            interrupted = false;

            uint executed = 0;

            while (!Finished && !interrupted)
            {

                ExecuteInstruction();

                ++executed;

                executed += ExecuteBackgroundJobs();

                if (DateTime.Now >= end)
                    break;

            }

            localMemory.HideTemporaryVariables();

            return executed;

        }

        public uint Interpret()
        {

            localMemory.ExposeTemporaryVariables();

            interrupted = false;

            uint executed = 0;

            while (!Finished && !interrupted)
            {

                ExecuteInstruction();

                ++executed;

                executed += ExecuteBackgroundJobs();

            }

            localMemory.HideTemporaryVariables();

            return executed;

        }

        #endregion

        #region Public Properties

        public Script Script
        {
            get { return script; }
        }

        public bool Interrupt
        {
            get { return interrupt; }
            set { interrupt = value; }
        }

        public ReadOnlyCollection<Interpreter> Jobs
        {
            get { return jobs.AsReadOnly(); }
        }

        public bool Interrupted
        {
            get { return interrupted; }
        }

        public bool Finished
        {
            get { return finished; }
        }

        public int NextInstruction
        {        
            get
            {
                if (functionStack.Count == 0) return -1;
                return functionStack.Peek().nextInstruction;
            }
        }

        public ReadOnlyCollection<Function> FunctionStack
        {
            get
            {
                List<Function> listFunctions = new List<Function>();
                foreach (FunctionFrame functionFrame in functionStack)
                    listFunctions.Add(functionFrame.function);
                return new List<Function>(listFunctions).AsReadOnly();
            }
        }

        public ReadOnlyCollection<object> ParameterStack
        {
            get { return new List<object>(parameterStack).AsReadOnly(); }
        }

        public Memory LocalMemory
        {
            get { return localMemory; }
        }

        public Host Handler
        {
            get { return host; }
            set { host = value; }
        }

        #endregion

    }

}

