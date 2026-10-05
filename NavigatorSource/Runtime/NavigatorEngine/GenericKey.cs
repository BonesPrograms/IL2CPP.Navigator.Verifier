using MethodBase = Il2CppSystem.Reflection.MethodBase;
using ConstructorInfo = Il2CppSystem.Reflection.ConstructorInfo;
using Type = Il2CppSystem.Type;
using System;
using System.Text;
using XQuinn.LangInterp.SyntaxTree;
using XQuinn.Extensions;
using XQuinn.LangInterp;

namespace XQuinn.Runtime.NavigatorEngine
{
    internal readonly struct GenericKey : IEquatable<GenericKey>
    {
        public readonly string Name;
        public readonly int Args;
        public static GenericKey TypeQuery(string typename)
        {
            CountGenericArgs(ref typename, out int count);
            GenericKey key = new(typename, count);
            return key;
        }

        static void CountGenericArgs(ref string typename, out int count)
        {
            int nest = 0;
            int declrindex = -1;
            bool readFirstGeneric = false;
            count = 0;
            for (int i = 0; i < typename.Length; i++)
            {
                char c = typename[i];
                if (c == CallLexer.GenericDeclr)
                {
                    if (readFirstGeneric)
                        nest++;
                    else
                    {
                        declrindex = i;
                        count++;
                    }
                    readFirstGeneric = true;
                }
                if (c == CallLexer.GenericTerminate)
                    nest--;
                if (nest == 0 && c == CallLexer.ParamTerminate)
                    if (!readFirstGeneric)
                        throw new LexicalException("Invalid type name", typename);
                    else
                        count++;
            }
            if (declrindex != -1)
                typename = typename.Remove(declrindex);
        }


        public GenericKey(MethodBase method) : this(method is ConstructorInfo ? "new" : method.Name,
            Types.IsLogicalGeneric(method) ? 1 : method.IsGenericMethodDefinition ? method.GetGenericArguments().Length : 0)
        {
        }

        public GenericKey(string key, int args = 0)
        {
            Name = key;
            Args = args;
        }

        public GenericKey(string key, Type t) : this(key, t.IsGenericTypeDefinition ? t.GetGenericArguments().Length : 0)
        {
        }


        internal GenericKey(TypeString str) : this(str.Name, str.Generics.Count)
        {
        }

        bool IEquatable<GenericKey>.Equals(GenericKey other)
        {
            return Equals(other);
        }

        public override bool Equals(object? obj)
        {
            return obj is GenericKey key && Equals(key);
        }

        public bool Equals(GenericKey key)
        {
            return Args == key.Args && Name.EqualsCaseless(key.Name);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            unchecked
            {
                hash = hash * 23 + StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
                hash = hash * 23 + Args.GetHashCode();
            }
            return hash;
        }

        public static bool operator ==(GenericKey left, GenericKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GenericKey left, GenericKey right)
        {
            return !(left == right);
        }

        internal string ArgsToString(StringBuilder sb)
        {
            sb.Append("<T1, ");
            for (int i = 1; i < Args; i++)
            {
                sb.Append($"T{i + 1}");
                if (For.SmartDelimiter(Args, i))
                    sb.Append(", ");
            }
            sb.Append('>');
            return sb.ToString();
        }

        public override string ToString()
        {
            if (Args > 0)
            {
                if (Args == 1)
                    return $"{Name}<T>";
                return ArgsToString(new(Name));

            }
            return Name;
        }

    }
}
