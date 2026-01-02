# ScriptStack

A managed .NET scripting language to integrate into games (Unity3d, Godot) or any other application. See also 

- [StackShell](https://github.com/zarat/stackshell) - A simple console implementation with a number of plugins
- [Debugger](https://github.com/zarat/debugger) - A simple development environment and runtime debugger

# Features
- Multithreaded - run code in parallel
- Mutex locking - share data securely between asynchronous threads
- wait/notify - pause execution until a specific event is thrown
- CLR Interop - Use native C# Classes in your script
- Sandbox mode - only allow pre-defined CLR capabilities

# Lexer and Parser standalone
```CSharp
// 1) Lex + ParseTokens
List<Token> stream = Standalone.Lex("var a;var b; function main() { a = 1; b = 2; if(b > a) std.print(b); }");
Executable exec = Standalone.ParseTokens(stream, manager);

// 2) WICHTIG: exec an Script hängen, sonst crasht Interpreter (Script.Executable == null)
typeof(Script).GetField("executable", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(exec.Script, exec);

// 3) Interpreter starten
var interpreter = new Interpreter(exec.Script);
```
Wenn man TokenStream nicht braucht
```CSharp
var script = Standalone.CompileToScript("var a;var b; function main() { a = 1; b = 2; if(b > a) std.print(b); }", manager);
var interpreter = new Interpreter(script);
```
