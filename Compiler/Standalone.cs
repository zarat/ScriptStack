using System;
using System.Collections.Generic;
using ScriptStack;
using ScriptStack.Runtime;

namespace ScriptStack.Compiler
{
    /// <summary>
    /// Convenience helpers to use the lexer and parser without going through file-based <see cref="Script"/> loading.
    /// </summary>
    public static class Standalone
    {
        /// <summary>
        /// Tokenize a full source string.
        /// </summary>
        public static List<Token> Lex(string source, Lexer.DefaultRealType defaultReal = Lexer.DefaultRealType.Decimal)
            => Lexer.Tokenize(source, defaultReal);

        /// <summary>
        /// Parse (compile) a full source string into an <see cref="Executable"/> using the provided (or a new) <see cref="Manager"/>.
        /// This uses <see cref="Lexer"/> and <see cref="Parser"/> directly.
        /// </summary>
        public static Executable Parse(
            string source,
            Manager? manager = null,
            string scriptName = "<memory>",
            bool resolveIncludes = false,
            Lexer.DefaultRealType defaultReal = Lexer.DefaultRealType.Decimal)
        {
            manager ??= new Manager();

            // Create a context script (no implicit compilation), optionally expanding includes.
            var context = Script.CreateContext(manager, scriptName, Lexer.ToLines(source), resolveIncludes);

            var effectiveLines = new List<string>(context.SourceLines);
            var lexer = manager.LexerFactory(effectiveLines);
            lexer.DefaultReal = defaultReal;

            var tokens = lexer.GetTokens();

            var parser = new Parser(context, tokens)
            {
                DebugMode = manager.Debug
            };

            var executable = parser.Parse();

            if (manager.Optimize)
            {
                var optimizer = new Optimizer(executable)
                {
                    OptimizerInfo = false
                };
                optimizer.Optimize();
            }

            return executable;
        }

        /// <summary>
        /// Parse (compile) an existing token stream into an <see cref="Executable"/>.
        /// </summary>
        public static Executable ParseTokens(
            List<Token> tokenStream,
            Manager? manager = null,
            string scriptName = "<tokens>")
        {
            if (tokenStream == null)
                throw new ArgumentNullException(nameof(tokenStream));

            manager ??= new Manager();

            // Minimal context just to provide a manager/shared memory during parsing.
            var context = Script.CreateContext(manager, scriptName, new List<string> { "" }, resolveIncludes: false);

            var parser = new Parser(context, tokenStream)
            {
                DebugMode = manager.Debug
            };

            var executable = parser.Parse();

            if (manager.Optimize)
            {
                var optimizer = new Optimizer(executable)
                {
                    OptimizerInfo = false
                };
                optimizer.Optimize();
            }

            return executable;
        }

        /// <summary>
        /// Compile a source string by constructing an in-memory <see cref="Script"/>.
        /// (This uses the same compile routine as the regular file-based script constructor.)
        /// </summary>
        public static Script CompileToScript(
            string source,
            Manager? manager = null,
            string scriptName = "<memory>",
            bool resolveIncludes = false)
        {
            manager ??= new Manager();
            return new Script(manager, scriptName, Lexer.ToLines(source), resolveIncludes, compile: true);
        }
    }
}
