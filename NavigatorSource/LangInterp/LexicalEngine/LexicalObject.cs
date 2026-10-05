using System;
using System.Text;
using XQuinn.LangInterp;

namespace XQuinn.LangInterp.LexicalEngine
{


    enum ControlFlow
    {
        /// <summary>
        /// Jumps to Increment, skips Append, skips following branches.
        /// </summary>
        Increment,
        /// <summary>
        /// Jumps to Append, skips following branches.
        /// </summary>
        Append,
        /// <summary>
        /// Releases control of the character so that it may reach following branches.
        /// </summary>
        Release
    }
    abstract class LexicalObject
    {
        protected CallLexer _lexer;
        protected bool WhitespaceEnd() => CallLexer.Termination(_lexer._curr_char) || ((_lexer._read_qualified_member || _lexer._arbitrary_reader._beganReadingMainMethodName || _lexer._read_arbitrary_legal_value) && _lexer._curr_char == CallLexer.MethodDeclr);
        public LexicalObject(CallLexer lexer)
        {
            _lexer = lexer;
        }
    }
}