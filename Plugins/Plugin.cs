using System.Reflection;

namespace ScriptStack.Plugins
{
    public class Plugin
    {
        public string Directory { get; set; } = "";
        public PluginLoadContext? LoadContext { get; set; }
        public Assembly? Assembly { get; set; }
        public object? Instance { get; set; }
    }
}
