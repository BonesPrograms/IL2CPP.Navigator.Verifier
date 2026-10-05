using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using XQuinn.Extensions;
using XQuinn.Reflection;
using BindingFlags = Il2CppSystem.Reflection.BindingFlags;
using FieldAttributes = Il2CppSystem.Reflection.FieldAttributes;
using FieldInfo = Il2CppSystem.Reflection.FieldInfo;
using MemberInfo = Il2CppSystem.Reflection.MemberInfo;
using MethodBase = Il2CppSystem.Reflection.MethodBase;
using MethodInfo = Il2CppSystem.Reflection.MethodInfo;
using PropertyInfo = Il2CppSystem.Reflection.PropertyInfo;
using Type = Il2CppSystem.Type;

namespace XQuinn.Runtime.NavigatorEngine
{
    /// <summary>
    /// XQuinn's runtime utility type. This class is injected into IL2CPP so it can be resolved
    /// and invoked like other IL2CPP types. IL2CPP-visible helpers are instance methods because
    /// Il2CppInterop 1.4.x class injection does not expose injected static methods to IL2CPP reflection.
    /// </summary>
    public sealed class Types : Il2CppSystem.Object
    {
        static bool s_registered;
        static Types? s_runtime_instance;
        static nint s_runtime_handle;

        public Types(IntPtr ptr) : base(ptr) { }

        public Types() : base(ClassInjector.DerivedConstructorPointer<Types>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }

        [HideFromIl2Cpp]
        internal static void RegisterInIl2Cpp()
        {
            if (!s_registered)
            {
                if (!ClassInjector.IsTypeRegisteredInIl2Cpp<Types>())
                    ClassInjector.RegisterTypeInIl2Cpp<Types>();
                s_registered = true;
            }

            // Keep one actual injected IL2CPP instance alive. ClassInjector exposes instance
            // methods, not static methods, so Navigator can use this object as the receiver when
            // the cached Types runtime type is addressed directly.
            if (s_runtime_instance == null)
                s_runtime_instance = new Types();
            if (s_runtime_handle == 0)
                s_runtime_handle = Il2CppInterop.Runtime.IL2CPP.il2cpp_gchandle_new(s_runtime_instance.Pointer, false);
        }

        [HideFromIl2Cpp]
        internal static Types RuntimeInstance()
        {
            RegisterInIl2Cpp();
            return s_runtime_instance!;
        }

        // IL2CPP-visible instance API. These preserve the original Types surface.
        // ClassInjector exposes instance methods only, so XQuinn treats this injected singleton
        // as the logical static receiver when navigating the Types type.
        public Il2CppSystem.Array Props<T>(string? contains = null, BindingFlags search = XQuinn.Runtime.NavigatorCore.Flag)
            => ToIl2CppStrings(ReadProps(Il2CppInterop.Runtime.Il2CppType.Of<T>(), contains, search));

        public Il2CppSystem.Array Methods<T>(string? contains = null, BindingFlags search = XQuinn.Runtime.NavigatorCore.Flag)
            => ToIl2CppStrings(ReadMethods(Il2CppInterop.Runtime.Il2CppType.Of<T>(), contains, search));

        public Il2CppSystem.Array Fields<T>(string? contains = null, BindingFlags search = XQuinn.Runtime.NavigatorCore.Flag)
            => ToIl2CppStrings(ReadFields(Il2CppInterop.Runtime.Il2CppType.Of<T>(), contains, search));

        public Il2CppSystem.Array Props(Type t, string? contains = null, BindingFlags search = XQuinn.Runtime.NavigatorCore.Flag)
            => ToIl2CppStrings(ReadProps(t, contains, search));

        public Il2CppSystem.Array Fields(Type t, string? contains = null, BindingFlags search = XQuinn.Runtime.NavigatorCore.Flag)
            => ToIl2CppStrings(ReadFields(t, contains, search));

        public Il2CppSystem.Array Methods(Type t, string? contains = null, BindingFlags search = XQuinn.Runtime.NavigatorCore.Flag)
            => ToIl2CppStrings(ReadMethods(t, contains, search));

