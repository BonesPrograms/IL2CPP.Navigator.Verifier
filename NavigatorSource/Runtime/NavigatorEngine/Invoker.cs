using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using XQuinn.Extensions;
using XQuinn.LangInterp.SyntaxTree;
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
    internal sealed class Invoker : ExpandedCoreObject
    {
        Reflector _reflector => _core._reflector;
        Parser _parser => _core._parser;

        public Invoker(XQuinn.Runtime.NavigatorCore navig) : base(navig) { }

        internal object? InvokeFieldOrProperty(FieldString fieldString)
        {
            Type fromType = _reflector.FindReference(fieldString, out Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase? reference);
            string name = fieldString.Name;
            MemberInfo? member = RuntimeCache.FromCache<MemberInfo>(fieldString.Name, fromType, out bool typeCached, out bool memberCached);

            if (ReturnField(member, fromType, name, fieldString, typeCached, memberCached, reference, out object? result))
                return result;
            if (ReturnProperty(member, fromType, name, fieldString, typeCached, memberCached, reference, out result))
                return result;
            throw new MissingMemberException($"No field or property found named {name} in {fromType}.");
        }

        bool ReturnProperty(MemberInfo? member, Type fromType, string name, FieldString fieldString,
            bool typeCached, bool memberCached, Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase? reference, out object? result)
        {
            result = null;
            if (member == null && Il2CppRuntime.SameType(fromType, _loadedType))
            {
                if (_props.TryGetValue(name, out PropertyInfo? mapped))
                    member = mapped;
            }

            if (member == null && !Il2CppRuntime.SameType(fromType, _loadedType))
            {
                PropertyInfo? found = fromType.GetProperty(name, XQuinn.Runtime.NavigatorCore.Flag);
                if (found != null && found.GetIndexParameters().Length > 0)
                    throw new NotSupportedException("Indexers must be invoked using their backing method.");
                member = found;
            }

            if (member is not PropertyInfo prop)
                return false;

            RuntimeCache.CacheMember(typeCached, memberCached, fromType, prop, fieldString.Name);
            // Match the original reflection path: do not pre-check accessors. Read-only/write-only
            // properties stay discoverable and GetValue itself fails naturally when no getter exists.
            Il2CppSystem.Object? target = Il2CppRuntime.AsReference(TargetInstance(fromType, reference));
            result = prop.GetValue(target, null);
            return true;
        }

        bool ReturnField(MemberInfo? member, Type fromType, string name, FieldString fieldString,
            bool typeCached, bool memberCached, Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase? reference, out object? result)
        {
            result = null;
            if (member == null && Il2CppRuntime.SameType(fromType, _loadedType))
            {
                if (_fields.TryGetValue(name, out FieldInfo? mapped))
                    member = mapped;
            }
            if (member == null && !Il2CppRuntime.SameType(fromType, _loadedType))
                member = fromType.GetField(name, XQuinn.Runtime.NavigatorCore.Flag);

            if (member is not FieldInfo field)
                return false;

            RuntimeCache.CacheMember(typeCached, memberCached, fromType, field, fieldString.Name);
            Il2CppSystem.Object? target = field.IsStatic
                ? null
                : Il2CppRuntime.AsReference(TargetInstance(fromType, reference));
            result = field.GetValue(target);
            return true;
        }

        internal object? InvokeMethod(MethodString methodString)
        {
            Type fromType = _reflector.FindReference(methodString, out Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase? reference);
            MethodBase? call = RuntimeCache.FromCache<MethodBase>(methodString.StringID, fromType, out bool typeCached, out bool methodCached);
            Il2CppReferenceArray<ParameterInfo>? parameters = null;

            if (call == null)
            {
                if (Il2CppRuntime.SameType(fromType, _loadedType))
                    call = _reflector.FindMethod(methodString);
                else
                    ExternalMethod(ref call, ref parameters, fromType, methodString);
            }

            if (call == null)
                throw new MissingMethodException($"No method named {methodString.Name} with generic arg count {methodString.Generics.Count} found in {fromType}'s methods or overload resolutions.");

            if (Types.IsLogicalGeneric(call))
            {
                RuntimeCache.CacheMember(typeCached, methodCached, fromType, call, methodString.StringID);
                return Types.InvokeLogicalGeneric(call, methodString, _parser);
            }

            ConvertGeneric(ref call, ref parameters, methodString);
            parameters ??= call.GetParameters();
            RuntimeCache.CacheMember(typeCached, methodCached, fromType, call, methodString.StringID);
            return FinalizeInvoke(call, parameters, methodString, fromType, reference);
        }


        void ExternalMethod(ref MethodBase? call, ref Il2CppReferenceArray<ParameterInfo>? parameters,
            Type fromType, MethodString methodString)
        {
            if (call != null || Il2CppRuntime.SameType(fromType, _loadedType))
                return;

            bool ambiguous = RuntimeCache.CheckAmbiguousMatch(fromType, methodString.Name,
                out IntPtr cachedType, out HashSet<string>? cachedMatches);

            if (!ambiguous)
            {
                // NavigatorCore.Flag includes IgnoreCase, so the original Type.GetMethod(name, Flag)
                // first lookup was case-insensitive. Count matching names the same way here so
                // ambiguity falls through to MatchIndex resolution exactly as before.
                MethodBase? unique = null;
                int matches = 0;
                foreach (MethodInfo method in fromType.GetMethods(XQuinn.Runtime.NavigatorCore.Flag))
                {
                    if (!method.Name.EqualsCaseless(methodString.Name))
                        continue;
                    unique = method;
                    if (++matches > 1)
                        break;
                }

                if (matches == 1)
                {
                    call = unique;
                    if (Types.IsLogicalGeneric(call!))
                        return;
                    parameters = call!.GetParameters();
                    if (!Reflector.SupportedMember(call, parameters))
                        throw new ArgumentException($"Method {call} in type {call.DeclaringType} has unsupported in out or ref params or ref returntype, or is a static constructor.");
                }
                else if (matches > 1)
                {
                    ambiguous = true;
                    RuntimeCache.CacheAmbiguousMatch(cachedMatches, cachedType, methodString.Name);
                }
            }

            if (call == null || ambiguous)
                SortMatch(methodString, fromType, out parameters, out call);
        }

        internal Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase? TargetInstance(Type fromType, Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase? reference)
        {
            if (reference != null)
                return reference;
            if (_instanceType != null && Il2CppRuntime.IsAssignableFrom(fromType, _instanceType))
                return _object;
            return null;
        }

        object? FinalizeInvoke(MethodBase call, Il2CppReferenceArray<ParameterInfo> parameters,
            MethodString methodString, Type fromType, Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase? reference)
        {
            object?[] parsed = _parser.ParseParameters(parameters, methodString, call);

            // Types is an injected IL2CPP-visible facade. When Navigator itself invokes it, run
            // the logical implementation directly so its original return/exception semantics are
            // preserved instead of being altered by ClassInjector's trampoline boundary.
            if (Types.TryInvokeLogical(call, parsed, out object? logicalResult))
                return logicalResult;

            Il2CppReferenceArray<Il2CppSystem.Object> invokeArgs = new(parsed.Length);
            for (int i = 0; i < parsed.Length; i++)
                invokeArgs[i] = Il2CppRuntime.AsInvocationObject(parsed[i], parameters[i].ParameterType);

            Il2CppSystem.Object? target = call is ConstructorInfo || call.IsStatic
                ? null
                : Types.IsTypesMember(call)
                    ? Types.RuntimeInstance()
                    : XQuinn.Runtime.NavigatorBridge.TypeRegister.IsTypeRegisterMember(call)
                        ? XQuinn.Runtime.NavigatorBridge.TypeRegister.RuntimeInstance()
                    : Il2CppRuntime.AsReference(TargetInstance(fromType, reference));
            return Il2CppRuntime.InvokeReflection(call, target, invokeArgs);
        }

        static void ConvertGeneric(ref MethodBase call, ref Il2CppReferenceArray<ParameterInfo>? parameters, MethodString methodString)
        {
            if (call is not MethodInfo method)
                return;

            if (method.IsGenericMethodDefinition)
            {
                call = methodString.ConvertToGeneric(method);
                parameters = call.GetParameters();
            }
            else if (!method.IsGenericMethod && methodString.Generics.Count > 0)
                throw new ArgumentException($"method {call} cannot accept type arguments.");
        }

        static void SortMatch(MethodString methodString, Type fromType,
            out Il2CppReferenceArray<ParameterInfo>? parameters, out MethodBase? call)
        {
            parameters = null;
            call = null;
            MethodKey query = MethodKey.MethodQuery(methodString);
            List<MethodBase> methods = TypeMap.GetUnfilteredMethods(fromType, query.GKey.Name.EqualsCaseless("new"));
            int matches = 0;
            foreach (MethodBase method in methods)
            {
                GenericKey key = new(method);
                if (key != query.GKey)
                    continue;
                Il2CppReferenceArray<ParameterInfo>? args = Types.IsLogicalGeneric(method) ? null : method.GetParameters();
                if (args != null && !Reflector.SupportedMember(method, args))
                    continue;
                if (matches++ != query.MatchIndex)
                    continue;
                parameters = args;
                call = method;
                return;
            }
        }
    }
}
