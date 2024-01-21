using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using ScriptStack.Compiler;
using ScriptStack.Runtime;

namespace ScriptStack.Runtime
{
    /*
    /// <summary>
    /// A Model is an abstract representation of an object your Host works with. It implements Routine's and can be registered by a Host to exted the Host's functionality.
    /// 
    /// A Model must implement the Invoke() method. As you can see, it is just an empty declaration to hook in.
    /// 
    /// Every time a Routine is requested from within a script, this method is invoked either on the Host or on the Model which provides the requested Routine.
    ///
    /// A Model must also implement a Prototypes property with a public 'getter' which returns a ReadOnlyCollection of all Routine's.
    /// 
    /// The logic (the function body) of the Routine's is implemented inside the Invoke method. 
    /// 
    /// A Model class with 2 registered Routines ("toUpper", "toLower") would look like the following example.
    /// 
    /// ```
    /// 
    /// using System;
    /// using System.Collections.Generic;
    /// using System.Collections.ObjectModel;
    /// 
    /// using ScriptStack;
    /// 
    /// namespace DemoModel
    /// {
    /// 
    ///     public class DemoModel : Model
    ///     {
    /// 
    ///         private static ReadOnlyCollection<Routine> prototypes;
    /// 
    ///         public ReadOnlyCollection<Routine> Routines
    ///         {
    ///             get { return prototypes; }
    ///         }
    /// 
    ///         public DemoModel()
    ///         {
    /// 
    ///             if (prototypes != null)
    ///                 return;
    /// 
    ///             List<Routine> routines = new List<Routine>();
    /// 
    ///             routines.Add(new Routine((Type)null, "toUpper", (Type)null, "Make everything upper case"));
    ///             routines.Add(new Routine((Type)null, "toLower", (Type)null, "Make everything lower case"));
    /// 
    ///             prototypes = routines.AsReadOnly();
    /// 
    ///         }
    /// 
    ///         public object Invoke(String functionName, List<object> parameters)
    ///         {
    /// 
    ///             if (functionName == "toUpper")
    ///             {
    /// 
    ///                 return parameters[0].ToString().ToUpper();
    /// 
    ///             }
    /// 
    ///             if (functionName == "toLower")
    ///             {
    /// 
    ///                 return parameters[0].ToString().ToLower();
    /// 
    ///             }
    /// 
    ///             return null;
    /// 
    ///         }
    /// 
    ///     }
    /// 
    /// }
    /// 
    /// ```
    /// 
    /// </summary>
    */
    public interface Model : Host
    {

        /// <summary>
        /// Returns all Routine's a Model implements
        /// </summary>
        ReadOnlyCollection<Routine> Routines { get; }

    }
}