        public void FlushStaticCache(bool ambiguousMatches = true, bool accessedMembers = true, bool reifiedGenerics = true)
            => XQuinn.Runtime.NavigatorCore.FlushStaticCache(ambiguousMatches, accessedMembers, reifiedGenerics);

        public Type Of<T>() => Il2CppInterop.Runtime.Il2CppType.Of<T>();

        public Type Of(string name) => TypeRegister.GetTypeOrThrow(name);

        public T Num<T>(T obj = default) where T : struct => obj;

        public T Enum<T>(T obj) where T : Enum => obj;

        public string String(string txt) => txt;

        // Il2CppInterop 1.4.x cannot inject T[] as a generic method parameter. The visible
        // bridge accepts System.Array; Parser recreates the original params-T[] behavior before
        // this method is called, so Navigator users retain Array<T>(params T[]) semantics.
        public Il2CppSystem.Array Array<T>(Il2CppSystem.Array arr)
        {
            // Original Types.Array<T>(params T[]) dereferenced arr.Length, so an explicit null
            // params array throws rather than silently returning null. Preserve that contract.
            if (arr == null)
                throw new NullReferenceException();
            return arr.Length == 0
                ? Il2CppRuntime.EmptyArray(Il2CppInterop.Runtime.Il2CppType.Of<T>())
                : arr;
        }

        public Il2CppSystem.Array Array<T>(int i)
            => i == 0
                ? Il2CppRuntime.EmptyArray(Il2CppInterop.Runtime.Il2CppType.Of<T>())
                : Il2CppRuntime.CreateArray(Il2CppInterop.Runtime.Il2CppType.Of<T>(), i);

        public bool LateCache(string assemblyName, string targetTypeName, string keyForCaching)
            => LateCache(Il2CppSystem.Reflection.Assembly.Load(assemblyName), targetTypeName, keyForCaching);

        public bool LateCache(Il2CppSystem.Reflection.Assembly assembly, string targetTypeName, string keyForCaching)
        {
            Type target = assembly.GetType(targetTypeName, false, true)
                ?? throw new ArgumentException($"No type found in assembly {assembly.FullName} named {targetTypeName}. Requires full name.");
            return TypeRegister.CacheType(target, keyForCaching);
        }

        [HideFromIl2Cpp]
        internal static bool IsLogicalGeneric(MethodBase method)
        {
            if (!IsTypesMember(method) || !method.IsGenericMethod)
                return false;
            string name = method.Name;
            return name.EqualsCaseless("Of") || name.EqualsCaseless("Num") ||
                name.EqualsCaseless("Enum") || name.EqualsCaseless("Array") ||
                name.EqualsCaseless("Fields") || name.EqualsCaseless("Methods") ||
                name.EqualsCaseless("Props");
        }

        [HideFromIl2Cpp]
        static bool IsSizedArrayHelper(MethodBase method)
        {
            // Both bridge overloads have concrete parameter types. Unlike Num/Enum, this
            // does not inspect an injected generic-parameter Type or inflate a method.
            var parameters = method.GetParameters();
            return parameters.Length == 1 && Il2CppRuntime.SameType(parameters[0].ParameterType,
                Il2CppInterop.Runtime.Il2CppType.Of<int>());
        }

