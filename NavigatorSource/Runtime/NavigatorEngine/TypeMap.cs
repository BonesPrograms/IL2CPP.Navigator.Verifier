using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using FieldInfo = Il2CppSystem.Reflection.FieldInfo;
using MemberInfo = Il2CppSystem.Reflection.MemberInfo;
using MethodBase = Il2CppSystem.Reflection.MethodBase;
using PropertyInfo = Il2CppSystem.Reflection.PropertyInfo;
using Type = Il2CppSystem.Type;

namespace XQuinn.Runtime.NavigatorEngine
{
    internal static class TypeMap
    {
        static Type CompilerGeneratedAttributeType => Il2CppType.Of<Il2CppSystem.Runtime.CompilerServices.CompilerGeneratedAttribute>();

        static bool CompilerGenerated(MemberInfo member)
            => member.IsDefined(CompilerGeneratedAttributeType, false);

        internal static void MapType(
            Dictionary<MethodKey, MethodBase>? methods,
            Dictionary<string, FieldInfo>? fields,
            Type type,
            Dictionary<string, PropertyInfo>? props,
            bool includeConstructors = true)
        {
            if (methods != null)
            {
                methods.Clear();
                List<MethodBase> all = GetUnfilteredMethods(type, includeConstructors);
                FilterSupportedMethods(all);
                // Enumerable.Distinct in the original preserved the first-seen method-key
                // order. Keep that ordering explicitly; query output and overload key order
                // should not depend on HashSet enumeration details.
                HashSet<GenericKey> seen = new();
                List<GenericKey> keys = new();
                foreach (MethodBase method in all)
                {
                    GenericKey key = new(method);
                    if (seen.Add(key))
                        keys.Add(key);
                }
                foreach (GenericKey key in keys)
                    SortMethods(all, key, methods);
            }

            if (fields != null)
            {
                fields.Clear();
                var array = type.GetFields(XQuinn.Runtime.NavigatorCore.Flag);
                for (int i = array.Length - 1; i >= 0; i--)
                {
                    FieldInfo field = array[i];
                    if (!CompilerGenerated(field))
                        fields[field.Name] = field;
                }
            }

            if (props != null)
            {
                props.Clear();
                var array = type.GetProperties(XQuinn.Runtime.NavigatorCore.Flag);
                for (int i = array.Length - 1; i >= 0; i--)
                {
                    PropertyInfo prop = array[i];
                    if (!CompilerGenerated(prop) && prop.GetIndexParameters().Length == 0)
                        props[prop.Name] = prop;
                }
            }
        }

        internal static List<MethodBase> GetUnfilteredMethods(Type type, bool includeConstructors)
        {
            List<MethodBase> result = new();
            var methods = type.GetMethods(XQuinn.Runtime.NavigatorCore.Flag);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodBase method = methods[i];
                if (Types.IsSyntheticInjectedFinalize(method) ||
                    XQuinn.Runtime.NavigatorBridge.TypeRegister.IsSyntheticInjectedFinalize(method))
                {
                    // ClassInjector declares its own public Finalize on every injected class.
                    // The original static Types class instead inherited Object.Finalize, so
                    // substitute that inherited method rather than exposing the injector helper.
                    var objectMethods = Il2CppType.Of<Il2CppSystem.Object>().GetMethods(XQuinn.Runtime.NavigatorCore.Flag);
                    for (int j = 0; j < objectMethods.Length; j++)
                    {
                        MethodBase inherited = objectMethods[j];
                        if (inherited.Name.Equals("Finalize", StringComparison.Ordinal) && inherited.GetParameters().Length == 0)
                        {
                            result.Add(inherited);
                            break;
                        }
                    }
                    continue;
                }
                result.Add(method);
            }

            // The original Types helper was a static CLR class and therefore had no
            // constructors in its navigator method map. Its injected IL2CPP bridge needs
            // an implementation constructor, but that synthetic constructor is not part
            // of the public Navigator surface.
            if (includeConstructors && !Types.IsTypesType(type) &&
                !XQuinn.Runtime.NavigatorBridge.TypeRegister.IsTypeRegisterType(type))
            {
                var constructors = type.GetConstructors(XQuinn.Runtime.NavigatorCore.Flag);
                for (int i = 0; i < constructors.Length; i++)
                    result.Add(constructors[i]);
            }
            return result;
        }

        static void FilterSupportedMethods(List<MethodBase> methods)
        {
            for (int i = methods.Count - 1; i >= 0; i--)
            {
                MethodBase method = methods[i];
                if (Types.IsLogicalGeneric(method))
                    continue;
                if (CompilerGenerated(method) || !Reflector.SupportedMember(method, method.GetParameters()))
                    methods.RemoveAt(i);
            }
        }

        static void SortMethods(List<MethodBase> methods, GenericKey key, Dictionary<MethodKey, MethodBase> destination)
        {
            int matches = 0;
            for (int i = 0; i < methods.Count; i++)
            {
                MethodBase method = methods[i];
                GenericKey candidate = new(method);
                if (candidate == key)
                {
                    destination[new MethodKey(matches, candidate)] = method;
                    methods.RemoveAt(i--);
                    matches++;
                }
            }
        }
    }
}
