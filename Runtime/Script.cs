using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using ScriptStack.Collections;
using ScriptStack.Runtime;
using ScriptStack.Compiler;

namespace ScriptStack.Runtime
{

    /// <summary>
    /// Internal representation of a text file (source code) which can be passed to the Interpreter to execute it.
    /// </summary>
    public class Script
    {

        #region Private Variables

        private Manager manager;
        private String scriptName;
        private List<String> sourceCode;
        private Executable executable;

        #endregion

        #region Private Methods

        private void Scan(string scriptName)
        {

            Scanner scanner = manager.Scanner;
            sourceCode = scanner.Scan(scriptName);
            sourceCode.Add(" ");

            Dictionary<string, bool> included = new Dictionary<string, bool>();

            for (int i = 0; i < sourceCode.Count; i++)
            {

                string line = sourceCode[i];

                Lexer lexer = new Lexer(new List<string> { line });

                List<Token> tokenStream = null;

                try
                {
                    tokenStream = lexer.GetTokens();
                }
                catch (Exception)
                {
                    continue;
                }

                if (tokenStream.Count == 0)
                    continue;

                if (tokenStream[0].Type != TokenType.Include)
                    continue;

                if (tokenStream.Count < 2)
                    throw new ParserException("Include Statement ohne Pfadangabe.");

                if (tokenStream[1].Type != TokenType.String)
                    throw new ParserException("Nach einem 'include' Befehl wird ein String (Pfad) erwartet.");

                if (tokenStream.Count < 3)
                    throw new ParserException("Semicolon ';' am Ende eines 'include' Statement erwartet.");

                if (tokenStream[2].Type != TokenType.SemiColon)
                    throw new ParserException("Semicolon ';' am Ende eines 'include' Statement erwartet.");

                if (tokenStream.Count > 3)
                    throw new ParserException("Es wird nichts nach dem Semicolon ';' am Ende eines 'include' Statement erwartet.");

                string include = (string)tokenStream[1].Lexeme;

                sourceCode.RemoveAt(i);

                if (included.ContainsKey(include))
                    continue;

                /* and place the source where the original include statement was.. */
                sourceCode.InsertRange(i, scanner.Scan(include));

                /* set the current script as already included */
                included[include] = true;

                --i;

            }

        }

        #endregion

        #region Public Methods

        public Script(Manager manager, string scriptName)
        {

            this.manager = manager;

            this.scriptName = scriptName;

            try
            {

                Scan(scriptName);

                Lexer lexer = new Lexer(sourceCode);

                List<Token> tokenStream = lexer.GetTokens();

                Parser parser = new Parser(this, tokenStream);

                parser.DebugMode = this.manager.Debug;

                executable = parser.Parse();

                
                if (this.manager.Optimize)
                {

                    Optimizer optimizer = new Optimizer(executable);

                    optimizer.OptimizerInfo = false;

                    optimizer.Optimize();

                }
                

            }
            catch (Exception exception)
            {
                throw new ScriptStackException("Fehler in '" + scriptName + "'.", exception);
            }

        }

        public bool EntryPoint()
        {
            return executable.FunctionExists("main");
        }

        #endregion

        #region Public Properties

        public Manager Manager
        {
            get { return manager; }
        }

        public string Name
        {
            get { return scriptName; }
        }

        public ReadOnlyCollection<String> SourceLines
        {
            get { return sourceCode.AsReadOnly(); } 
        }

        public string Source
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (string line in sourceCode)
                {
                    sb.Append(line);
                    sb.Append("\r\n");
                }
                return sb.ToString();
            }
        }

        public Executable Executable
        {
            get { return executable; }
        }

        public Memory ScriptMemory
        {
            get { return executable.ScriptMemory; }
        }

        public ScriptStack.Collections.ReadOnlyDictionary<String, Function> Functions
        {
            get
            {
                return new ScriptStack.Collections.ReadOnlyDictionary<String,Function>(executable.Functions);
            }
        }

        public Function MainFunction
        {
            get { return executable.MainFunction; }
        }

        #endregion

    }
}