        [HideFromIl2Cpp]
        internal static object? InvokeLogicalGeneric(MethodBase method,
            XQuinn.LangInterp.SyntaxTree.MethodString syntax, Parser parser)
        {
            if (syntax.Generics.Count != 1)
                throw new ArgumentException($"Types.{method.Name}<T> requires one type argument.");
            Type argument = syntax.Generics[0].ToType();
            string name = method.Name;
            ValidateLogicalGenericArgument(name, argument);

            if (name.EqualsCaseless("Of"))
            {
                parser.ParseParameters(System.Array.Empty<Parser.Parameter>(), syntax);
                return argument;
            }

            if (name.EqualsCaseless("Num") || name.EqualsCaseless("Enum"))
            {
                bool optional = name.EqualsCaseless("Num");
                var schema = new[] { new Parser.Parameter(argument, optional,
                    defaultFactory: optional ? () => Il2CppRuntime.CreateDefault(argument) : null) };
                object?[] values = parser.ParseParameters(schema, syntax);
                return Il2CppRuntime.AsIl2CppObject(values[0], argument);
            }

            if (name.EqualsCaseless("Array"))
            {
                bool sized = IsSizedArrayHelper(method);
                var schema = sized
                    ? new[] { new Parser.Parameter(Il2CppInterop.Runtime.Il2CppType.Of<int>()) }
                    : new[] { new Parser.Parameter(argument.MakeArrayType(), elementType: argument) };
                object?[] values = parser.ParseParameters(schema, syntax);
                if (sized)
                {
                    int length = Il2CppRuntime.ReadInt32(values[0], "i");
                    return length == 0 ? Il2CppRuntime.EmptyArray(argument)
                        : Il2CppRuntime.CreateArray(argument, length);
                }
                if (values[0] == null)
                    throw new NullReferenceException();
                Il2CppSystem.Array array = Il2CppRuntime.RequireProxy<Il2CppSystem.Array>(values[0], "arr");
                if (!Il2CppRuntime.IsAssignableFrom(argument.MakeArrayType(), Il2CppRuntime.TypeOf(array)))
                    throw new ArgumentException($"Array cannot be passed to Types.Array<{argument}>.", "arr");
                return array.Length == 0 ? Il2CppRuntime.EmptyArray(argument) : array;
            }

            var querySchema = new[]
            {
                new Parser.Parameter(Il2CppInterop.Runtime.Il2CppType.Of<string>(), true),
                new Parser.Parameter(Il2CppInterop.Runtime.Il2CppType.Of<BindingFlags>(), true,
                    XQuinn.Runtime.NavigatorCore.Flag)
            };
            object?[] query = parser.ParseParameters(querySchema, syntax);
            string? contains = Il2CppRuntime.ReadNullableString(query[0], "contains");
            BindingFlags flags = Il2CppRuntime.ReadBindingFlags(query[1], "search");
            return ToIl2CppStrings(name.EqualsCaseless("Props") ? ReadProps(argument, contains, flags)
                : name.EqualsCaseless("Fields") ? ReadFields(argument, contains, flags)
                : ReadMethods(argument, contains, flags));
        }

        [HideFromIl2Cpp]
        internal static bool TryPrintLogicalGeneric(System.Text.StringBuilder sb, MethodInfo method, bool fullname)
        {
            if (!IsLogicalGeneric(method))
                return false;
            string name = method.Name;
            bool query = name.EqualsCaseless("Props") || name.EqualsCaseless("Fields") || name.EqualsCaseless("Methods");
            sb.Append("static ");
            if (query)
                sb.Append(fullname ? "System.Collections.Generic.IEnumerable<System.String>" : "IEnumerable<String>");
            else if (name.EqualsCaseless("Of"))
                sb.Append(fullname ? "System.Type" : "Type");
            else
                sb.Append(name.EqualsCaseless("Array") ? "T[]" : "T");
            sb.Append(' ');
            MetadataPrinter.GenericTypeToString(sb, method.DeclaringType, fullname);
            sb.Append("::").Append(name).Append("<T>(");
            if (query)
            {
                sb.Append(fullname ? "System.String" : "String").Append(" contains = null, ");
                sb.Append(fullname ? "System.Reflection.BindingFlags" : "BindingFlags");
                sb.Append(" search = ").Append(XQuinn.Runtime.NavigatorCore.Flag);
            }
            else if (name.EqualsCaseless("Num"))
                sb.Append("T obj = default");
            else if (name.EqualsCaseless("Enum"))
                sb.Append("T obj");
            else if (name.EqualsCaseless("Array"))
                sb.Append(IsSizedArrayHelper(method) ? (fullname ? "System.Int32 i" : "Int32 i") : "params T[] arr");
            sb.Append(')');
            return true;
        }

