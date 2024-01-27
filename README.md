# ScriptStack

Auch via [nuget.org](https://www.nuget.org/packages/ScriptStack/).

# Examples

Writing a host application.

```CSharp
class ScriptStack : Host
{
 
    private Manager manager;
    private Script script;
    private Interpreter interpreter;
 
    public ScriptStack(String[] args)
    {
    
        manager = new Manager();
 
        manager.Register(new Routine((Type)null, "print", (Type)null));
 
        script = new Script(manager, args[0]);
 
        interpreter = new Interpreter(script);
                      
        interpreter.Handler = this;
 
        while (!interpreter.Finished)
            interpreter.Interpret(1);
 
    }
 
    public object Invoke(string routine, List<object> parameters)
    {
 
        if (routine == "print")
        {
            // do some stuff..
        }
 
        return null;
 
    }
 
}
```

Writing a plugin.

```CSharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
 
using ScriptStack;
 
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
