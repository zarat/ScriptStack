using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace ScriptStack.Runtime
{
    public enum Scope
    {
        Shared,
        Script,
        Local
    }

    public class Memory
    {

        private Scope scope;
        private Memory sharedMemory;
        private Memory scriptMemory;
        private Dictionary<string, object> variables;
        private Dictionary<string, object> tempVariables;

        private Memory(Scope scope, Memory sharedMemory, Memory scriptMemory)
        {
            this.scope = scope;
            this.sharedMemory = sharedMemory;
            this.scriptMemory = scriptMemory;
            variables = new Dictionary<string, object>();
            tempVariables = new Dictionary<string, object>();
        }

        internal void HideTemporaryVariables()
        {

            foreach (String identifier in variables.Keys)
                if (identifier.StartsWith("["))
                    tempVariables[identifier] = variables[identifier];

            foreach (String strIdentifier in tempVariables.Keys)
                variables.Remove(strIdentifier);

        }

        internal void ExposeTemporaryVariables()
        {

            foreach (String identifier in tempVariables.Keys)
                variables[identifier] = tempVariables[identifier];

            tempVariables.Clear();

        }

        public static Memory AllocateSharedMemory()
        {
            return new Memory(Scope.Shared, null, null);
        }

        public static Memory AllocateScriptMemory(Memory sharedMemory)
        {
            return new Memory(Scope.Script, sharedMemory, null);
        }

        public static Memory AllocateLocalMemory(Memory scriptMemory)
        {
            return new Memory(
                Scope.Local, scriptMemory.sharedMemory, scriptMemory);
        }

        public void Clear()
        {
            variables.Clear();
            tempVariables.Clear();
        }

        public bool Exists(string identifier)
        {

            switch (scope)
            {

                case Scope.Shared:
                    return variables.ContainsKey(identifier);

                case Scope.Script:
                    if (variables.ContainsKey(identifier))
                        return true;
                    else
                        return sharedMemory.Exists(identifier);

                case Scope.Local:
                    if (variables.ContainsKey(identifier))
                        return true;
                    else
                        return scriptMemory.Exists(identifier);

                default:
                    throw new ExecutionException("Der Scope '" + scope + "' ist unkelannt.");

            }

        }

        public void Remove(string identifier)
        {
            variables.Remove(identifier);
        }

        public Scope Find(string identifier)
        {
            switch (scope)
            {

                case Scope.Shared:
                    if (variables.ContainsKey(identifier))
                        return scope;
                    else
                        throw new ExecutionException("Variable '" + identifier + "' undefined.");

                case Scope.Script:
                    if (variables.ContainsKey(identifier))
                        return scope;
                    else
                        return sharedMemory.Find(identifier);

                case Scope.Local:
                    if (variables.ContainsKey(identifier))
                        return scope;
                    else
                        return scriptMemory.Find(identifier);

                default:
                    throw new ExecutionException("Unknown scope: " + scope);

            }

        }

        public ReadOnlyCollection<string> Identifiers
        {
            get
            {
                List<String> listIdentifiers
                    = new List<String>(variables.Keys);
                return listIdentifiers.AsReadOnly();
            }
        }

        public object this[string identifier]
        {
            get
            {
                switch (scope)
                {
                    case Scope.Shared:
                        if (!variables.ContainsKey(identifier))
                            throw new ExecutionException( "Globale Variable '" + identifier + "' wurde nicht deklariert.");
                        return variables[identifier];
                    case Scope.Script:
                        if (variables.ContainsKey(identifier))
                            return variables[identifier];
                        else
                            return sharedMemory[identifier];
                    case Scope.Local:
                        if (variables.ContainsKey(identifier))
                            return variables[identifier];
                        else
                            return scriptMemory[identifier];
                    default:
                        throw new ExecutionException("Der Scope '" + scope + "' int unbekannt.");
                }
            }
            set
            {
                if (!Exists(identifier))
                    variables[identifier] = value;
                else
                {
                    switch (scope)
                    {
                        case Scope.Shared:
                            variables[identifier] = value;
                            break;
                        case Scope.Script:
                            if (sharedMemory.Exists(identifier))
                                sharedMemory[identifier] = value;
                            else
                                variables[identifier] = value;
                            break;
                        case Scope.Local:
                            if (scriptMemory.Exists(identifier))
                                scriptMemory[identifier] = value;
                            else
                                variables[identifier] = value;
                            break;
                    }
                }
            }
        }

        public Scope Scope
        {
            get { return scope; }
        }

    }

}