        /// <summary>
        /// Executes the logical static Types API when Navigator itself is the caller.
        /// The public class still lives in IL2CPP, but Navigator does not round-trip these calls
        /// through ClassInjector's IL2CPP-to-managed trampoline. That trampoline cannot propagate
        /// managed exceptions back to the managed reflection caller, while the original Types API
        /// did. Keeping this dispatch here preserves the original result/exception behavior.
        /// </summary>
        [HideFromIl2Cpp]
        internal static bool TryInvokeLogical(MethodBase method, object?[] args, out object? result)
        {
            result = null;
            if (method is not MethodInfo info || !IsTypesMember(info) || info.IsGenericMethod)
                return false;

            string name = info.Name;

            if (name.EqualsCaseless("Props") || name.EqualsCaseless("Methods") || name.EqualsCaseless("Fields"))
            {
                Type targetType = Il2CppRuntime.RequireProxy<Type>(args[0], "t");
                const int offset = 1;

                string? contains = Il2CppRuntime.ReadNullableString(args[offset], "contains");
                BindingFlags search = Il2CppRuntime.ReadBindingFlags(args[offset + 1], "search");
                IEnumerable<string> values = name.EqualsCaseless("Props")
                    ? ReadProps(targetType, contains, search)
                    : name.EqualsCaseless("Methods")
                        ? ReadMethods(targetType, contains, search)
                        : ReadFields(targetType, contains, search);
                result = ToIl2CppStrings(values);
                return true;
            }

            if (name.EqualsCaseless("Of"))
            {
                result = TypeRegister.GetTypeOrThrow(Il2CppRuntime.ReadString(args[0], "name"));
                return true;
            }

            if (name.EqualsCaseless("String"))
            {
                string? text = Il2CppRuntime.ReadNullableString(args[0], "txt");
                result = Il2CppRuntime.AsIl2CppObject(text, Il2CppInterop.Runtime.Il2CppType.Of<string>());
                return true;
            }

            if (name.EqualsCaseless("LateCache"))
            {
                var parameters = info.GetParameters();
                bool cached;
                if (parameters.Length == 3 &&
                    Il2CppRuntime.SameType(parameters[0].ParameterType, Il2CppInterop.Runtime.Il2CppType.Of<string>()))
                {
                    string assemblyName = Il2CppRuntime.ReadString(args[0], "assemblyName");
                    string targetTypeName = Il2CppRuntime.ReadString(args[1], "targetTypeName");
                    string keyForCaching = Il2CppRuntime.ReadString(args[2], "keyForCaching");
                    Il2CppSystem.Reflection.Assembly assembly = Il2CppSystem.Reflection.Assembly.Load(assemblyName);
                    cached = LateCacheLogical(assembly, targetTypeName, keyForCaching);
                }
                else
                {
                    Il2CppSystem.Reflection.Assembly assembly =
                        Il2CppRuntime.RequireProxy<Il2CppSystem.Reflection.Assembly>(args[0], "assembly");
                    string targetTypeName = Il2CppRuntime.ReadString(args[1], "targetTypeName");
                    string keyForCaching = Il2CppRuntime.ReadString(args[2], "keyForCaching");
                    cached = LateCacheLogical(assembly, targetTypeName, keyForCaching);
                }

                result = Il2CppRuntime.AsIl2CppObject(cached, Il2CppInterop.Runtime.Il2CppType.Of<bool>());
                return true;
            }

            if (name.EqualsCaseless("FlushStaticCache"))
            {
                bool ambiguousMatches = Il2CppRuntime.ReadBoolean(args[0], "ambiguousMatches");
                bool accessedMembers = Il2CppRuntime.ReadBoolean(args[1], "accessedMembers");
                bool reifiedGenerics = Il2CppRuntime.ReadBoolean(args[2], "reifiedGenerics");
                XQuinn.Runtime.NavigatorCore.FlushStaticCache(ambiguousMatches, accessedMembers, reifiedGenerics);
                result = null;
                return true;
            }

            return false;
        }

        [HideFromIl2Cpp]
        static bool LateCacheLogical(Il2CppSystem.Reflection.Assembly assembly, string targetTypeName, string keyForCaching)
        {
            Type target = assembly.GetType(targetTypeName, false, true)
                ?? throw new ArgumentException($"No type found in assembly {assembly.FullName} named {targetTypeName}. Requires full name.");
            return TypeRegister.CacheType(target, keyForCaching);
        }

