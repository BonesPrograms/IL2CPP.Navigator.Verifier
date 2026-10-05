using System.Text;
using XQuinn.LangInterp.SyntaxTree;
using System;
using XQuinn.Extensions;
using XQuinn.LangInterp.LexicalEngine;
using System.Diagnostics.CodeAnalysis;

namespace XQuinn.LangInterp
{



    internal sealed class CallLexer
    {

        internal const char ValidNonAlphaNumeric = '_';
        internal const char MethodDeclr = '(';
        internal const char MethodTerminate = ')';
        internal const char ParamTerminate = ',';
        internal const char MemberAccessOrDecimal = '.';
        internal const char StringDeclr = '"';
        internal const char NegSign = '-';
        internal const char EscSeq = '\\';
        internal const char CharDeclr = '\'';
        internal const char EnumOR = '|';
        internal const char NoEscDeclr = '@';
        internal const char GenericDeclr = '<';
        internal const char GenericTerminate = '>';
        internal static Whitespace Whitespace => Whitespace._whitespace;
        internal readonly StringBuilder _writer = new();
        internal readonly ValueReader _value_reader;
        internal readonly ArbitraryReader _arbitrary_reader;
        internal MethodString? _main;
        internal MethodString? _curr_method;
        internal TypeString? _declr_type;
        internal TypeString? _implicit_this;
        internal char _curr_char;
        internal bool _start = true;

        //Primary reading rulesets - determine how to lex incoming data based on context
        internal bool _read_char;
        internal bool _read_arbitrary_legal_value; //anything that isnt a digit, string or char but also hasnt yet been determined as a method field or enumOR
        internal bool _read_qualified_member; //something that has a member access operator and is not a digit
        internal bool _read_digit;
        internal bool _read_string;
        internal bool _read_enumOR; //OR-less enums are read as arbitrary values. | activates EnumOR read
        internal bool _read_generic_args;

        //These are supporting flags for rulesets, some rulesets have specific rules for specific characters, or need to be read around declaration characters
        internal bool _skipping;

        //These are for getting context on parameter values. Once a method begins or a parameter/method terminates, one of these is true, and we wait until we receive a character that gives us context on what will be read next.
        //Once we receive context, a Reading flag is set to true related to that specific context, and these flags are set to false, to prevent context getting reset in the middle of a read.
        internal bool _method_params_began;
        internal bool _terminated;

        //This is for additional awareness on how deeply nested a subparameter read is
        internal int _reading_sub_params;
        internal int _last_nested_read_count;
        Read ReadState
        {
            get
            {
                if (_start)
                    return Read.Start;
                if (_read_generic_args)
                    return Read.ReadGeneric;
                if (_read_enumOR)
                    return Read.ReadEnumOR;
                if (_read_string)
                    return Read.ReadString;
                if (_read_char)
                    return Read.ReadChar;
                if (_read_digit)
                    return Read.ReadDigit;
                if (_read_arbitrary_legal_value)
                    return Read.ReadArbitrary;
                if (_read_qualified_member)
                    return Read.ReadQualified;
                if (_curr_char == Whitespace)
                    return Read.Whitespace;
                if (_terminated || _method_params_began)
                    return Read.Context;
                return Read.Releasing;
            }
        }   

        internal CallLexer()
        {
            _value_reader = new(this);
            _arbitrary_reader = new(this);
        }

        internal MethodString MethodTemplate(string invocation, TypeString declaringType, TypeString? implicitAccess)
        {
            Clear();
            int? breakpoint = Breakpoint(invocation);
            _declr_type = declaringType;
            _implicit_this = implicitAccess;
            StringBuffer(invocation, breakpoint);
            FatalLexicalError(invocation);
            MethodString method = _main!;
            Clear();
            return method;
        }



