Now available via [nuget.org](https://www.nuget.org/packages/ScriptStack/)

### What is ScriptStack? 

A customizable scripting language you can embed in your new or existing project, unity game or as standalone interpreter. It is designed to be both easy to use and easy to integrate while being as flexible as possible.

```CSharp
Manager manager = new Manager();
Script script = new Script(manager, "script.txt");
interpreter.Handler = this;
interpreter.Interpret();
```

You can also just execute a specified function inside a script

```CSharp
Interpreter interpreter = new Interpreter(script.Functions["myFunction"]);
interpreter.Handler = this;
interpreter.Interpret();
```

To pass objects between the script and the host application you can use the managed local memory of the interpreter

```CSharp
interpreter.LocalMemory["testParam"] = 12;
interpreter.Interpret();
int testParamResult = (int)interpreter.LocalMemory["testParam"];
```
or the shared managed memory of the manager to pass it between several interpreters.

```CSharp
manager.SharedMemory["testParam"] = 12;
interpreter.Interpret();
int testParamResult = (int)manager.SharedMemory["testParam"];
```