        [HideFromIl2Cpp]
        static void ValidateLogicalGenericArgument(string name, Type argument)
        {
            if (name.EqualsCaseless("Num"))
            {
                if (!argument.IsValueType || Il2CppRuntime.NullableUnderlyingType(argument) != null)
                    throw new ArgumentException($"Generic argument {argument} does not satisfy the struct constraint for Types.Num<T>.");
                return;
            }

            if (name.EqualsCaseless("Enum"))
            {
                Type enumType = Il2CppInterop.Runtime.Il2CppType.Of<Il2CppSystem.Enum>();
                if (!Il2CppRuntime.SameType(argument, enumType) && !argument.IsEnum)
                    throw new ArgumentException($"Generic argument {argument} does not satisfy the Enum constraint for Types.Enum<T>.");
            }
        }

        [HideFromIl2Cpp]
        internal static bool IsTypesType(Type? type)
        {
            // ReflectionPrinter is also public independently of Navigator. Inspecting an
            // ordinary native type must not require the Types bridge to be registered first.
            IntPtr registeredClass = Il2CppInterop.Runtime.Il2CppClassPointerStore<Types>.NativeClassPtr;
            return type != null && registeredClass != IntPtr.Zero &&
                Il2CppRuntime.ClassOf(type) == registeredClass;
        }

        [HideFromIl2Cpp]
        internal static bool IsTypesMember(MemberInfo member)
            => IsTypesType(member.DeclaringType);

        [HideFromIl2Cpp]
        internal static bool IsSyntheticInjectedFinalize(MemberInfo member)
        {
            if (member is not MethodBase method || !IsTypesMember(method) ||
                !method.Name.Equals("Finalize", StringComparison.Ordinal))
                return false;
            return method.GetParameters().Length == 0;
        }

        [HideFromIl2Cpp]
        internal static bool IsLogicalStatic(MemberInfo member)
            => (IsTypesMember(member) && !IsSyntheticInjectedFinalize(member)) ||
               (XQuinn.Runtime.NavigatorBridge.TypeRegister.IsTypeRegisterMember(member) &&
                !XQuinn.Runtime.NavigatorBridge.TypeRegister.IsSyntheticInjectedFinalize(member));

        [HideFromIl2Cpp]
        internal static bool TryAppendLogicalReturnType(MethodInfo method, System.Text.StringBuilder sb, bool fullname)
        {
            if (!IsTypesMember(method))
                return false;

            if (method.Name.EqualsCaseless("Props") || method.Name.EqualsCaseless("Methods") || method.Name.EqualsCaseless("Fields"))
            {
                // The injected bridge returns an IL2CPP array so ClassInjector can expose it,
                // but the public Navigator API remains IEnumerable<string> exactly as before.
                sb.Append(fullname
                    ? "System.Collections.Generic.IEnumerable<System.String>"
                    : "IEnumerable<String>");
                return true;
            }

            return false;
        }

        [HideFromIl2Cpp]
        internal static bool TryGetLogicalDefault(MethodBase method, int parameterIndex, Type parameterType, out object? value)
        {
            value = null;
            if (!IsTypesMember(method))
                return false;

            if (method.Name.EqualsCaseless("Props") || method.Name.EqualsCaseless("Methods") || method.Name.EqualsCaseless("Fields"))
            {
                const int containsIndex = 1;
                const int flagsIndex = 2;
                if (parameterIndex == containsIndex)
                {
                    value = null;
                    return true;
                }
                if (parameterIndex == flagsIndex)
                {
                    value = XQuinn.Runtime.NavigatorCore.Flag;
                    return true;
                }
                return false;
            }

            if (method.Name.EqualsCaseless("FlushStaticCache") && parameterIndex >= 0 && parameterIndex < 3)
            {
                value = true;
                return true;
            }

            return false;
        }

        [HideFromIl2Cpp]
        internal static IEnumerable<string> ReadProps(Type type, string? contains = null, BindingFlags search = XQuinn.Runtime.NavigatorCore.Flag)
        {
            Dictionary<string, PropertyInfo> props = new();
            TypeMap.MapType(null, null, type, props, false);
            return ReadMembers(props, contains, search, type);
        }

        [HideFromIl2Cpp]
        internal static IEnumerable<string> ReadFields(Type type, string? contains = null, BindingFlags search = XQuinn.Runtime.NavigatorCore.Flag)
        {
            Dictionary<string, FieldInfo> fields = new();
            TypeMap.MapType(null, fields, type, null, false);
            return ReadMembers(fields, contains, search, type);
        }