        void StringBuffer(string invocation, int? breakpoint)
        {
            int i = 0;
            while (i < invocation.Length)
            {
                if (i == breakpoint)
                    break;
                _curr_char = invocation[i];
                ControlFlow flow = ReadState switch
                {
                    Read.Start => _arbitrary_reader.ReadMainMethod(ref i, invocation),
                    Read.ReadGeneric => _arbitrary_reader.ReadGeneric(invocation),
                    Read.ReadEnumOR => _value_reader.ReadEnumOR(invocation),
                    Read.ReadDigit => _value_reader.ReadNum(ref i, invocation),
                    Read.ReadString => _value_reader.ReadString(ref i, invocation),
                    Read.ReadChar => _value_reader.ReadChar(ref i, invocation),
                    Read.ReadArbitrary => _arbitrary_reader.ReadArbitrary(ref i, invocation),
                    Read.ReadQualified => _arbitrary_reader.ReadQualifiedMember(ref i, invocation),
                    Read.Context => GetContext(),
                    Read.Whitespace => ControlFlow.Increment,
                    Read.Releasing => ControlFlow.Release,
                };
                if (flow == ControlFlow.Append)
                    goto Append;
                if (flow == ControlFlow.Increment)
                    goto Increment;
                if (_last_nested_read_count > _reading_sub_params)
                {
                    _curr_method = _curr_method!._subParamOf;
                    _last_nested_read_count--;
                }
                if (_curr_char == MethodTerminate)
                {
                    if (_reading_sub_params > 0)
                        _reading_sub_params--;
                }
                if (Termination(_curr_char))
                {
                    if (_writer.Length != 0)
                    {
                        _value_reader.ReadParam();
                        _read_arbitrary_legal_value = false;
                        _terminated = true;
                    }
                    goto Increment;
                }
                ValidIdentifier(_curr_char, invocation);
            Append:
                _writer.Append(_curr_char);
            Increment:
                i++;
            }
        }

        // if (!_read_arbitrary_legal_value && !_read_char && !_read_digit && !_read_string && !_read_qualified_member && !_start && !_read_enumOR && !_read_generic_args)
        ControlFlow GetContext() //helps us figure out whats about to be read 
        {
            if (_curr_char == CharDeclr)
                _read_char = true;
            else if (_curr_char == StringDeclr || _curr_char == NoEscDeclr)
            {
                if (_curr_char == NoEscDeclr) //there can be an invalid sequence here but it wont throw until we get to ValueString
                {
                    _value_reader._noEscape = true;
                    _value_reader._readNoEscDeclr = true;
                }
                _read_string = true;
            }
            else if (_curr_char == NegSign || _curr_char == MemberAccessOrDecimal || char.IsDigit(_curr_char))
            {
                _read_digit = true;
                if (_curr_char == MemberAccessOrDecimal)
                    _value_reader._readFloat = true;
            }
            else if (ValidIdentifierFirstChar(_curr_char))
                _read_arbitrary_legal_value = true;
            if (_read_digit || _read_string || _read_arbitrary_legal_value || _read_char)
            {
                _terminated = false;
                _method_params_began = false;
                return ControlFlow.Append;
            }
            return ControlFlow.Release;
        }


        void FatalLexicalError(string invocation)
        {
            if (_last_nested_read_count > 0)
                throw new LexicalException("Nested method parameters not properly terminated. You can usually throw another ending parenthesis at the end of your invocation to fix this. ", invocation, _writer);
            if (_read_generic_args) //if you dont terminate a generic with . or ( then this will throw. genericlex checks later to make sure theyre propelry closed with >
                throw new LexicalException("Invalid generic arguments.", invocation, _writer);
            if (_read_char)
                throw new LexicalException("Chars require a closing apostrophe character.", invocation, _writer);
            if (_read_digit)
                throw new LexicalException("Digit parameter not terminated.", invocation, _writer);
            if (_read_string)
                throw new LexicalException("Strings require a closing quotation character.", invocation, _writer);
            if (_read_arbitrary_legal_value)
                throw new LexicalException("Parameter or method not terminated.", invocation, _writer);
            if (_read_qualified_member)
                throw new LexicalException("Member access requires a terminator after the member's name; either a ( leading parenthesis for method names, or a , comma for fields.", invocation, _writer);
            if (_start)
                throw new LexicalException("Method name was unable to be read due to missing ( leading parenthesis.", invocation, _writer);
        }

        internal void ValidIdentifier(char value, string invocation)
        {
            if (value != ':' && Illegal(value))
                throw new LexicalException("Detected illegal character in identifier.", invocation, value, _writer);

        }
        internal static bool Illegal(char val) => val != ValidNonAlphaNumeric && !char.IsDigit(val) && !char.IsLetter(val);
        internal static bool Termination(char value) => value == ParamTerminate || value == MethodTerminate;
        internal static bool ValidIdentifierFirstChar(char value) => value == ValidNonAlphaNumeric || char.IsLetter(value);

