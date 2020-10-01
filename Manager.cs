using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Text;

using ScriptStack.Collections;
using ScriptStack.Compiler;
using ScriptStack.Runtime;

namespace ScriptStack
{

    /// <summary>
    /// API entry point.
    /// </summary>
    /// \todo Sandboxing See https://stackoverflow.com/questions/35061043/dynamically-load-assembly-from-local-file-and-run-with-restricted-privileges
    /// 
    public class Manager
    {

        #region Private Variables

        private string name;
        private Scanner scanner;
        private Memory sharedMemory;
        private Dictionary<string, Routine> routines;
        private Dictionary<object, Interpreter> locks;
        private bool debug;
        private bool optimize;

        #endregion

        #region Internal Properties

        internal Dictionary<object, Interpreter> Locks
        {
            get { return locks; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// A Manager object is responsible for memory management, type evaluation and loading of plugins
        /// </summary>
        public Manager()
        {

            scanner = new ScannerPrototype();

            sharedMemory = Memory.AllocateSharedMemory();

            routines = new Dictionary<string, Routine>();

            locks = new Dictionary<object, Interpreter>();

            debug = false;

            optimize = true;

        }

        public void LoadComponents(string relativeDirectoryPath)
        {

            if (!Directory.Exists(System.AppDomain.CurrentDomain.BaseDirectory + relativeDirectoryPath))
                return;

            foreach (string dll in System.IO.Directory.GetFiles(System.AppDomain.CurrentDomain.BaseDirectory + relativeDirectoryPath, "*.dll"))
            {

                try
                {

                    foreach (Type type in System.Reflection.Assembly.LoadFrom(dll).GetExportedTypes())
                        if (typeof(Model).IsAssignableFrom(type))
                            Register((Model)type.GetConstructor(new Type[0]).Invoke(new object[0]));

                }
                catch (ScriptStackException e)
                {
                    /* plugins (always?) has to be allowed explicitely in windows. If so, display a hint */
                    //Console.WriteLine("Error: " + e);
                }

            }
        }
        public String Name
        {
            get { return name; }
            set {
                name = value;
            }
        }

        public bool IsRegistered(string routine)
        {
            return routines.ContainsKey(routine);
        }

        public void Register(Model model)
        {

            foreach (Routine routine in model.Routines)
                Register(routine, model);

        }

        public void Register(Routine routine, Host host)
        {

            string name = routine.Name;

            if (routines.ContainsKey(name))
                throw new ScriptStackException("Die Routine '" + name + "' ist bereits registriert.");

            routine.Handler = host;

            routines[name] = routine;

        }

        public void UnRegister(string routine)
        {

            if (!routines.ContainsKey(routine))
                throw new ScriptStackException("Die Routine '" + routine + "' wurde nicht gefunden.");

            routines.Remove(routine);

        }

        /// <summary>
        /// Register a new Routine
        /// </summary>
        /// <param name="routine"></param>
        public void Register(Routine routine)
        {
            Register(routine, null);
        }

        public void ClearActiveLocks()
        {
            locks.Clear();
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// A Scanner reference
        /// </summary>
        public Scanner Scanner
        {
            get { return scanner; }
            set { scanner = value; }
        }

        public Memory SharedMemory
        {
            get { return sharedMemory; }
        }

        public ReadOnlyDictionary<String, Routine> Routines {
            get
            {
                return new ReadOnlyDictionary<string, Routine>(routines);
            }
        }

        public bool Debug
        {
            get { return debug; }
            set { debug = value; }
        }

        public bool Optimize
        {
            get { return optimize; }
            set { optimize = value; }
        }

        public ReadOnlyDictionary<object, Interpreter> ActiveLocks
        {
            get { return new ReadOnlyDictionary<object,Interpreter>(locks); }
        }

        #endregion

    }

}
