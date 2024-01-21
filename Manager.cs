using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using ScriptStack.Collections;
using ScriptStack.Compiler;
using ScriptStack.Runtime;

namespace ScriptStack
{

    /// <summary>
    /// API entry point.
    /// </summary>
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

            string path = relativeDirectoryPath; // System.AppDomain.CurrentDomain.BaseDirectory;

            foreach (string dll in System.IO.Directory.GetFiles(path, "*.dll"))
            {

                if (dll == path + "ScriptStack.dll")
                    continue;

                Assembly assembly = null;

                try
                {
                    assembly = Assembly.LoadFile(dll);
                }
                catch (Exception e) { }

                Type[] arrayTypes = assembly.GetExportedTypes();

                foreach (Type type in arrayTypes)
                {

                    if (!typeof(Model).IsAssignableFrom(type))
                        continue;

                    ConstructorInfo constructorInfo = null;
                    try
                    {
                        constructorInfo = type.GetConstructor(new Type[0]);
                    }
                    catch (Exception e) { continue; }

                    object objectHostModule = constructorInfo.Invoke(new object[0]);
                    Model hostModule = (Model)objectHostModule;

                    /*
                    Console.WriteLine("[INFO] Lade Modul '" + hostModule.ToString() + "'");
                    foreach(Routine r in hostModule.Routines)
                        Console.WriteLine("[INFO] Lade Routine '" + r.Name.ToString() + "'");
                    */

                    Register(hostModule);

                }

            }

        }

        public void LoadComponent(string relativeDirectoryPath)
        {

            Assembly assembly = null;
            try
            {
                assembly = Assembly.LoadFile(relativeDirectoryPath);
            }
            catch (Exception e) { }

            Type[] arrayTypes = assembly.GetExportedTypes();

            foreach (Type type in arrayTypes)
            {

                if (!typeof(Model).IsAssignableFrom(type))
                    continue;

                ConstructorInfo constructorInfo = null;
                try
                {
                    constructorInfo = type.GetConstructor(new Type[0]);
                }
                catch (Exception e) { continue; }

                object objectHostModule = constructorInfo.Invoke(new object[0]);
                Model hostModule = (Model)objectHostModule;

                /*
                Console.WriteLine("[INFO] Lade Modul '" + hostModule.ToString() + "'");
                foreach(Routine r in hostModule.Routines)
                    Console.WriteLine("[INFO] Lade Routine '" + r.Name.ToString() + "'");
                */

                Register(hostModule);

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

        public void UnRegister(Model model)
        {

            foreach (Routine routine in model.Routines)
                UnRegister(routine.Name);

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
