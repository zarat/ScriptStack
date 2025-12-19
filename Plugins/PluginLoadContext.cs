using System;
using System.Reflection;
using System.Runtime.Loader;

namespace ScriptStack.Plugins
{
    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string[] _sharedAssemblies;

        public PluginLoadContext(string pluginMainAssemblyPath, string[]? sharedAssemblyNames = null)
            : base(isCollectible: false)
        {
            _resolver = new AssemblyDependencyResolver(pluginMainAssemblyPath);
            _sharedAssemblies = sharedAssemblyNames ?? Array.Empty<string>();
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            foreach (var shared in _sharedAssemblies)
            {
                if (string.Equals(shared, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
                {
                    try { return Assembly.Load(assemblyName); } catch { return null; }
                }
            }

            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
                return LoadFromAssemblyPath(assemblyPath);

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (path != null) return LoadUnmanagedDllFromPath(path);
            return base.LoadUnmanagedDll(unmanagedDllName);
        }
    }
}
