using System;
using System.Collections.Generic;
using System.Text;

using ScriptStack.Compiler;
using ScriptStack.Runtime;

namespace ScriptStack.Runtime
{

    /// <summary>
    /// A Routine is an abstract representation of a method.
    /// 
    /// To successfully write a routine you have to use one of its several overloaded cunstuctors listed above and pass up to a maximum of 3 parameters.
    /// 
    /// ```
    /// 
    /// Routine myRoutine;
    /// 
    /// myRoutine = new Routine( 
    ///     typeof(int), // return value (optional)
    ///     "myFunction", // name of the function (required)
    ///     typeof(int), typeof(float), typeof(bool), // up to 3 parameters (optional)
    ///     "Describe your custom method" // a description (optional)
    /// );
    /// 
    /// ```
    /// 
    /// To declare more then 3 parameters you can add them to a list and add the list as parameter
    /// 
    /// ```
    /// 
    /// // create a generic list ..
    /// List<Type> customParameter = new List<Type>();
    /// 
    /// // .. add all parameter types
    /// customParameter.Add(typeof(int));
    /// customParameter.Add(typeof(float));
    /// customParameter.Add(typeof(bool));
    /// customParameter.Add(typeof(int));
    /// 
    /// // .. and add them as a parameter
    /// Routine myRoutine = new Routine( 
    ///     typeof(int), 
    ///     "myFunction", 
    ///     typeof(customParameter), // the list as parameter
    ///     "Describe your function" 
    /// );
    /// 
    /// ```
    /// 
    /// The Manager can invoke a Routine by using the Invoke method.
    /// 
    /// ```
    /// 
    /// Routine myRoutine = manager.Routines["print"];
    /// 
    /// List<object> parameters = new List<object>();
    /// parameters.Add("Hello world");
    /// 
    /// myRoutine.Handler.Invoke(myRoutine.Name, parameters);
    /// 
    /// ```
    /// 
    /// <seealso cref="Manager"/>  <seealso cref="Host"/>
    /// 
    /// </summary>
    /// \todo The Constructor has serveral (way too much) Overloaders
    public class Routine
    {

        #region Private Variables

        private string name;
        private List<Type> parameters;
        private Type result;
        private string description;
        private Host host;

        #endregion

        #region Private Methods

        private void Validate(Type type)
        {

            if (type == null || type == typeof(void))
                return;

            if (type != typeof(int)
                && type != typeof(float)
                && type != typeof(bool)
                && type != typeof(double)
                && type != typeof(string)
                && type != typeof(char)
                && type != typeof(ArrayList)
                )
                throw new ExecutionException("Der Typ '" + type.Name + "' ist kein generischer Datentyp und es wurden keine Erweiterungen registriert.");

        }

        private string ToString(Type type)
        {

            string tmp = "";

            if (type == null)
                tmp = "null";
            else if (type == typeof(void))
                tmp = "void";
            else if (type == typeof(int))
                tmp = "int";
            else if (type == typeof(float))
                tmp = "float";
            else if (type == typeof(double))
                tmp = "double";
            else if (type == typeof(bool))
                tmp = "bool";
            else if (type == typeof(char))
                tmp = "char";
            else if (type == typeof(string))
                tmp = "string";
            else if (type == typeof(ArrayList))
                tmp = "array";
            else
                tmp = type.ToString().Replace("System.", "").ToLower();

            return tmp;

        }

        #endregion

        #region Public Methods

        public Routine(Type result, string name, List<Type> parameters) {

            Validate(result);

            foreach (Type parameter in parameters)
                Validate(parameter);

            this.result = result;
            this.name = name;
            this.parameters = parameters;

            host = null;

        }

        public Routine(Type result, string name, List<Type> parameterTypes, string description)
        {

            Validate(result);

            foreach (Type parameter in parameterTypes)
                Validate(parameter);

            this.result = result;
            this.name = name;
            this.parameters = parameterTypes;
            this.description = description;
            host = null;

        }

