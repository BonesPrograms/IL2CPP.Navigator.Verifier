using XQuinn.LangInterp;
using XQuinn.LangInterp.SyntaxTree;
using XQuinn.Extensions;
using System;

using static XQuinn.LangInterp.CallLexer;
using XQuinn.Runtime.NavigatorEngine;


namespace XQuinn.LangInterp.LexicalEngine
{
    sealed class ArbitraryReader : LexicalObject
    {
        internal bool _beganReadingMainMethodName;
        bool _memberAccessing;
        bool _genericParamTerminate;
        bool _readFirstGeneric;
        int _generic_sub_params;
        int _last_sub_count;
        public void Clear()
        {
            _memberAccessing = false;
            _beganReadingMainMethodName = false;
            _genericParamTerminate = false;
            _readFirstGeneric = false;
            _memberAccessing = false;
            _generic_sub_params = 0;
            _last_sub_count = 0;
        }






        public ArbitraryReader(CallLexer lexer) : base(lexer)
        {

        }
        void CrossWhitespace(ref int i, string invocation, Func<bool>? pred)
        {
            while (i < invocation.Length)
            {
                i++;
                _lexer._curr_char = invocation[i];
                if (WhitespaceEnd() || _lexer._curr_char == GenericDeclr || (pred != null && pred()))
                    break;
                else if (_lexer._curr_char != CallLexer.Whitespace)
                    throw new LexicalException("Detected trailing input after whitespace.", invocation, _lexer._curr_char, _lexer._writer, i);
            }
        }

        internal ControlFlow ReadArbitrary(ref int i, string invocation)
        {
            if (_lexer._curr_char == CallLexer.Whitespace)
                CrossWhitespace(ref i, invocation, delegate{return _lexer._curr_char == MemberAccessOrDecimal || _lexer._curr_char == EnumOR;});
            if (_lexer._curr_char == EnumOR)
            {
                ActivateReadEnumOR(invocation);
                return ControlFlow.Append;
            }
            else if (_lexer._curr_char == GenericDeclr)
                return ActivateReadGeneric(invocation);
            else if (_lexer._curr_char == MemberAccessOrDecimal)
            {
                _memberAccessing = true;
                _lexer._read_qualified_member = true;
                _lexer._read_arbitrary_legal_value = false;
                return ControlFlow.Append;
            }
            else if (_lexer._curr_char == MethodDeclr)
            {
                _lexer._read_arbitrary_legal_value = false;
                return ReadMethod(invocation);
            }
            else if (!Termination(_lexer._curr_char))
                _lexer.ValidIdentifier(_lexer._curr_char, invocation);
            return ControlFlow.Release;
        }

        internal ControlFlow ReadQualifiedMember(ref int i, string invocation) //once an arbitrary is determined to be an identifier, it is read with stricter rules
        {
            if (_lexer._curr_char == CallLexer.Whitespace)
                CrossWhitespace(ref i, invocation, delegate{return (_memberAccessing && _lexer._curr_char != CallLexer.Whitespace) || _lexer._curr_char == MemberAccessOrDecimal;});
            if (_lexer._curr_char == GenericDeclr)
                return ActivateReadGeneric(invocation);
            if (_lexer._curr_char == MemberAccessOrDecimal)
            {
                if (_memberAccessing)
                    throw new LexicalException("invalid name", invocation, _lexer._writer);
                _memberAccessing = true;
                return ControlFlow.Append;
            }
            if (Termination(_lexer._curr_char))
                return ReadField(invocation);
            if (_lexer._curr_char == MethodDeclr)
                return ReadMethod(invocation);
            if (_memberAccessing)
            {
                _memberAccessing = false;
                ValidIdentifierFirstCharOrThrow(_lexer._curr_char, invocation);
            }
            else
                _lexer.ValidIdentifier(_lexer._curr_char, invocation);
            return ControlFlow.Append;
        }


        internal ControlFlow ReadGeneric(string invocation)
        {

            if (_lexer._curr_char == GenericTerminate) //generics can only be Lexer.terminated by these, so if its not, the lex will throw at the end
            {
                if (_last_sub_count > _generic_sub_params)
                    _last_sub_count--;
                if (_last_sub_count == 0)
                {
                    _readFirstGeneric = false;
                    _lexer._skipping = false;
                    _lexer._read_generic_args = false;
                    _memberAccessing = false;
                }
                if (_generic_sub_params > 0)
                    _generic_sub_params--;
            }
            else if (_lexer._curr_char == MemberAccessOrDecimal)
            {
                if (_memberAccessing)
                    throw new LexicalException("invalid name", invocation, _lexer._writer);
                _memberAccessing = true;
            }
            else if (_lexer._curr_char == ParamTerminate) //wnt to add member access checks 2 this
            {
                if (_genericParamTerminate)
                    throw new LexicalException("Invalid generic arguments.", invocation, _lexer._curr_char, _lexer._writer);
                _genericParamTerminate = true;
                _lexer._skipping = false;
            }
            else if (_lexer._curr_char == CallLexer.Whitespace)
            {
                _lexer._skipping = true; //unlike most other reads, this one allows whitespace, so instead of Lexer.skipping to Termination and ending the read
                return ControlFlow.Increment; //we must instead increment one by one and keep the read active until we reach a proper termination
            }
            else if (_lexer._curr_char != GenericDeclr && _lexer._curr_char != GenericTerminate)
            {
                if (_lexer._skipping && !_readFirstGeneric && !_genericParamTerminate && !_memberAccessing) //this specifically checks if the last value was a char
                    throw new LexicalException("Invalid generic arguments.", invocation, _lexer._curr_char, _lexer._writer); //in essence this means youre putting something like "String"
                if (_memberAccessing || (_genericParamTerminate && !_readFirstGeneric))
                    ValidIdentifierFirstCharOrThrow(_lexer._curr_char, invocation);
                else
                    _lexer.ValidIdentifier(_lexer._curr_char, invocation);
                _genericParamTerminate = false;
                _readFirstGeneric = false;
                _lexer._skipping = false;
                _memberAccessing = false;
            }
            else if (_lexer._curr_char == GenericDeclr)
            {
                if (_readFirstGeneric)
                    throw new LexicalException("Invalid generic arguments.", invocation, _lexer._curr_char, _lexer._writer);
                _readFirstGeneric = true;
                _generic_sub_params++;
                _last_sub_count++;
            }
            else if (_lexer._curr_char == GenericTerminate && _readFirstGeneric)
                throw new LexicalException("Invalid generic arguments.", invocation, _lexer._curr_char, _lexer._writer);
            return ControlFlow.Append;

        }

