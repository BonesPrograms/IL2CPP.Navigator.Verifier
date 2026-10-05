using System;
using System.Collections.Generic;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using XQuinn.Extensions;
using XQuinn.Runtime.NavigatorEngine;
using Type = Il2CppSystem.Type;

namespace XQuinn.LangInterp.SyntaxTree
{
    internal abstract class GenericString : MetadataString
    {
        public string StringID => _id;
        string _id;

        public IReadOnlyList<TypeString> Generics => _type_args == null ? Array.Empty<TypeString>() : _type_args;
        List<TypeString>? _type_args;

        protected GenericString(string name) : base(name)
        {
            _id = name;
        }

        protected static T New<T>(T genericString) where T : GenericString
        {
            if (HasTypeArgs(genericString.Name))
                genericString.UpdateGenericArgs();
            return genericString;
        }

        protected Il2CppReferenceArray<Type> ConvertGenericArguments()
        {
            if (Generics.Count == 0)
                throw new ArgumentException($"No generic parameters were provided to {GetType().Name} with name value {Name} ");

            // Generic inflation is an IL2CPP operation. Build the native-facing Type[] explicitly
            // instead of depending on Il2CppInterop's managed-array implicit conversion.
            Il2CppReferenceArray<Type> genericArgs = new(Generics.Count);
            for (int i = 0; i < genericArgs.Length; i++)
                genericArgs[i] = Generics[i].ToType();
            return genericArgs;
        }

        void UpdateGenericArgs()
        {
            StringBuilder sb = new();
            LexGenerics(sb);
            sb.Length = 0;
            GenericID(sb);
        }

        static bool HasTypeArgs(string nameOrValue)
        {
            bool genericStart = false;
            bool genericEnd = false;
            int halt = -1;
            for (int i = 0; i < nameOrValue.Length; i++)
            {
                if (nameOrValue[i] == '<')
                {
                    halt = i;
                    genericStart = true;
                    break;
                }
            }
            if (genericStart)
            {
                for (int i = nameOrValue.Length - 1; i > halt; i--)
                {
                    if (nameOrValue[i] == '>')
                    {
                        genericEnd = true;
                        break;
                    }
                }
            }
            return genericEnd || (genericStart ? throw new FormatException($"Invalid generic argument format. {nameOrValue}") : false);
        }

        void LexGenerics(StringBuilder sb)
        {
            GenericString currentGeneric = this;
            bool finishedReadingLeadName = false;
            int readingSubParams = 0;
            int lastRead = 0;
            for (int i = 0; i < _id.Length; i++)
            {
                char c = _id[i];
                if (c == CallLexer.Whitespace)
                    continue;
                if (c == CallLexer.GenericDeclr)
                {
                    if (finishedReadingLeadName)
                    {
                        readingSubParams++;
                        lastRead++;
                        currentGeneric = currentGeneric.NewArg(sb);
                    }
                    else
                    {
                        currentGeneric._arg = sb.ToString();
                        sb.Length = 0;
                        finishedReadingLeadName = true;
                    }
                }
                else if (c == CallLexer.GenericTerminate)
                {
                    if (lastRead > readingSubParams)
                        lastRead--;
                    if (sb.Length > 0)
                        currentGeneric.NewArg(sb);
                    if (currentGeneric != this)
                    {
                        if (currentGeneric.Generics.Count > 0)
                        {
                            currentGeneric.GenericID(sb);
                            sb.Length = 0;
                        }
                        TypeString currentArg = (TypeString)currentGeneric;
                        currentGeneric = currentArg._typeArgOf!;
                        if (readingSubParams > 0)
                            readingSubParams--;
                        continue;
                    }
                    break;
                }
                else if (c == CallLexer.ParamTerminate)
                {
                    if (sb.Length > 0)
                        currentGeneric.NewArg(sb);
                }
                else
                    sb.Append(c);
            }
            if (lastRead > 0)
                throw new LexicalException($"Nested generics not properly terminated. You can usually fix this by throwing an extra > at the end. Generic:", _id);
        }

        TypeString NewArg(StringBuilder sb)
        {
            string name = sb.ToString();
            sb.Length = 0;
            _type_args ??= new List<TypeString>();
            TypeString typeArg = new(name, this);
            _type_args.Add(typeArg);
            return typeArg;
        }

        public override string ToString() => _id;

        void GenericID(StringBuilder sb)
        {
            sb.Append(Name);
            sb.Append('<');
            sb.AppendMany(Generics, ",");
            sb.Append('>');
            _id = sb.ToString();
        }
    }
}
