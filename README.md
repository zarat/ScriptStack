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

### What to do with it?

As a developer, it's near impossible to think of all the ways people will use your app. Wouldn't it be cool to let your users create scripts to customize your app to their own personal needs? Using an embedded scripting language you can allow users to control the behavior, look or functionality of your app. Some examples are

* Custom scripting for Microsoft Office AddIns (example [WordAddin](https://github.com/zarat/ScriptStack/raw/master/WordAddin_VS2017.zip))
* Executing scripts instead of reading config files (used for cheats in this [example Game](https://github.com/zarat/PolyStrike))
* Create dynamic dial plans, extend mailfilter, extract attachments or create dynamic mailing lists and queues

and so on. You can use the [DemoApplication](https://github.com/zarat/ScriptStack/raw/master/DemoApplication_VS2017.zip) as a starting point.

[Grid Ruler](https://github.com/zarat/gridruler) is an example app to filter and manipulate csv files using user defined rules. There are 2 type of rules - conditions and actions. Conditions are based on the value in a cell. Conditions are like (if < number, if > number,..) or (if string contains, if string starts with..). Actions - as the name implies - executes an action on the row when the Contition is met. Actions are like (remove row, change background color,..). Sounds good, but a new type of rule had to be deployed using an update. Using ScriptStack a script or a single function of a script can be executed on each row to extend the hardcoded into a very dynamic rule evaluated at runtime.

![image](https://raw.githubusercontent.com/zarat/GridRuler/master/gridruler.gif)

### More details?

For custom support or any questions please write an email to scriptstackproject@gmail.com, any request can also be submitted by opening an issue.
