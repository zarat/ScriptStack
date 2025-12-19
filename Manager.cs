using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using ScriptStack.Collections;
using ScriptStack.Compiler;
using ScriptStack.Runtime;
using ScriptStack.Plugins;

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

        // Plugins loaded with isolated ALCs (keep references to prevent premature unload)
        private List<Plugin> loadedPlugins = new List<Plugin>();

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

            loadedPlugins = new List<Plugin>();

        }

        public Func<List<string>, Lexer> LexerFactory { get; set; } = lines => new Lexer(lines);

        public void LoadComponents(string relativeDirectoryPath)
        {

            string path = relativeDirectoryPath; // System.AppDomain.CurrentDomain.BaseDirectory;

            if (!Directory.Exists(path))
                return;

            var subdirs = Directory.GetDirectories(path);
            if (subdirs.Length > 0)
            {
                var shared = new[] { Assembly.GetExecutingAssembly().GetName().Name! };
                var loaded = ScriptStack.Plugins.PluginLoader.LoadPlugins(path, this, shared);
                this.loadedPlugins = loaded;
                return;
            }


            foreach (string dll in System.IO.Directory.GetFiles(path, "*.dll"))
            {

                if (dll == path + "ScriptStack.dll")
                    continue;

                Assembly assembly = null;

                try
                {
                    assembly = Assembly.LoadFile(dll);
                }
                catch (Exception e) { Console.WriteLine($"[LoadComponents] Fehler beim Laden '{dll}': {e.Message}"); continue; }

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

        /// <summary>
        /// Unload all plugins that were loaded via LoadComponents (subdirectory mode).
        /// This will deregister their routines and unload their ALCs (collectible).
        /// </summary>
        public void UnloadPlugins()
        {
            if (loadedPlugins == null || loadedPlugins.Count == 0)
                return;

            // Deregister routines
            foreach (var plugin in loadedPlugins)
            {
                try
                {
                    if (plugin?.Instance is Model model)
                    {
                        foreach (var r in model.Routines)
                        {
                            if (r != null && routines.ContainsKey(r.Name))
                                routines.Remove(r.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UnloadPlugins] Fehler beim Deregistrieren: {ex.Message}");
                }
            }

            // Unload contexts
            foreach (var plugin in loadedPlugins)
            {
                try
                {
                    plugin.LoadContext?.Unload();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UnloadPlugins] Fehler beim Unload: {ex.Message}");
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            loadedPlugins.Clear();
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
