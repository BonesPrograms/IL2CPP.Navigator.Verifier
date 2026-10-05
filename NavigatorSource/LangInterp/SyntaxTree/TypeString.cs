using XQuinn.Reflection;
using XQuinn.Runtime.NavigatorEngine;
using Type = Il2CppSystem.Type;

namespace XQuinn.LangInterp.SyntaxTree
{
    internal sealed class TypeString : GenericString
    {
        internal static readonly TypeString s_this = new("this");
        internal readonly GenericString? _typeArgOf;

        internal TypeString(string name, GenericString? typeArgOf = null) : base(name)
        {
            _typeArgOf = typeArgOf;
        }

        internal static TypeString New(string name, GenericString? typeArgOf = null)
        {
            return New<TypeString>(new(name, typeArgOf));
        }

        internal Type ToType()
        {
            GenericKey key = new(this);
            if (key.Args > 0)
            {
                if (RuntimeCache.s_reified_generic_types.TryGetValue(StringID, out Type? cached))
                    return cached;
                Type reified = FindType(key).MakeGenericType(ConvertGenericArguments());
                RuntimeCache.s_reified_generic_types[StringID] = reified;
                return reified;
            }
            return FindType(key);
        }

        static Type FindType(GenericKey key) => TypeRegister.GetTypeOrThrow(key);
    }
}
