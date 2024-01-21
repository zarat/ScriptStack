using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using ScriptStack.Compiler;

namespace ScriptStack.Runtime
{

    /// <summary>
    /// A function, forward declared in a script
    /// </summary>
    public class Function
    {

        #region Private Variables

        private Executable executable;
        private string name;
        private List<string> parameters;
        private Instruction entryPoint;

        #endregion

        #region Public Methods

        public Function(Executable executable, string name, List<string> parameters, Instruction entryPoint)
        {

            this.executable = executable;

            this.name = name;

            this.parameters = new List<string>(parameters);

            this.entryPoint = entryPoint;

        }

        public override string ToString()
        {

            StringBuilder sb = new StringBuilder();

            sb.Append(name + "@" + entryPoint.Address.ToString("X8"));

            sb.Append("(");

            for (int i = 0; i < parameters.Count; i++)
            {

                if (i > 0)
                    sb.Append(", ");

                sb.Append(parameters[i]);

            }

            sb.Append(") ");

            return sb.ToString();

        }

        public Executable Executable
        {
            get { return executable; }
        }

        public string Name
        {
            get { return name; }
        }

        public uint ParameterCount
        {
            get { return (uint) parameters.Count; }
        }

        public ReadOnlyCollection<string> Parameters
        {
            get { return parameters.AsReadOnly(); }
        }

        public Instruction EntryPoint
        {
            get { return entryPoint; }
            set { entryPoint = value; }
        }

        #endregion

    }
}
