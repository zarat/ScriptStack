# ScriptStack

A managed .NET scripting language to integrate into games (Unity3d, Godot) or any other application.

# Examples

Writing a host application requires to implement the <code>Host</code> interface and the method <code>Invoke</code> with this signature.

```
public object Invoke(string functionName, List<object> parameters)
````

Here is a full example.

```CSharp
using ScriptStack;
using ScriptStack.Compiler;
using ScriptStack.Runtime;
using System.Collections.Generic;
using System;

namespace StackShell
{
    class StackShell : Host
    {

        private static Manager manager;
        private static Script script;
        private static Interpreter interpreter;

        public static void Main(String[] args)
        {

            manager = new Manager();

            manager.Register(new Routine((Type)null, "print", (Type)null));

            script = new Script(manager, args[0]);

            interpreter = new Interpreter(script);

            interpreter.Handler = new StackShell();

            while (!interpreter.Finished)
                interpreter.Interpret(1);

        }

        public object Invoke(string functionName, List<object> parameters)
        {

            if (functionName == "print")
            {
                System.Console.WriteLine(parameters[0]);
            }

            return null;

        }

    }

}
```

Writing a model (plugin) requires to implement the <code>Model</code> interface and the method <code>Invoke</code> with this signature.

```
public object Invoke(string functionName, List<object> parameters)
```

In addition it must expose a <code>List\<Routine\></code> of all the (script) functions it implements.

```
public ReadOnlyCollection<Routine> Routines
```

Here is a full example.

```CSharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
 
using ScriptStack;
using ScriptStack.Compiler;
using ScriptStack.Runtime;
 
namespace DemoModel
{
 
    public class DemoModel : Model
    {
 
        private static ReadOnlyCollection<Routine> prototypes;
 
        public ReadOnlyCollection<Routine> Routines
        {
            get { return prototypes; }
        }
 
        public DemoModel()
        {
 
            if (prototypes != null)
                return;
 
            List<Routine> routines = new List<Routine>();
 
            routines.Add(new Routine((Type)null, "toUpper", (Type)null, "Make everything upper case"));
            routines.Add(new Routine((Type)null, "toLower", (Type)null, "Make everything lower case"));
 
            prototypes = routines.AsReadOnly();
 
        }
 
        public object Invoke(String functionName, List<object> parameters)
        {
 
            if (functionName == "toUpper")
            {
 
                return parameters[0].ToString().ToUpper();
 
            }
 
            if (functionName == "toLower")
            {
 
                return parameters[0].ToString().ToLower();
 
            }
 
            return null;
 
        }
 
    }
 
}
```
