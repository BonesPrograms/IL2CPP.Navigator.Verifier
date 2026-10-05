using System;
using System.Collections.Generic;
using System.Text;
using Il2CppInterop.Runtime;
using XQuinn.Extensions;
using XQuinn.LangInterp;
using XQuinn.Runtime.NavigatorEngine;
using Type = Il2CppSystem.Type;

namespace XQuinn.Reflection
{
    /// <summary>
    /// Registry of IL2CPP runtime types. No CLR System.Type values are stored here.
    /// </summary>
    public static partial class TypeRegister
    {
        internal static readonly Dictionary<GenericKey, Type> s_registry = new();
        /// <summary>
        /// Duplicate-key entries retained by the most recent host bulk registration.
        /// Nested, compiler-generated and file-local types are quiet skips and are not included.
        /// </summary>
        public static List<(string key, Type type)> SkippedTypes = new();
        static readonly string[] s_illegal_keys = { "null", "default", "base", "this", bool.FalseString, bool.TrueString };
        static Type CompilerGeneratedAttributeType => Il2CppType.Of<Il2CppSystem.Runtime.CompilerServices.CompilerGeneratedAttribute>();


        static TypeRegister()
        {
            Types.RegisterInIl2Cpp();
            XQuinn.Runtime.NavigatorBridge.TypeRegister.RegisterInIl2Cpp();

            // These privileged Navigator helpers always own their simple-name keys before host,
            // game or Unity types are processed.
            CacheBuiltin("Types", Il2CppType.Of<Types>());
            CacheBuiltin("TypeRegister", Il2CppType.Of<XQuinn.Runtime.NavigatorBridge.TypeRegister>());

            // Preserve the managed registry's insertion order and display keys. Keys() and
            // Enumerate() expose Dictionary order, so this is observable behavior even though
            // GenericKey lookup itself is case-insensitive.
            CacheBuiltin("int", Il2CppType.Of<int>());
            CacheBuiltin("uint", Il2CppType.Of<uint>());
            CacheBuiltin("short", Il2CppType.Of<short>());
            CacheBuiltin("ushort", Il2CppType.Of<ushort>());
            CacheBuiltin("long", Il2CppType.Of<long>());
            CacheBuiltin("ulong", Il2CppType.Of<ulong>());
            CacheBuiltin("float", Il2CppType.Of<float>());
            CacheBuiltin("bool", Il2CppType.Of<bool>());
            TryCacheRuntimeType("Tuple", "System.ValueTuple");
            TryCacheRuntimeType("KVP", "System.Collections.Generic.KeyValuePair`2");

            CacheBuiltin("Object", Il2CppType.Of<Il2CppSystem.Object>());
            CacheBuiltin("String", Il2CppType.Of<string>());
            CacheBuiltin("SByte", Il2CppType.Of<sbyte>());
            CacheBuiltin("Byte", Il2CppType.Of<byte>());
            CacheBuiltin("Char", Il2CppType.Of<char>());
            CacheBuiltin("Double", Il2CppType.Of<double>());
            CacheBuiltin("Decimal", Il2CppType.Of<Il2CppSystem.Decimal>());
            CacheBuiltin("Enum", Il2CppType.Of<Il2CppSystem.Enum>());
            CacheBuiltin("BindingFlags", Il2CppType.Of<Il2CppSystem.Reflection.BindingFlags>());
            TryCacheRuntimeType("Nullable", "System.Nullable`1");
            TryCacheRuntimeType("Nullable", "System.Nullable");
            TryCacheRuntimeType("IDisposable", "System.IDisposable");
            // The managed registry remains authoritative. The cached TypeRegister runtime type
            // is a narrow injected facade for Navigator queries and duplicate inspection.
            TryCacheRuntimeType("Assembly", "System.Reflection.Assembly");
            TryCacheRuntimeType("Activator", "System.Activator");
            TryCacheRuntimeType("Environment", "System.Environment");
            TryCacheRuntimeType("AppDomain", "System.AppDomain");
            TryCacheRuntimeType("AppContext", "System.AppContext");
            TryCacheRuntimeType("RuntimeEnvironment", "System.Runtime.InteropServices.RuntimeEnvironment");
            TryCacheRuntimeType("RuntimeInformation", "System.Runtime.InteropServices.RuntimeInformation");
            CacheBuiltin("Array", Il2CppType.Of<Il2CppSystem.Array>());
            TryCacheRuntimeType("List", "System.Collections.Generic.List`1");
            TryCacheRuntimeType("IList", "System.Collections.Generic.IList`1");
            TryCacheRuntimeType("IList", "System.Collections.IList");
            TryCacheRuntimeType("Enumerable", "System.Linq.Enumerable");
            TryCacheRuntimeType("IEnumerable", "System.Collections.IEnumerable");
            TryCacheRuntimeType("IEnumerable", "System.Collections.Generic.IEnumerable`1");
            TryCacheRuntimeType("Dictionary", "System.Collections.Generic.Dictionary`2");
            TryCacheRuntimeType("IDictionary", "System.Collections.Generic.IDictionary`2");
            TryCacheRuntimeType("IDictionary", "System.Collections.IDictionary");
            TryCacheRuntimeType("HashSet", "System.Collections.Generic.HashSet`1");
            TryCacheRuntimeType("ICollection", "System.Collections.Generic.ICollection`1");
            TryCacheRuntimeType("ICollection", "System.Collections.ICollection");
            TryCacheRuntimeType("Collection", "System.Collections.ObjectModel.Collection`1");

            // The managed implementation deliberately cached the concrete runtime class of
            // reflected Type objects under the public key "Type". Derive that class from a real
            // IL2CPP Type object instead of ever falling back to the abstract System.Type proxy.
            CacheBuiltin("Type", Il2CppRuntime.TypeOf(Il2CppType.Of<int>()));
        }

