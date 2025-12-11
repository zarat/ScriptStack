using ScriptStack.Collections;
using ScriptStack.Compiler;
using ScriptStack.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;

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

        private static object ConvertLexeme(SerializableToken st)
        {
            if (st.Lexeme == null)
                return null;

            // Je nach TokenType anders parsen
            switch (st.Type)
            {
                case TokenType.Integer:
                    // z.B. "42" -> 42
                    return int.Parse(st.Lexeme, CultureInfo.InvariantCulture);

                case TokenType.Float:
                    // z.B. "3.14" -> 3.14f
                    return float.Parse(st.Lexeme, CultureInfo.InvariantCulture);

                case TokenType.Double:
                    // z.B. "3.14159" -> 3.14159d
                    return double.Parse(st.Lexeme, CultureInfo.InvariantCulture);

                case TokenType.Boolean:
                    // "true" / "false"
                    return bool.Parse(st.Lexeme);

                case TokenType.Char:
                    // hängt davon ab, wie dein Lexer das Lexeme speichert
                    // Beispiel 1: nur das Zeichen selbst: "a"
                    if (st.Lexeme.Length == 1)
                        return st.Lexeme[0];

                    // Beispiel 2: inklusive Hochkommas: "'a'"
                    if (st.Lexeme.Length >= 3 &&
                        st.Lexeme[0] == '\'' &&
                        st.Lexeme[^1] == '\'')
                    {
                        return st.Lexeme[1];
                    }

                    throw new FormatException($"Ungültiges Char-Lexeme: '{st.Lexeme}'");

                case TokenType.Null:
                    // für 'null' kannst du einfach null zurückgeben
                    return null;

                case TokenType.String:
                    // hier kannst du entscheiden:
                    // - komplette Literal-Syntax behalten ("\"Hallo\"")
                    // - oder die Anführungszeichen entfernen, wenn dein Lexer sie drin hat
                    // Beispiel: Anführungszeichen entfernen:
                    if (st.Lexeme.Length >= 2 &&
                        st.Lexeme[0] == '"' &&
                        st.Lexeme[^1] == '"')
                    {
                        return st.Lexeme.Substring(1, st.Lexeme.Length - 2);
                    }
                    return st.Lexeme;

                default:
                    // für alles andere reicht der String
                    return st.Lexeme;
            }
        }

        private static object ConvertLexeme(TokenType type, string lexemeStr)
        {
            // Falls du für Null-Tokens z.B. ein leeres Lexeme speicherst:
            if (type == TokenType.Null)
                return null;

            if (lexemeStr == null)
                return null;

            switch (type)
            {
                case TokenType.Integer:
                    return int.Parse(lexemeStr, CultureInfo.InvariantCulture);

                case TokenType.Float:
                    return float.Parse(lexemeStr, CultureInfo.InvariantCulture);

                case TokenType.Double:
                    return double.Parse(lexemeStr, CultureInfo.InvariantCulture);

                case TokenType.Boolean:
                    return bool.Parse(lexemeStr);

                case TokenType.Char:
                    // Variante 1: nur das Zeichen selbst gespeichert, z.B. "a"
                    if (lexemeStr.Length == 1)
                        return lexemeStr[0];

                    // Variante 2: inkl. Hochkommas, z.B. "'a'"
                    if (lexemeStr.Length >= 3 &&
                        lexemeStr[0] == '\'' &&
                        lexemeStr[^1] == '\'')
                    {
                        return lexemeStr[1];
                    }

                    throw new FormatException($"Ungültiges Char-Lexeme: '{lexemeStr}'");

                case TokenType.String:
                    // Wenn dein Lexer die Anführungszeichen mit speichert ("\"Hallo\"")
                    if (lexemeStr.Length >= 2 &&
                        lexemeStr[0] == '"' &&
                        lexemeStr[^1] == '"')
                    {
                        return lexemeStr.Substring(1, lexemeStr.Length - 2);
                    }
                    return lexemeStr;

                default:
                    // Für alles andere reicht der String
                    return lexemeStr;
            }
        }

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

        public Script(Manager manager, string scriptName, bool binary)
        {

            this.manager = manager;

            this.scriptName = scriptName;

            try
            {

                List<Token> tokenStream = null;
                if (binary == true)
                {
                    tokenStream = new List<Token>();
                    using (var fs = new FileStream(scriptName, FileMode.Open, FileAccess.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        int count = br.ReadInt32();

                        for (int i = 0; i < count; i++)
                        {
                            var typeInt = br.ReadInt32();
                            var lexemeStr = br.ReadString();
                            var line = br.ReadInt32();
                            var column = br.ReadInt32();
                            var text = ""; // br.ReadString();

                            TokenType type = (TokenType)typeInt;
                            object lexeme = ConvertLexeme(type, lexemeStr);

                            tokenStream.Add(new Token(type, lexeme, line, column, text));
                        }
                    }
                }
                else
                {
                    string json = File.ReadAllText(scriptName, Encoding.UTF8);

                    var serializableTokens = JsonSerializer.Deserialize<List<SerializableToken>>(json);

                    // Falls du wieder echte Token-Objekte brauchst:
                    tokenStream = serializableTokens
                        .Select(st => new Token(
                            st.Type,
                            ConvertLexeme(st),   // (st.Lexeme) oder hier wieder in int/float/etc. umwandeln, wenn nötig
                            st.Line,
                            st.Column,
                            st.Text))
                        .ToList();
                } 

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

        public void CompileBinary(string fileName)
        {

            try
            {

                Scan(scriptName);

                Lexer lexer = new Lexer(sourceCode);

                List<Token> tokenStream = lexer.GetTokens();

                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                using (var bw = new BinaryWriter(fs))
                {
                    // Anzahl Tokens zuerst
                    bw.Write(tokenStream.Count);

                    foreach (var t in tokenStream)
                    {
                        bw.Write((int)t.Type);         // Enum als int
                        bw.Write(t.Lexeme?.ToString() ?? ""); // als String
                        bw.Write(t.Line);
                        bw.Write(t.Column);
                        //bw.Write(t.Text ?? "");
                    }
                }

            }
            catch (Exception exception)
            {
                throw new ScriptStackException("Fehler in '" + scriptName + "'.", exception);
            }
        }

        public void CompileJSON(string fileName)
        {

            try
            {

                Scan(scriptName);

                Lexer lexer = new Lexer(sourceCode);

                List<Token> tokenStream = lexer.GetTokens();

                var serializableTokens = tokenStream.Select(t => new SerializableToken
                {
                    Type = t.Type,
                    Lexeme = t.Lexeme?.ToString(),
                    Line = t.Line,
                    Column = t.Column,
                    Text = t.Text
                }).ToList();

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true // für schön formatiertes JSON
                };

                string json = JsonSerializer.Serialize(serializableTokens, options);

                // In Datei schreiben
                File.WriteAllText(fileName, json, Encoding.UTF8);

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
