using System;
using XQuinn.Reflection;
using XQuinn.LangInterp.SyntaxTree;
using System.Text;

namespace XQuinn.Runtime.NavigatorEngine
{
    internal readonly struct MethodKey : IEquatable<MethodKey>
    {
        public readonly int MatchIndex;
        public readonly GenericKey GKey;
        internal MethodKey(int index, GenericKey key)
        {
            MatchIndex = index;
            GKey = key;
        }

        internal static MethodKey MethodQuery(MethodString mthdString)
        {
            string name = mthdString.Name;
            int split = name.IndexOf(':');
            int index = 0;
            if (split >= 0)
            {
                string indexed = name.Substring(split + 1); //stringSplit better here? prob doestn amtter
                index = int.Parse(indexed);
                name = name.Remove(split);
            }
            GenericKey key = new(name, mthdString.Generics.Count);
            return new(index, key);
        }

        bool IEquatable<MethodKey>.Equals(MethodKey obj)
        {
            return Equals(obj);
        }
        public override int GetHashCode()
        {
            int hash = 17;
            unchecked
            {
                hash = hash * 31 + MatchIndex.GetHashCode();
                hash = hash * 31 + GKey.GetHashCode();
            }
            return hash;
        }

        public override string ToString()
        {
            StringBuilder sb = new(GKey.Name);
            if (MatchIndex > 0)
                sb.Append($":{MatchIndex}");
            return GKey.Args == 0 ? sb.ToString() : GKey.Args == 1 ? sb.Append("<T>").ToString() : GKey.ArgsToString(sb);
        }
        public bool Equals(MethodKey overload)
        {
            return overload.MatchIndex == MatchIndex && overload.GKey == GKey;
        }

        public override bool Equals(object? obj)
        {
            return obj is MethodKey overload && Equals(overload);
        } //obj is Overload overload && ((IEquatable<Overload>)this).Equals(overload);

        public static bool operator ==(MethodKey left, MethodKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MethodKey left, MethodKey right)
        {
            return !(left == right);
        }
    }
}