        static void CacheBuiltin(string key, Type type)
        {
            s_registry[new GenericKey(key, type)] = type;
        }

        internal static Type? FindRuntimeType(string fullName)
        {
            Type? type = Type.GetType(fullName, false, true);
            if (type != null)
                return type;

            var assemblies = Il2CppSystem.AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName, false, true);
                if (type != null)
                    return type;
            }
            return null;
        }

        static void TryCacheRuntimeType(string key, string fullName)
        {
            Type? type = FindRuntimeType(fullName);
            if (type != null)
                CacheBuiltin(key, type);
        }

        public static IEnumerable<string> Keys()
        {
            foreach (GenericKey key in s_registry.Keys)
                yield return key.ToString();
        }

        public static ICollection<Type> Values => s_registry.Values;

        public static IEnumerable<KeyValuePair<string, Type>> Enumerate()
        {
            foreach (KeyValuePair<GenericKey, Type> obj in s_registry)
                yield return new KeyValuePair<string, Type>(obj.Key.ToString(), obj.Value);
        }

        public static bool Contains(string name) => s_registry.ContainsKey(GenericKey.TypeQuery(name));

        public static bool TryGetType(string name, out Type? cachedType)
            => s_registry.TryGetValue(GenericKey.TypeQuery(name), out cachedType);

        public static Type? GetTypeCached(string name)
            => TryGetType(name, out Type? cachedType) ? cachedType : null;

        public static Type GetTypeOrThrow(string name) => GetTypeOrThrow(GenericKey.TypeQuery(name));

        internal static Type GetTypeOrThrow(GenericKey key)
        {
            if (s_registry.TryGetValue(key, out Type? cachedType))
                return cachedType;
            throw new ArgumentException($"Could not find cached type with key {key.Name} and generic arg count {key.Args}.");
        }

        public static bool CacheType(Type type, string key)
        {
            if (type.IsNested)
                return false;
            if (type.IsDefined(CompilerGeneratedAttributeType, true) || IsFileType(type))
                return false;
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
                throw new NotSupportedException($"Generic type {type} with key {key} cannot be cached. Only generic type definitions and nongeneric types can be cached.");

            ThrowIfBadKey(key);
            GenericKey trueKey = new(key, type);
            if (CheckDuplicateOrCached(type, trueKey))
                return false;
            s_registry[trueKey] = type;
            return true;
        }

        public static bool CacheType<T>(string key) => CacheType(Il2CppType.Of<T>(), key);
        public static bool CacheType<T>(bool fullname) => CacheType(Il2CppType.Of<T>(), fullname);
        public static bool CacheType(Type type, bool fullname) => CacheType(type, GetCompatibleName(type, fullname));

        public static void CacheTypes(IEnumerable<Type> types, bool fullname)
        {
            foreach (Type type in types)
                CacheType(type, fullname);
        }

        public static void CacheTypes(IEnumerable<Type> types, bool fullname, string append)
        {
            foreach (Type type in types)
                CacheType(type, append + GetCompatibleName(type, fullname));
        }

        public static void CacheTypes(IEnumerable<Type> types, Func<Type, string?> keyProvider)
        {
            foreach (Type type in types)
            {
                string? key = keyProvider(type);
                if (key != null)
                    CacheType(type, key);
            }
        }

        /// <summary>
        /// Caches every eligible type while retaining simple-name collisions. CacheType continues
        /// to quietly reject nested types, so this does not add nested-type support.
        /// </summary>
        public static List<(string key, Type type)> CacheTypesSkipDuplicates(IEnumerable<Type> types, bool fullname)
        {
            List<(string key, Type type)> skipped = new();
            foreach (Type type in types)
            {
                try
                {
                    CacheType(type, fullname);
                }
                catch (DuplicateKeyException ex)
                {
                    skipped.Add((ex.GKey.ToString(), type));
                }
            }
            return skipped;
        }

        public static string GetCompatibleName(Type type, bool fullname)
        {
            string name = fullname
                ? type.FullName ?? throw new ArgumentNullException(nameof(fullname), $"Type {type} returned null for fullname.")
                : type.Name;
            if (type.IsGenericTypeDefinition)
                return SnipGenericName(name).ToString();
            if (type.IsNested && fullname)
                return name.Replace('+', '.');
            return name;
        }

        static StringBuilder SnipGenericName(string name)
        {
            StringBuilder sb = new();
            foreach (char original in name)
            {
                char c = original == '+' ? '.' : original;
                if (c == '`')
                    break;
                sb.Append(c);
            }
            return sb;
        }

        static bool IsFileType(Type type)
            => !type.IsPublic && !type.IsNested && type.Name.StartsWith("<", StringComparison.Ordinal);

        internal static void ThrowIfBadKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Empty key.");
            if (char.IsDigit(key[0]))
                throw new ArgumentException($"Keys cannot begin with a digit. Bad Key: {key}");
            if (key[0] == CallLexer.MemberAccessOrDecimal)
                throw new ArgumentException($"Keys cannot begin with a period. Bad Key {key}");
            foreach (string illegal in s_illegal_keys)
                if (illegal.EqualsCaseless(key))
                    throw new ArgumentException($"This key is restricted and cannot be registered. Bad Key {key}.");

            bool accessor = false;
            for (int i = 0; i < key.Length; i++)
            {
                char value = key[i];
                if (CallLexer.Illegal(value))
                {
                    if (!accessor && value == CallLexer.MemberAccessOrDecimal)
                        accessor = true;
                    else
                        throw new ArgumentException($"Keys can only consist of digits, letters, underscores, or single periods between names. Bad Key {key}.");
                }
                else if (accessor)
                {
                    if (!CallLexer.ValidIdentifierFirstChar(value))
                        throw new ArgumentException($"Member access must be followed by an underscore or a letter for namespaces. Bad Key: {key}");
                    accessor = false;
                }
            }
        }

        static bool CheckDuplicateOrCached(Type type, GenericKey key)
        {
            if (!s_registry.TryGetValue(key, out Type? cachedType))
                return false;
            if (Il2CppRuntime.SameType(type, cachedType))
                return true;
            throw new DuplicateKeyException(cachedType, type, key);
        }
    }

    internal sealed class DuplicateKeyException : Exception
    {
        internal readonly GenericKey GKey;

        internal DuplicateKeyException(Type type, Type insert, GenericKey name)
            : this(type, insert, name.ToString())
        {
            GKey = name;
        }

        internal DuplicateKeyException(Type type, Type insert, string name)
            : base($"Conflict detected when trying to cache {insert.FullName} with key {name}. Key has already been used for type {type.FullName}. Key names are not case sensitive.")
        {
            GKey = GenericKey.TypeQuery(name);
        }
    }
}