        public Routine(string name) : this(null, name, new List<Type>())
        {
        }

        public Routine(string name, string description) : this(null, name, new List<Type>(), description)
        {
        }

        public Routine(Type result, string name) : this(result, name, new List<Type>())
        {
        }

        public Routine(Type result, string name, string description) : this(result, name, new List<Type>(), description)
        {
        }

        public Routine(Type result, string name, Type parameter) : this(result, name, new List<Type>())
        {
            parameters.Add(parameter);
        }

        public Routine(Type result, string name, Type parameter, string description) : this(result, name, new List<Type>(), description)
        {
            parameters.Add(parameter);
        }

        public Routine(Type result, string name, Type parameter0, Type parameter1) : this(result, name, new List<Type>())
        {
            parameters.Add(parameter0);
            parameters.Add(parameter1);
        }

        public Routine(Type result, string name, Type parameter0, Type parameter1, string description) : this(result, name, new List<Type>(), description)
        {
            parameters.Add(parameter0);
            parameters.Add(parameter1);
        }

        public Routine(Type result, string name, Type parameter0, Type parameter1, Type parameter2) : this(result, name, new List<Type>())
        {
            parameters.Add(parameter0);
            parameters.Add(parameter1);
            parameters.Add(parameter2);
        }

        public Routine(Type result, string name, Type parameter0, Type parameter1, Type parameter2, string description) : this(result, name, new List<Type>(), description)
        {
            parameters.Add(parameter0);
            parameters.Add(parameter1);
            parameters.Add(parameter2);
        }

        /// <summary>
        /// Verify the parameter types of a Routine. If null or void was specified values arent verified
        /// </summary>
        /// <param name="parameters"></param>
        public void Verify(List<object> parameters)
        {

            if (parameters.Count != this.parameters.Count)
                throw new ExecutionException("Die Routine '" + name + "' wurde mit " + parameters.Count  + " statt erwarteten " + this.parameters.Count  + " Parametern aufgerufen.");

            for (int i = 0; i < parameters.Count; i++)
            {

                if (null == this.parameters[i] || null == parameters[i])
                    continue;

                if (typeof(void) == this.parameters[i] || typeof(void) == parameters[i])
                    continue;

                Type expected = this.parameters[i];

                Type specified = parameters[i].GetType();

                if (expected != specified)
                    throw new ExecutionException("Typ '" + specified.Name + "' statt erwartetem Typ '" + expected.Name + "' als " + (i + 1) + " Parameter von '" + name +"' angegeben.");

            }

        }

        /// <summary>
        /// Verify the result of a Routine. If null or void was specified values arent verified
        /// </summary>
        /// <param name="result"></param>
        public void Verify(object result)
        {

            if (null == this.result || null == result)
                return;

            if (typeof(void) == this.result || typeof(void) == result)
                return;

            if (result.GetType() != this.result)
                throw new ExecutionException("Typ '" + result.GetType().Name + "' statt erwartetem Typ '"  + this.result.Name + "' als Ergebnis von '" + name + "' erhalten.");

        }

        public override string ToString()
        {

            StringBuilder sb = new StringBuilder();

            //if(result != (Type)null)
                sb.Append(ToString(result) + " ");

            sb.Append(name);

            sb.Append("(");

            int i = 0;

            for (i = 0; i < parameters.Count; i++)
            {

                if (i > 0)
                    sb.Append(", ");

                sb.Append(ToString(parameters[i]));

            }

            sb.Append(")");

            return sb.ToString();

        }

        public string Description()
        {
            return description;
        }

        #endregion

        #region Public Properties

        public string Name
        {
            get { return name; }
        }

        public List<Type> ParameterTypes
        {
            get { return parameters; }
        }

        public Type Result
        {
            get { return result; }
        }

        public Host Handler
        {
            get { return host; }
            internal set { host = value; }
        }

        #endregion

    }

}
