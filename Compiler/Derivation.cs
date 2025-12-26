using System;
using System.Collections.Generic;
using System.Text;

using ScriptStack.Runtime;

namespace ScriptStack.Compiler
{

    internal class Derivation
    {

        #region Private Static Variables

        private static Dictionary<TokenType, Dictionary<string, Type>> derivates;

        #endregion

        #region Private Methods

        private string Derivate(Type destination, Type source)
        {
            return destination + "-" + source;
        }

        private string Translate(Type type)
        {

            if (type == null)
                return "void";
            if (type == typeof(NullReference))
                return "null";
            else if (type == typeof(int))
                return "int";
            else if (type == typeof(float))
                return "float";
            else if (type == typeof(double))
                return "double";
            else if (type == typeof(bool))
                return "boolean";
            else if (type == typeof(string))
                return "string";
            else if (type == typeof(ArrayList))
                return "array";
            else if (type == typeof(char))
                return "char";
            else
                throw new ParserException("Type '" + type.Name + "' not supported by type inference system.");
        }

        #endregion;

        #region Public Methods

        public Derivation()
        {

            if (derivates != null)
                return;

            derivates = new Dictionary<TokenType, Dictionary<String, Type>>();

            Type typeNull = typeof(NullReference);
            Type typeInt = typeof(int);
            Type typeFloat = typeof(float);
            Type typeDouble = typeof(double);
            Type typeBoolean = typeof(bool);
            Type typeString = typeof(string);
            Type typeArray = typeof(ArrayList);
            Type typeChar = typeof(char);
            
            Dictionary<string, Type> Logic = new Dictionary<string, Type>();

            Logic[Derivate(null, null)] = typeBoolean;
            Logic[Derivate(typeNull, typeNull)] = typeBoolean;
            Logic[Derivate(null, typeBoolean)] = typeBoolean;
            Logic[Derivate(typeBoolean, null)] = typeBoolean;
            Logic[Derivate(typeBoolean, typeBoolean)] = typeBoolean;

            derivates[TokenType.And] = Logic;
            derivates[TokenType.Or] = Logic;
            
            Dictionary<string, Type> Comparision = new Dictionary<string, Type>();

            Comparision[Derivate(null, null)] = typeBoolean;
            Comparision[Derivate(null, typeNull)] = typeBoolean;
            Comparision[Derivate(null, typeInt)] = typeBoolean;
            Comparision[Derivate(null, typeFloat)] = typeBoolean;
            Comparision[Derivate(null, typeDouble)] = typeBoolean;
            Comparision[Derivate(null, typeBoolean)] = typeBoolean;
            Comparision[Derivate(null, typeString)] = typeBoolean;
            Comparision[Derivate(null, typeChar)] = typeBoolean;
            
            Comparision[Derivate(typeNull, null)] = typeBoolean;
            Comparision[Derivate(typeNull, typeNull)] = typeBoolean;

            Comparision[Derivate(typeInt, null)] = typeBoolean;
            Comparision[Derivate(typeInt, typeInt)] = typeBoolean;
            Comparision[Derivate(typeInt, typeFloat)] = typeBoolean;
            Comparision[Derivate(typeInt, typeDouble)] = typeBoolean;

            Comparision[Derivate(typeFloat, null)] = typeBoolean;
            Comparision[Derivate(typeFloat, typeInt)] = typeBoolean;
            Comparision[Derivate(typeFloat, typeFloat)] = typeBoolean;
            Comparision[Derivate(typeFloat, typeDouble)] = typeBoolean;

            Comparision[Derivate(typeChar, null)] = typeBoolean;
            Comparision[Derivate(typeChar, typeInt)] = typeBoolean;
            Comparision[Derivate(typeChar, typeFloat)] = typeBoolean;
            Comparision[Derivate(typeChar, typeString)] = typeBoolean;
            Comparision[Derivate(typeChar, typeDouble)] = typeBoolean;

            Comparision[Derivate(typeBoolean, null)] = typeBoolean;
            Comparision[Derivate(typeBoolean, typeBoolean)] = typeBoolean;

            Comparision[Derivate(typeString, null)] = typeBoolean;
            Comparision[Derivate(typeString, typeString)] = typeBoolean;
            Comparision[Derivate(typeString, typeChar)] = typeBoolean;
                        
            derivates[TokenType.Equal] = Comparision;
            derivates[TokenType.NotEqual] = Comparision;
            
            Dictionary<string, Type> Relation = new Dictionary<string, Type>();

            Relation[Derivate(null, null)] = typeBoolean;
            Relation[Derivate(typeBoolean, typeBoolean)] = typeBoolean;
            Relation[Derivate(null, typeInt)] = typeBoolean;
            Relation[Derivate(null, typeFloat)] = typeBoolean;
            Relation[Derivate(null, typeDouble)] = typeBoolean;
            Relation[Derivate(null, typeString)] = typeBoolean;
            Relation[Derivate(null, typeChar)] = typeBoolean;

            Relation[Derivate(typeInt, null)] = typeBoolean;
            Relation[Derivate(typeInt, typeInt)] = typeBoolean;
            Relation[Derivate(typeInt, typeFloat)] = typeBoolean;
            Relation[Derivate(typeInt, typeDouble)] = typeBoolean;
            Relation[Derivate(typeInt, typeChar)] = typeBoolean;
            
            Relation[Derivate(typeFloat, null)] = typeBoolean;
            Relation[Derivate(typeFloat, typeInt)] = typeBoolean;
            Relation[Derivate(typeFloat, typeFloat)] = typeBoolean;
            Relation[Derivate(typeFloat, typeChar)] = typeBoolean;
            Relation[Derivate(typeFloat, typeDouble)] = typeBoolean;

            Relation[Derivate(typeChar, null)] = typeBoolean;
            Relation[Derivate(typeChar, typeInt)] = typeBoolean;
            Relation[Derivate(typeChar, typeFloat)] = typeBoolean;
            Relation[Derivate(typeChar, typeChar)] = typeBoolean;
            Relation[Derivate(typeChar, typeString)] = typeBoolean;
            Relation[Derivate(typeChar, typeDouble)] = typeBoolean;

            Relation[Derivate(typeString, null)] = typeBoolean;
            Relation[Derivate(typeString, typeString)] = typeBoolean;
            Relation[Derivate(typeString, typeChar)] = typeBoolean;
            
            derivates[TokenType.Greater] = Relation;
            derivates[TokenType.GreaterEqual] = Relation;
            derivates[TokenType.Less] = Relation;
            derivates[TokenType.LessEqual] = Relation;
            
            Dictionary<string, Type> Plus = new Dictionary<string, Type>();

            Plus[Derivate(null, null)] = null;
            Plus[Derivate(null, typeInt)] = null;
            Plus[Derivate(null, typeFloat)] = null;
            Plus[Derivate(null, typeDouble)] = null;
            Plus[Derivate(null, typeBoolean)] = null;
            Plus[Derivate(null, typeString)] = null;
            Plus[Derivate(null, typeChar)] = null;
            Plus[Derivate(null, typeArray)] = typeArray;

            Plus[Derivate(typeInt, null)] = null;
            Plus[Derivate(typeInt, typeInt)] = typeInt;
            Plus[Derivate(typeInt, typeFloat)] = typeFloat;
            Plus[Derivate(typeInt, typeChar)] = typeChar;
            Plus[Derivate(typeInt, typeDouble)] = typeDouble;

            Plus[Derivate(typeFloat, null)] = null;
            Plus[Derivate(typeFloat, typeInt)] = typeFloat;
            Plus[Derivate(typeFloat, typeFloat)] = typeFloat;
            Plus[Derivate(typeFloat, typeChar)] = typeFloat;
            Plus[Derivate(typeFloat, typeDouble)] = typeDouble;

            Plus[Derivate(typeString, null)] = typeString;
            Plus[Derivate(typeString, typeInt)] = typeString;
            Plus[Derivate(typeString, typeFloat)] = typeString;
            Plus[Derivate(typeString, typeBoolean)] = typeString;
            Plus[Derivate(typeString, typeString)] = typeString;
            Plus[Derivate(typeString, typeChar)] = typeString;
            Plus[Derivate(typeString, typeArray)] = typeArray;
            Plus[Derivate(typeString, typeDouble)] = typeString;

            Plus[Derivate(typeArray, null)] = typeArray;
            Plus[Derivate(typeArray, typeInt)] = typeArray;
            Plus[Derivate(typeArray, typeFloat)] = typeArray;
            Plus[Derivate(typeArray, typeBoolean)] = typeArray;
            Plus[Derivate(typeArray, typeString)] = typeArray;
            Plus[Derivate(typeArray, typeChar)] = typeArray;
            Plus[Derivate(typeArray, typeArray)] = typeArray;
            Plus[Derivate(typeArray, typeDouble)] = typeArray;

            Plus[Derivate(typeChar, null)] = typeChar;
            Plus[Derivate(typeChar, typeInt)] = typeChar;
            Plus[Derivate(typeChar, typeFloat)] = typeChar;
            Plus[Derivate(typeChar, typeChar)] = typeChar;
            Plus[Derivate(typeChar, typeDouble)] = typeDouble;

            derivates[TokenType.Plus] = Plus;

            Dictionary<string, Type> Minus = new Dictionary<string, Type>();

            Minus[Derivate(null, null)] = null;
            Minus[Derivate(null, typeInt)] = null;
            Minus[Derivate(null, typeFloat)] = null;
            Minus[Derivate(null, typeBoolean)] = null;
            Minus[Derivate(null, typeString)] = null;
            Minus[Derivate(null, typeChar)] = null;
            Minus[Derivate(null, typeArray)] = null;

            Minus[Derivate(typeInt, null)] = null;
            Minus[Derivate(typeInt, typeInt)] = typeInt;
            Minus[Derivate(typeInt, typeFloat)] = typeFloat;
            Minus[Derivate(typeInt, typeChar)] = typeChar;

            Minus[Derivate(typeFloat, null)] = null;
            Minus[Derivate(typeFloat, typeInt)] = typeFloat;
            Minus[Derivate(typeFloat, typeFloat)] = typeFloat;
            Minus[Derivate(typeFloat, typeChar)] = typeFloat;
            
            Minus[Derivate(typeString, null)] = typeString;
            Minus[Derivate(typeString, typeString)] = typeString;
            Minus[Derivate(typeString, typeChar)] = typeString;

            Minus[Derivate(typeArray, null)] = typeArray;
            Minus[Derivate(typeArray, typeInt)] = typeArray;
            Minus[Derivate(typeArray, typeFloat)] = typeArray;
            Minus[Derivate(typeArray, typeBoolean)] = typeArray;
            Minus[Derivate(typeArray, typeString)] = typeArray;
            Minus[Derivate(typeArray, typeChar)] = typeArray;
            Minus[Derivate(typeArray, typeArray)] = typeArray;

            Minus[Derivate(typeChar, null)] = typeChar;
            Minus[Derivate(typeChar, typeInt)] = typeChar;
            Minus[Derivate(typeChar, typeFloat)] = typeChar;
            Minus[Derivate(typeChar, typeChar)] = typeChar;

            derivates[TokenType.Minus] = Minus;

            Dictionary<string, Type> Factor = new Dictionary<string, Type>();

            Factor[Derivate(null, null)] = null;
            Factor[Derivate(null, typeInt)] = null;
            Factor[Derivate(null, typeFloat)] = typeFloat;
            Factor[Derivate(null, typeChar)] = typeChar;

            Factor[Derivate(typeInt, null)] = null;
            Factor[Derivate(typeInt, typeInt)] = typeInt;
            Factor[Derivate(typeInt, typeFloat)] = typeFloat;
            Factor[Derivate(typeInt, typeChar)] = typeChar;
            
            Factor[Derivate(typeFloat, null)] = typeFloat;
            Factor[Derivate(typeFloat, typeInt)] = typeFloat;
            Factor[Derivate(typeFloat, typeFloat)] = typeFloat;
            Factor[Derivate(typeFloat, typeChar)] = typeFloat;

            Factor[Derivate(typeChar, null)] = typeFloat;
            Factor[Derivate(typeChar, typeInt)] = typeFloat;
            Factor[Derivate(typeChar, typeFloat)] = typeFloat;
            Factor[Derivate(typeChar, typeChar)] = typeChar;

            derivates[TokenType.Multiply] = Factor;
            derivates[TokenType.Divide] = Factor;
            derivates[TokenType.Modulo] = Factor;

        }

        /// <summary>
        /// Get the derivated type
        /// </summary>
        /// <param name="token"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public Type Derivate(Token token, Type destination, Type source)
        {

            if (!derivates.ContainsKey(token.Type))
                throw new ParserException("Token '" + token + "' ist kein binary operator.");
                                                                                                 
            Dictionary<string, Type> derivate = derivates[token.Type];

            string key = Derivate(destination, source);

            if (!derivate.ContainsKey(key))
            {

                string dst = Translate(destination);
                string src = Translate(source);

                throw new ParserException("Die Operation '" + token.Lexeme + "' kann '" + Translate(destination) + "' und '" + Translate(source) + "' nicht ableiten.");

            }

            return derivate[key];

        }

        #endregion

    }
}
