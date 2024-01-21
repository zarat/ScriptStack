using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using ScriptStack.Runtime;

namespace ScriptStack.Compiler
{

    internal class ScannerPrototype : Scanner {

        #region Public Methods

        public List<string> Scan(string source)
        {

            try
            {

                List<string> lines = new List<string>();

                StreamReader streamReader = new StreamReader(source);

                while (!streamReader.EndOfStream)
                    lines.Add(streamReader.ReadLine());

                streamReader.Close();

                return lines; 

            }
            catch (Exception exception)
            {
                throw new ScriptStackException("Beim einlesen der Datei '" + source + "' ist ein Fehler aufgetreten.", exception);
            }

        }

        #endregion

    }

}
