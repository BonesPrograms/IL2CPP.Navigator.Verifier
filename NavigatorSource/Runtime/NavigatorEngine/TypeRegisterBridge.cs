using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using MemberInfo = Il2CppSystem.Reflection.MemberInfo;
using MethodBase = Il2CppSystem.Reflection.MethodBase;
using Type = Il2CppSystem.Type;

namespace XQuinn.Runtime.NavigatorBridge
{
    /// <summary>
    /// IL2CPP-visible logical-static view of the managed TypeRegister. Tuple storage remains in
    /// managed memory while Navigator can query duplicate entries through this facade.
    /// </summary>
    public sealed class TypeRegister : Il2CppSystem.Object
    {
        static bool s_registered;
        static TypeRegister? s_runtimeInstance;
        static nint s_runtimeHandle;

        public TypeRegister(IntPtr ptr) : base(ptr) { }

        public TypeRegister() : base(ClassInjector.DerivedConstructorPointer<TypeRegister>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }

        public int CachedTypeCount() => XQuinn.Reflection.TypeRegister.Values.Count;

        public int SkippedTypeCount() => XQuinn.Reflection.TypeRegister.SkippedTypes.Count;

        public string SkippedTypeKey(int index) => Entry(index).key;

        public Type SkippedType(int index) => Entry(index).type;

        public string SkippedTypeName(int index)
        {
            Type type = Entry(index).type;
            return type.FullName ?? type.Name;
        }

        public Type Get(string key) => XQuinn.Reflection.TypeRegister.GetTypeOrThrow(key);

        public bool Contains(string key) => XQuinn.Reflection.TypeRegister.Contains(key);

        [HideFromIl2Cpp]
        static (string key, Type type) Entry(int index)
        {
            if ((uint)index >= (uint)XQuinn.Reflection.TypeRegister.SkippedTypes.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return XQuinn.Reflection.TypeRegister.SkippedTypes[index];
        }

        [HideFromIl2Cpp]
        internal static void RegisterInIl2Cpp()
        {
            if (!s_registered)
            {
                if (!ClassInjector.IsTypeRegisteredInIl2Cpp<TypeRegister>())
                    ClassInjector.RegisterTypeInIl2Cpp<TypeRegister>();
                s_registered = true;
            }

            if (s_runtimeInstance == null)
                s_runtimeInstance = new TypeRegister();
            if (s_runtimeHandle == 0)
                s_runtimeHandle = IL2CPP.il2cpp_gchandle_new(s_runtimeInstance.Pointer, false);
        }

        [HideFromIl2Cpp]
        internal static TypeRegister RuntimeInstance()
        {
            RegisterInIl2Cpp();
            return s_runtimeInstance!;
        }

        [HideFromIl2Cpp]
        internal static bool IsTypeRegisterType(Type? type)
        {
            IntPtr registeredClass = Il2CppClassPointerStore<TypeRegister>.NativeClassPtr;
            return type != null && registeredClass != IntPtr.Zero &&
                XQuinn.Runtime.NavigatorEngine.Il2CppRuntime.ClassOf(type) == registeredClass;
        }

        [HideFromIl2Cpp]
        internal static bool IsTypeRegisterMember(MemberInfo member)
            => IsTypeRegisterType(member.DeclaringType);

        [HideFromIl2Cpp]
        internal static bool IsSyntheticInjectedFinalize(MemberInfo member)
        {
            if (member is not MethodBase method || !IsTypeRegisterMember(method) ||
                !method.Name.Equals("Finalize", StringComparison.Ordinal))
                return false;
            return method.GetParameters().Length == 0;
        }
    }
}