        void Clear() //This allows the Lexer to recover if an exception is thrown during analysis
        {             //There can also be some leftover values after a lex so state must be reset
            _main = null;
            _declr_type = null;
            _implicit_this = null;
            _curr_char = default;
            _start = true;
            _skipping = false;
            _curr_method = null;
            _read_digit = false;
            _read_string = false;
            _read_generic_args = false;
            _skipping = false;
            _read_qualified_member = false;
            _read_char = false;
            _read_enumOR = false;
            _read_arbitrary_legal_value = false;
            _method_params_began = false;
            _terminated = false;
            _reading_sub_params = 0;
            _last_nested_read_count = 0;
            _writer.Length = 0;
            _value_reader.Clear();
            _arbitrary_reader.Clear();
        }
        int? Breakpoint(string invocation)
        {
            bool breaking = false;
            int? breakpoint = null;
            for (int i = invocation.Length - 1; i >= 0; i--)
            {
                char val = invocation[i];
                if (val == Whitespace)
                    continue;
                if (val == ';')
                    breaking = true;
                else if (breaking)
                {
                    if (val == MethodTerminate)
                    {
                        breakpoint = i + 1;
                        break;
                    }
                    else
                        throw new LexicalException("Invalid method termination. ", invocation, val, _writer);
                }
                else if (val == MethodTerminate)
                    break;
                else
                    throw new LexicalException("Detected trailing input after method termination or lack of method termination. ", invocation, val, _writer);
            }
            return breakpoint;
        }

        //testing with strings reveals that breakpoint needs to be pushed up by +1
        //this is because having breakpoint exactly on method terminate fucks up string closure
        // the _stringEnding branch in ReadString is not reached
        //this phenomena can be tested with class<string>.method("hi"); simply if you remove the +1 code
        //but you will get the same failed string closure exception if you do method("hi" 
        //so the cause is revealed
        //when you terminate on ), you are effectively shrinking the call down to
        //class<string>.method("hi"
        //when we reach the index after ", the loop ends entirely and we leave its scope
        //but you werent supposed to, it needs to loop *one* more time to finalize the string read
        //string read processing doesnt actually happen on the final "
        //it happens on the next loop thereafter
        //so essentially you break out of the loop and the stringread method doesnt get a chance to invoke and that causes the bug


        ///more private notes
        /// if you have extra empty parameters ie Method(22,32,,,)
        /// the system generally ignores it
        /// we ignore it to prevent a different bug
        /// Method(22, 23, OtherMethod(), 15)
        /// ) is considered a valid parameter terminator, but so is ,
        /// so the system would, normally, try to append the 'text' between ) and ,
        /// this would not only append an empty string but it will also lead to a parameter count mismatch
        /// since , and ) both need to be able to terminate parameters, i have no way to prevent empty parameters
        /// without also recreating that original bug
        /// (we solve the bug by checking if our StringBuilder is empty before appending, which would also be the logical
        /// point to throw an 'empty parameter' exception, so doesnt really work)
        enum Read
        {
            Releasing,
            Start,
            ReadGeneric,
            ReadEnumOR,
            ReadString,
            ReadDigit,
            ReadChar,
            ReadArbitrary,
            ReadQualified,
            Whitespace,
            Context
        }


    }

    internal sealed class Whitespace
    {

        internal static readonly Whitespace _whitespace = new();
        public override bool Equals(object? obj)
        {
            return obj is char c && char.IsWhiteSpace(c);
        }

        public static bool operator ==(char left, Whitespace right) => char.IsWhiteSpace(left);

        public static bool operator !=(char left, Whitespace right) => !(left == right);

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        Whitespace()
        {

        }
    }

    internal sealed class LexicalException : Exception
    {
        internal LexicalException(string msg, string invocation, char next, StringBuilder sb, int i) : base(msg + $"Input: {invocation} Bad character: {next} Current string value: {sb} Index: {i + 1}")
        {

        }

        internal LexicalException(string msg, string invocation, StringBuilder sb) : base(msg + $"Input: {invocation} Current string value: {sb}")
        {

        }

        internal LexicalException(string msg, string invocation) : base(msg + $"Input: {invocation}")
        {

        }

        internal LexicalException(string msg, string invocation, char val, StringBuilder sb) : base(msg + $"Input: {invocation} Bad Character:{val} Current String Value: {sb}")
        {

        }
    }



}
