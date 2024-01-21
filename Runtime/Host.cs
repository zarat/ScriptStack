using System;
using System.Collections.Generic;
using System.Text;

using ScriptStack.Runtime;

namespace ScriptStack.Runtime
{

    /// <summary>
    /// The main interface to create a Host. A Host can implement Routine's to extend its functionality.
    /// 
    /// A Host must implement the Invoke() method. As you can see, it is just an empty declaration to hook in.
    /// 
    /// Every time a Routine is requested from within a script, this method is invoked.
    /// 
    /// You have to implement the logic (the function body) inside this method! 
    /// 
    /// The name of the Routine is passed as string as 1st parameter. Parameters are passed as a list of objects as 2nd parameter.
    /// 
    /// A Host class with a registered "print" Routine would look like the following example.
    /// 
    /// ```
    /// 
    /// class ScriptStack : Host
    /// {
    /// 
    ///     private Manager manager;
    ///     private Script script;
    ///     private Interpreter interpreter;
    /// 
    ///     public ScriptStack(String[] args)
    ///     {
    ///     
    ///         manager = new Manager();
    /// 
    ///         manager.Register(new Routine((Type)null, "print", (Type)null));
    /// 
    ///         script = new Script(manager, args[0]);
    /// 
    ///         interpreter = new Interpreter(script);
    ///                       
    ///         interpreter.Handler = this;
    /// 
    ///         while (!interpreter.Finished)
    ///             interpreter.Interpret(1);
    /// 
    ///     }
    /// 
    ///     public object Invoke(string routine, List<object> parameters)
    ///     {
    /// 
    ///         if (routine == "print")
    ///         {
    ///             // do some stuff..
    ///         }
    /// 
    ///         return null;
    /// 
    ///     }
    /// 
    /// }
    /// 
    /// ```
    /// 
    /// You can call Routine's directly by calling the Invoke method on the corresponding handler (Host / Model) passing its name and a list of parameters (if needed)
    /// 
    /// ```
    /// 
    /// Routine myRoutine = manager.Routines["print"];
    /// 
    /// List<object> parameters = new List<object>();
    /// parameters.Add("Hello world");
    /// 
    /// myRoutine.Handler.Invoke("print", parameters);
    /// 
    /// ```
    /// 
    /// </summary>
    public interface Host {

        /// <summary>
        /// Called when a Routine is invoked
        /// </summary>
        /// <param name="routine"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        object Invoke(string routine, List<object> parameters);

    }

}
