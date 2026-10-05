using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using XQuinn.Extensions;
using XQuinn.LangInterp.SyntaxTree;
using XQuinn.Reflection;
using ConstructorInfo = Il2CppSystem.Reflection.ConstructorInfo;
using FieldInfo = Il2CppSystem.Reflection.FieldInfo;
using MemberInfo = Il2CppSystem.Reflection.MemberInfo;
using MethodBase = Il2CppSystem.Reflection.MethodBase;
using MethodInfo = Il2CppSystem.Reflection.MethodInfo;
using ParameterInfo = Il2CppSystem.Reflection.ParameterInfo;
using PropertyInfo = Il2CppSystem.Reflection.PropertyInfo;
using Type = Il2CppSystem.Type;

namespace XQuinn.Runtime.NavigatorEngine
{
    internal sealed class Reflector : ExpandedCoreObject
    {
        TypeString? _implicit_this => _core._implicit_this;
        Dictionary<MethodKey, MethodBase> _methods => _core._methods;

        public Reflector(XQuinn.Runtime.NavigatorCore navig) : base(navig) { }

        public static bool SupportedMember(MethodBase method, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<ParameterInfo> parameters)
        {
            if (method is MethodInfo mthd && mthd.ReturnType.IsByRef)
                return false;
            if (method is ConstructorInfo && method.IsStatic)
                return false;
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo p = parameters[i];
                if (p.IsOut || p.IsIn || p.ParameterType.IsByRef)
                    return false;
            }
            return true;
        }

        internal Type FindReference(IMemberString member, out Il2CppObjectBase? instance)
        {
            if (_fields.TryGetValue(member.DeclaringType.StringID, out FieldInfo? field))
            {
                Il2CppSystem.Object? target = field.IsStatic ? null : Il2CppRuntime.AsReference(_object);
                Il2CppSystem.Object? value = field.GetValue(target);
                instance = value ?? throw new ArgumentException($"Field {field} in type {_loadedType} returned null and it's member methods and fields cannot be invoked.");
                return Il2CppRuntime.TypeOf(instance);
            }

            if (_props.TryGetValue(member.DeclaringType.StringID, out PropertyInfo? prop))
            {
                Il2CppSystem.Object? value = prop.GetValue(Il2CppRuntime.AsReference(_object), null);
                instance = value ?? throw new ArgumentException($"Property {member.DeclaringType.Name} in type {_loadedType} returned null and it's member methods and fields cannot be invoked.");
                return Il2CppRuntime.TypeOf(instance);
            }

            if (_variables.TryGetValue(member.DeclaringType.StringID, out VariableBinding? variable))
            {
                instance = variable.Object;
                return Il2CppRuntime.TypeOf(variable.Object);
            }

            instance = null;
            return FindType(member.DeclaringType, false);
        }

        internal Type FindType(TypeString typename, bool staticLoadOrCasting)
        {
            if (!staticLoadOrCasting)
            {
                if (typename.StringID.EqualsCaseless("this"))
                    return _instanceType == null
                        ? throw new InvalidOperationException("Cannot pass this, instance is null.")
                        : _loadedType!;
                if (typename.StringID.EqualsCaseless(_implicit_this?.StringID))
                    return _loadedType!;
            }

            if (typename.StringID.EqualsCaseless("base"))
            {
                if (_instanceType == null)
                    throw new InvalidOperationException("Cannot get instance base, instance is null.");
                return _instanceType.BaseType ?? throw new ArgumentException("Base type of instance is null.");
            }
            return typename.ToType();
        }

        internal MethodBase FindMethod(MethodString method)
        {
            _methods.TryGetValue(MethodKey.MethodQuery(method), out MethodBase? methodBase);
            if (methodBase == null)
                throw new MissingMethodException($"No method named {method.Name}  with generic arg count {method.Generics.Count} found in {_loadedType}'s method dictionary. It may have been removed due to having a ref return type or in/out/ref parameters.");
            if (!Types.IsLogicalGeneric(methodBase) && methodBase.IsGenericMethodDefinition && methodBase is MethodInfo info)
                methodBase = method.ConvertToGeneric(info);
            return methodBase;
        }

        internal static void NotNullable(Type type, object? value)
        {
            if (value == null && !(type.IsClass || Il2CppRuntime.NullableUnderlyingType(type) != null))
                throw new ArgumentException($"Cannot assign null to type {type}.");
        }

        internal readonly struct Assignment
        {
            public readonly MemberInfo Member;
            public readonly Type ObjectType;

            public Assignment(MemberInfo member)
            {
                if (member is PropertyInfo prop)
                    ObjectType = prop.PropertyType;
                else if (member is FieldInfo field)
                    ObjectType = field.FieldType;
                else
                    throw new NotSupportedException();
                Member = member;
            }

            public void SetValue(Il2CppObjectBase? instance, object? value)
            {
                NotNullable(ObjectType, value);
                Il2CppSystem.Object? converted = Il2CppRuntime.AsInvocationObject(value, ObjectType);
                if (Member is PropertyInfo prop)
                {
                    // Match the original: SetValue is the authority on whether a setter exists.
                    // This keeps write-only/read-only properties discoverable without pre-filtering.
                    prop.SetValue(Il2CppRuntime.AsReference(instance), converted, null);
                }
                else if (Member is FieldInfo field)
                {
                    Il2CppSystem.Object? target = field.IsStatic ? null : Il2CppRuntime.AsReference(instance);
                    field.SetValue(target, converted);
                }
            }
        }
    }

    internal sealed class VariableBinding
    {
        public readonly Il2CppObjectBase Object;

        internal VariableBinding(Il2CppObjectBase instance)
        {
            Object = instance;
        }

        public override string ToString()
        {
            Type type = Il2CppRuntime.TypeOf(Object);
            string objText = Il2CppRuntime.ToDisplayString(Object);
            return $"ObjectType: {ReflectionPrinter.Print(type, false)} :: ToString: {objText}";
        }
    }
}