        [HideFromIl2Cpp]
        internal static IEnumerable<string> ReadMethods(Type type, string? contains = null, BindingFlags search = XQuinn.Runtime.NavigatorCore.Flag)
        {
            Dictionary<MethodKey, MethodBase> methods = new();
            TypeMap.MapType(methods, null, type, null, contains?.EqualsCaseless("new") ?? true);
            return ReadMembers(methods, contains, search, type);
        }

        [HideFromIl2Cpp]
        internal static IEnumerable<string> ReadMembers<K, V>(IEnumerable<KeyValuePair<K, V>> members, string? key, BindingFlags flags, Type? reflectedType = null)
            where K : notnull where V : MemberInfo
        {
            bool contained = false;
            foreach (KeyValuePair<K, V> member in members)
            {
                if (!SearchModifiers.ProcessSearch(member.Value, flags, reflectedType))
                    continue;
                if (key != null && !member.Key.ToString()!.ContainsCaseless(key))
                    continue;
                contained = true;
                yield return $"[Key: {member.Key} :: {ReflectionPrinter.Print(member.Value, false)}]";
            }
            if (key != null && !contained)
                yield return $"No {typeof(V).Name} found with name containing {key} with search option {flags}.";
        }

        [HideFromIl2Cpp]
        static Il2CppSystem.Array ToIl2CppStrings(IEnumerable<string> source)
        {
            List<object?> values = new();
            foreach (string value in source)
                values.Add(value);
            return Il2CppRuntime.CreateArray(Il2CppInterop.Runtime.Il2CppType.Of<string>(), values);
        }

        readonly struct SearchModifiers
        {
            readonly bool _static;
            readonly bool _public;
            readonly bool _inherited;

            SearchModifiers(MethodBase method, Type? reflectedType)
            {
                _static = method.IsStatic || IsLogicalStatic(method);
                _public = method.IsPublic;
                _inherited = Inherited(method, reflectedType);
            }

            SearchModifiers(FieldInfo field, Type? reflectedType)
            {
                _static = field.IsStatic;
                _public = (field.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
                _inherited = Inherited(field, reflectedType);
            }

            public static bool ProcessSearch(MemberInfo info, BindingFlags flags, Type? reflectedType = null)
            {
                if (info is MethodBase method)
                    return new SearchModifiers(method, reflectedType).Process(flags);
                if (info is FieldInfo field)
                    return new SearchModifiers(field, reflectedType).Process(flags);
                return PropertySearch((PropertyInfo)info, flags, reflectedType);
            }

            bool Process(BindingFlags flags)
            {
                if (_inherited && Has(flags, BindingFlags.DeclaredOnly)) return false;
                if (_public ? !Has(flags, BindingFlags.Public) : !Has(flags, BindingFlags.NonPublic)) return false;
                if (_static ? !Has(flags, BindingFlags.Static) : !Has(flags, BindingFlags.Instance)) return false;
                if (_static && _inherited && !Has(flags, BindingFlags.FlattenHierarchy)) return false;
                return true;
            }

            static bool PropertySearch(PropertyInfo prop, BindingFlags flags, Type? reflectedType)
            {
                // Match the original SearchModifiers implementation: query filtering asks only
                // for public accessors here. ReflectionPrinter separately uses non-public accessors.
                MethodInfo? getter = prop.GetGetMethod();
                if (getter != null && new SearchModifiers(getter, reflectedType).Process(flags)) return true;
                MethodInfo? setter = prop.GetSetMethod(false);
                return setter != null && new SearchModifiers(setter, reflectedType).Process(flags);
            }

            static bool Inherited(MemberInfo info, Type? reflectedType)
            {
                // CLR MemberInfo.ReflectedType is the type through which an inherited member was
                // obtained. Carry the mapped type explicitly so logical replacements (notably the
                // injected Types finalizer) preserve that same inheritance/DeclaredOnly behavior.
                Type? reflected = reflectedType ?? info.ReflectedType;
                return info.DeclaringType != null &&
                       !Il2CppRuntime.SameType(reflected, info.DeclaringType);
            }

            static bool Has(BindingFlags value, BindingFlags flag) => (value & flag) == flag;
        }
    }
}
