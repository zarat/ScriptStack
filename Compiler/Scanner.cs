using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptStack.Compiler
{

    /// <summary>
    /// An interface to modify the default process of reading text files into Script's. 
    /// 
    /// THIS INTERFACE IS IN TESTING
    /// 
    /// There is a default routine how a script (text file) gets loaded into the scanner. But its a private method of Script.
    /// This interface has a public prototype called "Scan". Using this method you can hook into the private method in Script.
    /// Before the scanner actually reads the file it calls the "Scan" method from this interface. 
    /// You can preprocess lines which are not part of code.
    /// 
    /// When a script is loaded it gets split into a list of lines, each element holding a line of the script.
    /// For e.g the default process of loading scripts does resolve "include" statements. 
    /// It loads the included file into a list and replace the statement in the source script with the included lines.
    /// 
    /// The custom scanner must be assigned to the <see cref="Manager"/> using its <see cref="Manager.Scanner"/> property.
    /// 
    /// <seealso cref="Lexer"/> <seealso cref="Token"/>
    /// 
    /// ```
    /// 
    /// class MyScanner : Scanner
    /// {
    /// 
    ///     private Manager manager;
    /// 
    ///     public MyScanner(Manager manager)
    ///     {
    ///         this.manager = manager;
    ///     }
    /// 
    ///     public List<string> Scan(string script)
    ///     {
    /// 
    ///         try
    ///         {
    /// 
    ///              // The method must return a list of strings, each element is a line in code.
    ///              List<string> code = new List<string>();
    ///              
    ///              StreamReader streamReader = new StreamReader(script);
    ///              
    ///              // One possibility is to inject code into the script
    ///              code.Add("function setup() {");
    ///              code.Add("var i = 12;");
    ///              code.Add("return i;");
    ///              code.Add("}");
    ///              
    ///              string currentLine;
    ///              
    ///              while (!streamReader.EndOfStream)
    ///              {
    ///  
    ///                  currentLine = streamReader.ReadLine();
    ///                  
    ///                  // Another possibility is to replace code
    ///                  // To debug scripts i will add kind of "compiler switch" which must start with a '#'.
    ///                  // When a line starts with '#' i know its a "switch" and not part of the code, so i will process it but dont add it to the list.
    ///                  if (currentLine.StartsWith("#"))
    ///                  {
    ///                  
    ///                      currentLine = currentLine.Replace("#", "");
    ///                      string[] parts = currentLine.Split('=');
    ///                      if (parts[0].Trim() == "debug" && parts[1].Trim() == "true") 
    ///                          manager.Debug = true;
    ///                          
    ///                  }
    ///                  
    ///                  // One more possibility is to use the internal Lexer to get a token stream to analyze
    ///                  // Its a bit complex, examples are coming soon.
    ///                  else if (currentLine.StartsWith("."))
    ///                  {
    /// 
    ///                      string[] parts = currentLine.Split('.');
    ///                      List<string> tmp = new List<string>();
    ///                      tmp.Add(parts[1]);
    ///                      Lexer lexer = new Lexer(tmp);
    ///                      foreach (Token tok in lexer.GetTokens())
    ///                          Console.WriteLine(tok.ToString());
    /// 
    ///                  } 
    ///                 
    ///                  // Otherwise, if the line does not start with any of these, i just add it to the list
    ///                  else
    ///                      code.Add(currentLine);
    ///  
    ///              }
    ///  
    ///              streamReader.Close();
    ///  
    ///              // Return the list of strings
    ///              return code;
    /// 
    ///         }
    ///         catch (ZVMException exception)
    ///         {
    ///             
    ///         }
    /// 
    ///     }
    /// 
    /// }
    /// 
    /// ```
    /// 
    /// 
    /// </summary>
    /// \todo Load event
    public interface Scanner
    {

        #region Public Methods

        List<String> Scan(String strResourceName);

        #endregion

    }
}
