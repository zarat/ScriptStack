using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ScriptStack.Plugins
{
    public static class PluginLoader
    {
        public static List<Plugin> LoadPlugins(string pluginsRoot, ScriptStack.Manager manager, string[]? sharedAssemblyNames = null)
        {
            var plugins = new List<Plugin>();
            if (!Directory.Exists(pluginsRoot)) return plugins;
            var shared = sharedAssemblyNames ?? new[] { Assembly.GetExecutingAssembly().GetName().Name! };

            foreach (var pluginDir in Directory.GetDirectories(pluginsRoot))
            {
                try
                {
                    var dlls = Directory.GetFiles(pluginDir, "*.dll");
                    if (dlls.Length == 0) continue;

                    string pluginDll = dlls.FirstOrDefault(f =>
                        string.Equals(Path.GetFileNameWithoutExtension(f), Path.GetFileName(pluginDir), StringComparison.OrdinalIgnoreCase))
                        ?? dlls[0];

                    var loadContext = new PluginLoadContext(pluginDll, shared);
                    var assembly = loadContext.LoadFromAssemblyPath(pluginDll);

                    foreach (var type in assembly.GetExportedTypes())
                    {
                        if (!typeof(ScriptStack.Runtime.Model).IsAssignableFrom(type)) continue;
                        var ctor = type.GetConstructor(Type.EmptyTypes);
                        if (ctor == null) continue;
                        var obj = ctor.Invoke(Array.Empty<object>());
                        var model = (ScriptStack.Runtime.Model)obj;
                        manager.Register(model);

                        plugins.Add(new Plugin
                        {
                            Directory = pluginDir,
                            LoadContext = loadContext,
                            Assembly = assembly,
                            Instance = model
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PluginLoader] Fehler beim Laden von '{pluginDir}': {ex.Message}");
                }
            }

            return plugins;
        }
    }
}