        //all errors stop the program, but these errors mean you really messed up 
        internal ControlFlow ReadMainMethod(ref int i, string invocation)
        {
            if (_beganReadingMainMethodName)
            {
                if (_lexer._read_generic_args)
                    return ReadGeneric(invocation); //readgeneric is so special isnt it, this is the only read called by another read, but it works
                if (_lexer._curr_char == CallLexer.Whitespace)
                    CrossWhitespace(ref i, invocation, null);
                if (_lexer._curr_char == MethodDeclr)
                    return ReadMain();
                if (_lexer._curr_char == GenericDeclr)
                    ActivateReadGeneric(invocation);
                else
                    _lexer.ValidIdentifier(_lexer._curr_char, invocation);
                return ControlFlow.Append;
            }
            else if (_lexer._curr_char == CallLexer.Whitespace)
                return ControlFlow.Increment;
            if (ValidIdentifierFirstCharOrThrow(_lexer._curr_char, invocation))
                _beganReadingMainMethodName = true;
            return ControlFlow.Append;
        }


        ControlFlow ReadMain()
        {
            MethodString method = MethodString.New(_lexer._writer.ToString(), null, _lexer._declr_type!);// ?? throw new InvalidOperationException());
            _lexer._writer.Length = 0;
            _lexer._curr_method = method;
            _lexer._main = method;
            _lexer._method_params_began = true;
            _lexer._start = false;
            _beganReadingMainMethodName = false;
            return ControlFlow.Increment;

        }


        ControlFlow ReadField(string invocation)
        {
            if (_memberAccessing) //i expect ethod( class<int>. , ) readqualifiedmember to be true here but it is not -ah yes thats cause , causes it to be read as a field
                throw new LexicalException("Invalid member access operator, no member was accesed.", invocation, _lexer._writer);
            _lexer._terminated = true;
            string typename = ResolveMemberAccess(out string fieldname)!;
            TypeString type = ImplicitDeclaredOrNew(typename);
            FieldString field = new(fieldname, type);
            _lexer._writer.Length = 0;
            _lexer._curr_method!.AddParameter(field);
            _lexer._read_qualified_member = false;
            return ControlFlow.Release;
        }


        ControlFlow ReadMethod(string invocation)
        {
            if (_memberAccessing) //i expect ethod( class<int>. , ) readqualifiedmember to be true here but it is not -ah yes thats cause , causes it to be read as a field
                throw new LexicalException("Invalid member access operator, no member was accesed.", invocation, _lexer._writer);
            string? typename = ResolveMemberAccess(out string methodname);
            TypeString type = ImplicitDeclaredOrNew(typename);
            MethodString method = MethodString.New(methodname, _lexer._curr_method, type);
            _lexer._writer.Length = 0;
            _lexer._curr_method!.AddParameter(method);
            _lexer._curr_method = method;
            _lexer._read_qualified_member = false;
            _lexer._method_params_began = true;
            _lexer._reading_sub_params++;
            _lexer._last_nested_read_count++;
            return ControlFlow.Increment;
        }

        ControlFlow ActivateReadEnumOR(string invocation)
        {
            if (_lexer._read_generic_args)
                throw new LexicalException("Invalid EnumOR or generic", invocation, _lexer._curr_char, _lexer._writer);
            _lexer._read_arbitrary_legal_value = false;
            _lexer._read_enumOR = true;
            _lexer._value_reader._readORVal = true;
            _lexer._value_reader._readORDelimit = true;
            return ControlFlow.Append;

        }

        ControlFlow ActivateReadGeneric(string invocation)
        {
            if (_lexer._read_generic_args)
                throw new LexicalException("Invalid generic arguments.", invocation, _lexer._writer);
            _lexer._read_generic_args = true;
            _readFirstGeneric = true;
            return ControlFlow.Append;
        }

        TypeString ImplicitDeclaredOrNew(string? name)
        {
            if (name == null)
                return _lexer._implicit_this ?? throw new InvalidOperationException("Cannot use implicit this, no implicit this has been provided.");
            if (name.EqualsCaseless(_lexer._implicit_this?.StringID)) // == this or == _key
                return _lexer._implicit_this!;
            if (name.EqualsCaseless(_lexer._declr_type!.StringID))
                return _lexer._declr_type;
            return TypeString.New(name);
        }
        string? ResolveMemberAccess(out string member) //returns typename, outputs the accessed member
        {
            string qualifiedMember = _lexer._writer.ToString();
            _lexer._writer.Length = 0;
            return MiniLexer.ResolveMemberAccessOrLiteral(qualifiedMember, out member, out _);
        }


        internal bool ValidIdentifierFirstCharOrThrow(char next, string invocation)
        {
            const string error = "Identifier names must Lexer.start with a letter or an underscore.";
            if (!ValidIdentifierFirstChar(next))
                throw new LexicalException(error, invocation, next, _lexer._writer);
            return true;
        }

    }
}