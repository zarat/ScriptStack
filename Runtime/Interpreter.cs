using ScriptStack.Compiler;
using ScriptStack.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
// CLR Bridge
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

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

        private int ToInt32Bitwise(object value, string opName = "bitwise")
        {
            if (value == null) throw new ExecutionException($"Cannot apply {opName} operation to null.");
            if (value is NullReference) throw new ExecutionException($"Cannot apply {opName} operation to 'null'.");

            Type t = value.GetType();

            try
            {
                switch (Type.GetTypeCode(t))
                {
                    case TypeCode.Int32: return (int)value;
                    case TypeCode.Boolean: return ((bool)value) ? 1 : 0;
                    case TypeCode.Char: return (char)value;
                    case TypeCode.SByte: return (sbyte)value;
                    case TypeCode.Byte: return (byte)value;
                    case TypeCode.Int16: return (short)value;
                    case TypeCode.UInt16: return (ushort)value;
                    case TypeCode.UInt32: { uint u = (uint)value; if (u > int.MaxValue) throw new OverflowException(); return (int)u; }
                    case TypeCode.Int64: { long l = (long)value; if (l < int.MinValue || l > int.MaxValue) throw new OverflowException(); return (int)l; }
                    case TypeCode.UInt64: { ulong ul = (ulong)value; if (ul > (ulong)int.MaxValue) throw new OverflowException(); return (int)ul; }
                    case TypeCode.Single: { float f = (float)value; if (float.IsNaN(f) || float.IsInfinity(f) || f < int.MinValue || f > int.MaxValue) throw new OverflowException(); return (int)f; }
                    case TypeCode.Double: { double d = (double)value; if (double.IsNaN(d) || double.IsInfinity(d) || d < int.MinValue || d > int.MaxValue) throw new OverflowException(); return (int)d; }
                    case TypeCode.Decimal: return decimal.ToInt32((decimal)value);
                    default: throw new InvalidCastException();
                }
            }
            catch
            {
                throw new ExecutionException($"Values of type '{t.Name}' cannot be used in {opName} operations.");
            }
        }


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

                    else
                    {
                        src = localMemory[(string)operand.Value];
                        var memberName = operand.Member?.ToString() ?? "";
                        return GetClrMemberValue(src, memberName);
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

                    else if (src is System.Collections.IList list)
                    {
                        object objectIndex = localMemory[operand.Pointer];

                        if (objectIndex.GetType() != typeof(int))
                            throw new ExecutionException("Ein CLR Array ist nur numerisch indexierbar.");

                        return list[(int)objectIndex] ?? NullReference.Instance;
                    }

                    else
                    {
                        // IDictionary / Indexer-Property
                        object objectIndex = localMemory[operand.Pointer];
                        if (TryGetClrIndexedValue(src, objectIndex, out var v))
                            return v;
                        throw new ExecutionException("Nur Arrays, Dictionaries und Strings sind indexierbar.");
                    }


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
                    {
                        object target = localMemory.Exists(identifier) ? localMemory[identifier] : NullReference.Instance;

                        if (target is ArrayList arr)
                        {
                            arr[dst.Member] = val;
                            break;
                        }

                        // NEU: C# Objekt Member setzen
                        SetClrMemberValue(target, dst.Member.ToString()!, val);
                        break;
                    }
                case OperandType.Pointer:
                    {
                        object container = localMemory.Exists(identifier) ? localMemory[identifier] : NullReference.Instance;
                        object key = localMemory[dst.Pointer];

                        // ScriptStack Array
                        if (container is ArrayList arr)
                        {
                            arr[key] = val;
                            break;
                        }

                        // Strings are read-only in this language
                        if (container is string)
                            throw new ExecutionException("Ein String ist nicht schreibbar.");

                        // CLR arrays / lists / dictionaries / indexers
                        if (TrySetClrIndexedValue(container, key, val))
                            break;

                        throw new ExecutionException($"Das Ziel '{dst}' vom Typ '{container?.GetType().FullName}' ist nicht indexierbar.");
                    }

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

            // NEW: CLR/object support while preserving ScriptStack numeric semantics
            bool numericDest =
                typeDest == typeof(int) ||
                typeDest == typeof(float) ||
                typeDest == typeof(double) ||
                typeDest == typeof(char);

            bool numericSource =
                typeSource == typeof(int) ||
                typeSource == typeof(float) ||
                typeSource == typeof(double) ||
                typeSource == typeof(char);

            // Equality/inequality for arbitrary CLR objects (and ScriptStack objects),
            // BUT keep the old numeric cross-type behaviour (int == float etc.)
            if ((instruction.OpCode == OpCode.CEQ || instruction.OpCode == OpCode.CNE) && !(numericDest && numericSource))
            {
                bool eq = Equals(ScriptNullToClr(dst), ScriptNullToClr(src));
                result = (instruction.OpCode == OpCode.CEQ) ? eq : !eq;
                Assignment(instruction.First, result);
                return;
            }

            // Ordering comparisons for arbitrary CLR objects via IComparable,
            // again only when we are NOT in the numeric fast-path.
            if (!(numericDest && numericSource) &&
                (instruction.OpCode == OpCode.CG || instruction.OpCode == OpCode.CGE || instruction.OpCode == OpCode.CL || instruction.OpCode == OpCode.CLE) &&
                dst is IComparable cmpDst)
            {
                int cmp;
                try
                {
                    cmp = cmpDst.CompareTo(ScriptNullToClr(src));
                }
                catch
                {
                    // last resort: string compare
                    cmp = string.Compare(dst.ToString(), src.ToString(), StringComparison.Ordinal);
                }

                switch (instruction.OpCode)
                {
                    case OpCode.CG:  result = cmp > 0; break;
                    case OpCode.CGE: result = cmp >= 0; break;
                    case OpCode.CL:  result = cmp < 0; break;
                    case OpCode.CLE: result = cmp <= 0; break;
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

                if (Equals(tmp, iterator))
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

        private void Iterator(IDictionary dict)
        {

            if (dict.Count == 0)
                return;

            object iterator = Evaluate(instruction.First);

            bool found = false;

            object next = null;

            foreach (object tmp in dict.Keys)
            {

                if (found)
                {
                    next = tmp;
                    break;
                }

                if (Equals(tmp, iterator))
                    found = true;

            }

            if (!found)
            {
                IDictionaryEnumerator en = dict.GetEnumerator();
                if (en.MoveNext())
                    next = en.Key;
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

            // Delegate to the unified Assignment() so Member/Pointer also work for CLR objects.
            Assignment(operand, tmp);

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
                    //int res = (int)val | (int)localMemory[(string)operand.Value];
                    int res = ToInt32Bitwise(localMemory[(string)operand.Value], "|") | ToInt32Bitwise(val, "|");

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
                    //int res = (int)localMemory[(string)operand.Value] & (int)val;
                    int res = ToInt32Bitwise(localMemory[(string)operand.Value], "&") & ToInt32Bitwise(val, "&");

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
                    //int res = (int)val ^ (int)localMemory[(string)operand.Value];
                    int res = ToInt32Bitwise(localMemory[(string)operand.Value], "^") ^ ToInt32Bitwise(val, "^");

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

            if ((bool)Evaluate(instruction.First))
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
 
            if (!(bool)Evaluate(instruction.First))
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

            string enumerableVar = instruction.Second.Value.ToString();
            object enumerable = localMemory[enumerableVar];

            if (enumerable is ArrayList a)
            {
                Iterator(a);
                return;
            }

            if (enumerable is string s)
            {
                Iterator(s);
                return;
            }

            if (enumerable is IList list)
            {
                Iterator(list);
                return;
            }


            if (enumerable is IDictionary dict)
            {
                Iterator(dict);
                return;
            }

            // Any other IEnumerable (e.g. HashSet, IEnumerable<T>, LINQ results)
            if (enumerable is IEnumerable en)
            {
                var materialized = new ArrayList();
                foreach (var item in en)
                    materialized.Add(item ?? NullReference.Instance);

                // Replace variable with materialized ArrayList so the existing foreach bytecode
                // (which indexes via array[key]) keeps working.
                localMemory[enumerableVar] = materialized;
                Iterator(materialized);
                return;
            }

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
        /// Invoke a CLR instance method via reflection.
        ///
        /// The compiler lowers <code>obj.Method(a, b)</code> into:
        /// PUSH a; PUSH b; MIV obj.Method, <argc>; POP result
        /// </summary>
        private void MIV()
        {

            if (instruction.First.Type != OperandType.Member)
                throw new ExecutionException("Error in MIV.");

            string targetIdentifier = instruction.First.Value.ToString();
            string methodName = instruction.First.Member?.ToString() ?? "";

            int argc = 0;
            if (instruction.Second != null)
            {
                object raw = instruction.Second.Value;
                if (raw is int i) argc = i;
                else if (raw is string s && int.TryParse(s, out var j)) argc = j;
                else throw new ExecutionException("Error in MIV.");
            }

            object target = localMemory[targetIdentifier];
            if (target == null || target is NullReference)
            {
                parameterStack.Push(NullReference.Instance);
                return;
            }

            // Collect args (preserve order)
            List<object> args = new List<object>(argc);
            for (int i = 0; i < argc; i++)
                args.Insert(0, parameterStack.Pop());

            object result = InvokeClrMethod(target, methodName, args);
            if (result == null)
                result = NullReference.Instance;

            parameterStack.Push(result);

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
                case OpCode.MIV: MIV(); break;
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

        #region CLR Bridge

        private static readonly BindingFlags ClrMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

        private static object ScriptNullToClr(object v) => v is NullReference ? null : v;

        private static object CoerceTo(object value, Type targetType)
        {
            value = ScriptNullToClr(value);

            // Handle Nullable<T>
            var underlyingNullable = Nullable.GetUnderlyingType(targetType);
            if (underlyingNullable != null)
                targetType = underlyingNullable;

            if (value == null)
            {
                // null für ValueTypes nicht erlaubt -> Default
                return targetType.IsValueType ? Activator.CreateInstance(targetType)! : null!;
            }

            var srcType = value.GetType();
            if (targetType.IsAssignableFrom(srcType))
                return value;

            // --- ScriptStack ArrayList -> CLR types ---
            if (value is ArrayList scriptArr)
            {
                // CLR Array (e.g. int[])
                if (targetType.IsArray)
                {
                    var elemType = targetType.GetElementType()!;
                    return ConvertScriptArrayListToClrArray(scriptArr, elemType);
                }

                // Generic collections (List<T>, ICollection<T>, IEnumerable<T>, etc.)
                if (TryConvertScriptArrayListToGenericCollection(scriptArr, targetType, out var coll))
                    return coll;

                // Generic dictionaries (Dictionary<TKey,TValue>, IDictionary<TKey,TValue>)
                if (TryConvertScriptArrayListToGenericDictionary(scriptArr, targetType, out var dict))
                    return dict;
            }

            // Enum conversions
            if (targetType.IsEnum)
                return Enum.Parse(targetType, value.ToString()!, ignoreCase: true);

            // numeric / string conversions etc.
            if (value is IConvertible)
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);

            // last resort: keep as-is (Reflection may still accept via implicit operators)
            return value;
        }

        private static object ConvertScriptArrayListToClrArray(ArrayList scriptArr, Type elementType)
        {
            // We only support list-like arrays here: integer keys, 0..n-1 (no gaps).
            // Anything associative should be mapped to dictionaries instead.
            if (scriptArr.Count == 0)
                return Array.CreateInstance(elementType, 0);

            // ensure all keys are ints
            var keys = new List<int>(scriptArr.Count);
            foreach (var k in scriptArr.Keys)
            {
                if (k is int i)
                    keys.Add(i);
                else
                    throw new ExecutionException($"Associatives Array kann nicht zu '{elementType.Name}[]' konvertiert werden (Key-Typ: {k?.GetType().Name ?? "null"}).");
            }

            keys.Sort();
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != i)
                    throw new ExecutionException($"Array kann nicht zu '{elementType.Name}[]' konvertiert werden: Keys müssen 0..n-1 ohne Lücken sein.");
            }

            var arr = Array.CreateInstance(elementType, keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                var v = scriptArr[i]; // uses our indexer: returns NullReference.Instance if missing
                var coerced = CoerceTo(v, elementType);
                arr.SetValue(coerced, i);
            }
            return arr;
        }

        private static Type GetGenericInterface(Type type, Type genericDefinition)
        {
            if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition)
                return type;

            return type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericDefinition);
        }

        private static bool TryConvertScriptArrayListToGenericCollection(ArrayList scriptArr, Type targetType, out object collection)
        {
            collection = null;

            var iface = GetGenericInterface(targetType, typeof(ICollection<>))
                     ?? GetGenericInterface(targetType, typeof(IList<>))
                     ?? GetGenericInterface(targetType, typeof(IEnumerable<>))
                     ?? GetGenericInterface(targetType, typeof(IReadOnlyCollection<>))
                     ?? GetGenericInterface(targetType, typeof(IReadOnlyList<>));

            if (iface == null)
                return false;

            var elemType = iface.GetGenericArguments()[0];

            // Choose concrete type
            Type concrete = targetType;
            if (targetType.IsInterface || targetType.IsAbstract)
                concrete = typeof(List<>).MakeGenericType(elemType);

            object instance;
            try
            {
                instance = Activator.CreateInstance(concrete)!;
            }
            catch
            {
                // If the target type is non-instantiable, fallback to List<T>
                instance = Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType))!;
                concrete = instance.GetType();
            }

            // Find Add(T)
            var add = concrete.GetMethod("Add", new[] { elemType })
                   ?? concrete.GetMethods().FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 1);

            if (add == null)
                return false;

            // Add items in key order (int keys). If there are non-int keys, refuse.
            var intKeys = new List<int>();
            foreach (var k in scriptArr.Keys)
            {
                if (k is int i) intKeys.Add(i);
                else return false;
            }
            intKeys.Sort();

            foreach (var k in intKeys)
            {
                var v = scriptArr[k];
                var coerced = CoerceTo(v, elemType);
                add.Invoke(instance, new[] { coerced });
            }

            collection = instance;
            return true;
        }

        private static bool TryConvertScriptArrayListToGenericDictionary(ArrayList scriptArr, Type targetType, out object dict)
        {
            dict = null;

            var iface = GetGenericInterface(targetType, typeof(IDictionary<,>));
            if (iface == null)
                return false;

            var ga = iface.GetGenericArguments();
            var keyType = ga[0];
            var valType = ga[1];

            Type concrete = targetType;
            if (targetType.IsInterface || targetType.IsAbstract)
                concrete = typeof(Dictionary<,>).MakeGenericType(keyType, valType);

            object instance;
            try
            {
                instance = Activator.CreateInstance(concrete)!;
            }
            catch
            {
                instance = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valType))!;
                concrete = instance.GetType();
            }

            var add = concrete.GetMethod("Add", new[] { keyType, valType })
                   ?? concrete.GetMethods().FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 2);

            if (add == null)
                return false;

            foreach (var k in scriptArr.Keys)
            {
                var v = scriptArr[k];
                var ck = CoerceTo(k, keyType);
                var cv = CoerceTo(v, valType);
                add.Invoke(instance, new[] { ck, cv });
            }

            dict = instance;
            return true;
        }


        private static object GetClrMemberValue(object target, string memberName)
        {
            if (target == null || target is NullReference)
                return NullReference.Instance;

            var t = target.GetType();

            // 1) Field
            var field = t.GetField(memberName, ClrMemberFlags);
            if (field != null)
                return field.GetValue(target) ?? NullReference.Instance;

            // 2) Property
            var prop = t.GetProperty(memberName, ClrMemberFlags);
            if (prop != null && prop.CanRead)
                return prop.GetValue(target) ?? NullReference.Instance;

            // 3) Fallback: parameterlose Methode (dein bisheriges Verhalten)
            var m = t.GetMethod(memberName, ClrMemberFlags, binder: null, Type.EmptyTypes, modifiers: null);
            if (m != null)
                return m.Invoke(target, Array.Empty<object>()) ?? NullReference.Instance;

            return NullReference.Instance;
        }

        private static void SetClrMemberValue(object target, string memberName, object scriptValue)
        {
            if (target == null || target is NullReference)
                throw new ExecutionException($"Null reference bei Zuweisung auf Member '{memberName}'.");

            var t = target.GetType();

            var field = t.GetField(memberName, ClrMemberFlags);
            if (field != null)
            {
                var coerced = CoerceTo(scriptValue, field.FieldType);
                field.SetValue(target, coerced);
                return;
            }

            var prop = t.GetProperty(memberName, ClrMemberFlags);
            if (prop != null && prop.CanWrite)
            {
                var coerced = CoerceTo(scriptValue, prop.PropertyType);
                prop.SetValue(target, coerced);
                return;
            }

            throw new ExecutionException($"Member '{memberName}' nicht gefunden oder nicht schreibbar auf Typ '{t.FullName}'.");
        }

        private static bool TryCoerceTo(object value, Type targetType, out object coerced)
        {
            try
            {
                coerced = CoerceTo(value, targetType);
                return true;
            }
            catch
            {
                coerced = null;
                return false;
            }
        }

        private static object InvokeClrMethod(object target, string methodName, List<object> args)
        {
            if (target == null || target is NullReference)
                return NullReference.Instance;

            var t = target.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

            var methods = t.GetMethods(flags);

            MethodInfo best = null;
            object[] bestArgs = null;
            int bestScore = int.MaxValue;

            foreach (var m in methods)
            {
                if (!string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var ps = m.GetParameters();
                if (ps.Length != args.Count)
                    continue;

                int score = 0;
                var coercedArgs = new object[ps.Length];
                bool ok = true;

                for (int i = 0; i < ps.Length; i++)
                {
                    var pType = ps[i].ParameterType;
                    var a = ScriptNullToClr(args[i]);

                    if (a == null)
                    {
                        // null passt auf RefTypes/Nullable
                        if (pType.IsValueType && Nullable.GetUnderlyingType(pType) == null)
                        {
                            ok = false;
                            break;
                        }

                        coercedArgs[i] = null;
                        score += 1;
                        continue;
                    }

                    var aType = a.GetType();

                    if (pType.IsAssignableFrom(aType))
                    {
                        coercedArgs[i] = a;
                        score += (pType == aType) ? 0 : 1;
                        continue;
                    }

                    if (TryCoerceTo(a, pType, out var c))
                    {
                        coercedArgs[i] = c;
                        score += 2;
                        continue;
                    }

                    ok = false;
                    break;
                }

                if (!ok)
                    continue;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = m;
                    bestArgs = coercedArgs;
                }
            }

            if (best == null)
            {
                throw new ExecutionException($"Methode '{methodName}' mit {args.Count} Parametern nicht gefunden auf Typ '{t.FullName}'.");
            }

            try
            {
                object result = best.Invoke(target, bestArgs);
                if (best.ReturnType == typeof(void))
                    return NullReference.Instance;

                return result ?? NullReference.Instance;
            }
            catch (TargetInvocationException tie)
            {
                // unwrap inner exception for better diagnostics
                throw new ExecutionException($"Fehler in CLR-Methode '{t.FullName}.{best.Name}': {tie.InnerException?.Message ?? tie.Message}");
            }
            catch (Exception ex)
            {
                throw new ExecutionException($"Fehler beim Aufruf von CLR-Methode '{t.FullName}.{best.Name}': {ex.Message}");
            }
        }

        private static bool TryGetClrIndexedValue(object target, object scriptKey, out object value)
        {
            value = NullReference.Instance;
            if (target == null || target is NullReference)
                return true;

            // IList / arrays
            if (target is IList list)
            {
                var idxObj = ScriptNullToClr(scriptKey);
                if (idxObj is int idx)
                {
                    value = list[idx] ?? NullReference.Instance;
                    return true;
                }
                return false;
            }

            // IDictionary
            if (target is IDictionary dict)
            {
                object key = ScriptNullToClr(scriptKey);

                // Try to coerce key to generic TKey if possible (avoids InvalidCastException)
                var keyType = target.GetType().GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    ?.GetGenericArguments()[0];

                if (keyType != null && TryCoerceTo(key, keyType, out var ck))
                    key = ck;

                try
                {
                    var v = dict[key];
                    value = v ?? NullReference.Instance;
                    return true;
                }
                catch
                {
                    // fall through to indexer reflection
                }
            }

            // indexer property (Item[...])
            var t = target.GetType();
            foreach (var p in t.GetProperties(ClrMemberFlags))
            {
                var ip = p.GetIndexParameters();
                if (ip.Length != 1 || !p.CanRead)
                    continue;

                if (!TryCoerceTo(scriptKey, ip[0].ParameterType, out var ck))
                    continue;

                try
                {
                    var v = p.GetValue(target, new object[] { ck });
                    value = v ?? NullReference.Instance;
                    return true;
                }
                catch
                {
                    // try next
                }
            }

            return false;
        }

        private static bool TrySetClrIndexedValue(object target, object scriptKey, object scriptValue)
        {
            if (target == null || target is NullReference)
                return false;

            // IList / arrays
            if (target is IList list)
            {
                var idxObj = ScriptNullToClr(scriptKey);
                if (idxObj is not int idx)
                    return false;

                // try to coerce value to element type if we can infer it
                Type elemType = null;
                var tt = target.GetType();
                if (tt.IsArray)
                    elemType = tt.GetElementType();
                else
                {
                    var gi = tt.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
                    if (gi != null) elemType = gi.GetGenericArguments()[0];
                }

                object v = scriptValue;
                if (elemType != null && TryCoerceTo(scriptValue, elemType, out var cv))
                    v = cv;

                list[idx] = ScriptNullToClr(v);
                return true;
            }

            // IDictionary
            if (target is IDictionary dict)
            {
                object key = ScriptNullToClr(scriptKey);
                object val = ScriptNullToClr(scriptValue);

                var iface = target.GetType().GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
                if (iface != null)
                {
                    var ga = iface.GetGenericArguments();
                    var keyType = ga[0];
                    var valType = ga[1];
                    if (TryCoerceTo(key, keyType, out var ck)) key = ck;
                    if (TryCoerceTo(val, valType, out var cv)) val = cv;
                }

                try
                {
                    dict[key] = val;
                    return true;
                }
                catch
                {
                    // fall through to indexer reflection
                }
            }

            // indexer property (Item[...])
            var t = target.GetType();
            foreach (var p in t.GetProperties(ClrMemberFlags))
            {
                var ip = p.GetIndexParameters();
                if (ip.Length != 1 || !p.CanWrite)
                    continue;

                if (!TryCoerceTo(scriptKey, ip[0].ParameterType, out var ck))
                    continue;

                object v = scriptValue;
                if (TryCoerceTo(scriptValue, p.PropertyType, out var cv))
                    v = cv;

                try
                {
                    p.SetValue(target, ScriptNullToClr(v), new object[] { ck });
                    return true;
                }
                catch
                {
                    // try next
                }
            }

            return false;
        }

        private void Iterator(IList list)
        {
            if (list.Count == 0)
                return;

            object iterator = Evaluate(instruction.First);

            if (iterator.GetType() != typeof(int))
            {
                localMemory[instruction.First.Value.ToString()] = 0;
                return;
            }

            int i = (int)iterator;

            if (i < list.Count - 1)
                localMemory[instruction.First.Value.ToString()] = i + 1;
            else
                localMemory[instruction.First.Value.ToString()] = NullReference.Instance;
        }

        #endregion

    }

}

