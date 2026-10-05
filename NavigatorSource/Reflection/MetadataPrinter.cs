using System.Text;
using Il2CppInterop.Runtime;
using XQuinn.Runtime.NavigatorEngine;
using ConstructorInfo = Il2CppSystem.Reflection.ConstructorInfo;
using MethodBase = Il2CppSystem.Reflection.MethodBase;
using ParameterAttributes = Il2CppSystem.Reflection.ParameterAttributes;
using MemberInfo = Il2CppSystem.Reflection.MemberInfo;
using MethodInfo = Il2CppSystem.Reflection.MethodInfo;
using ParameterInfo = Il2CppSystem.Reflection.ParameterInfo;
using PropertyInfo = Il2CppSystem.Reflection.PropertyInfo;
using FieldInfo = Il2CppSystem.Reflection.FieldInfo;
using EventInfo = Il2CppSystem.Reflection.EventInfo;
using Type = Il2CppSystem.Type;

namespace XQuinn.Reflection
{
    internal static class MetadataPrinter
    {
        public static StringBuilder BuildPrint(StringBuilder sb, MemberInfo obj, bool fullname)
        {
            if (obj is MethodInfo method)
                return MethodToString(sb, method, fullname);
            if (obj is ConstructorInfo ctor)
                return ConstructorToString(sb, ctor, fullname);
            if (obj is Type type)
                return TypeToString(sb, type, fullname);
            if (obj is PropertyInfo)
                return sb;
            if (obj is FieldInfo field)
                return FieldToString(sb, field, fullname);
            if (obj is EventInfo eventInfo)
                return EventToString(sb, eventInfo, fullname);
            sb.Append(obj.Name);
            return sb;
        }

        public static StringBuilder TypeToString(StringBuilder sb, Type type, bool fullname)
        {
            Type delegateType = Il2CppType.Of<Il2CppSystem.Delegate>();
            Type enumerableType = Il2CppType.Of<Il2CppSystem.Collections.IEnumerable>();
            Type collectionType = Il2CppType.Of<Il2CppSystem.Collections.ICollection>();
            Type stringType = Il2CppType.Of<string>();

            if (Il2CppRuntime.IsAssignableFrom(delegateType, type)) sb.Append("delegate");
            else if (type.IsEnum) sb.Append("enum");
            else if (type.IsArray) sb.Append("array");
            else if (type.IsInterface) sb.Append("interface");
            else if (!Il2CppRuntime.SameType(type, stringType) &&
                     (Il2CppRuntime.IsAssignableFrom(enumerableType, type) || Il2CppRuntime.IsAssignableFrom(collectionType, type)))
                sb.Append("collection");
            else if (type.IsClass) sb.Append("class");
            else sb.Append("struct");
            sb.Append(' ');
            GenericTypeToString(sb, type, fullname);
            return sb;
        }

        static StringBuilder FieldToString(StringBuilder sb, FieldInfo field, bool fullname)
        {
            // Preserve the original MetadataPrinter output shape exactly, including the
            // duplicated underlying field type after the declaring-type separator.
            sb.Append(field.FieldType.ToString());
            sb.Append(' ');
            GenericTypeToString(sb, field.DeclaringType, fullname);
            sb.Append("::");
            GenericTypeToString(sb, field.FieldType, fullname);
            sb.Append(' ');
            FixGenericString(sb, field.Name);
            return sb;
        }

        static StringBuilder EventToString(StringBuilder sb, EventInfo eventInfo, bool fullname)
        {
            Type? eventType = eventInfo.EventHandlerType;
            if (eventType != null)
            {
                sb.Append(eventType.ToString());
                sb.Append(' ');
                GenericTypeToString(sb, eventInfo.DeclaringType, fullname);
                sb.Append("::");
                GenericTypeToString(sb, eventType, fullname);
                sb.Append(' ');
                FixGenericString(sb, eventInfo.Name);
            }
            else
                sb.Append(eventInfo.Name);
            return sb;
        }

        public static StringBuilder ConstructorToString(StringBuilder sb, ConstructorInfo ctor, bool fullname)
        {
            GenericTypeToString(sb, ctor.DeclaringType, fullname);
            sb.Append("::.ctor");
            ParamsToString(sb, ctor, ctor.GetParameters(), fullname);
            return sb;
        }

        public static StringBuilder MethodToString(StringBuilder sb, MethodInfo method, bool fullname)
        {
            if (Types.TryPrintLogicalGeneric(sb, method, fullname))
                return sb;
            sb.Append(method.IsStatic || Types.IsLogicalStatic(method) ? "static " : "instance ");
            AppendReturnType(sb, method, fullname);
            sb.Append(' ');
            GenericTypeToString(sb, method.DeclaringType, fullname);
            sb.Append("::");
            sb.Append(method.Name);
            // Some Il2CppInterop/ClassInjector runtimes expose injected non-generic methods
            // through reflection objects whose GetGenericArguments implementation dereferences
            // invalid native generic metadata. Do not invoke it for a method that reflection has
            // already identified as non-generic. Logical Types generics return above through
            // TryPrintLogicalGeneric and never use the native generic-argument accessor.
            if (method.IsGenericMethod)
                AddGenericArguments(sb, method.GetGenericArguments(), fullname);
            ParamsToString(sb, method, method.GetParameters(), fullname);
            return sb;
        }

        static void AppendReturnType(StringBuilder sb, MethodInfo method, bool fullname)
        {
            if (Types.TryAppendLogicalReturnType(method, sb, fullname))
                return;

            Type type = method.ReturnType;
            string name = type.Name;
            if (name == "Boolean") sb.Append("bool");
            else if (name.Equals("String", System.StringComparison.OrdinalIgnoreCase)) sb.Append("string");
            else if (name.Equals("Void", System.StringComparison.OrdinalIgnoreCase)) sb.Append("void");
            else GenericTypeToString(sb, type, fullname);
        }

        static StringBuilder ParamsToString(StringBuilder sb, MethodBase method, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<ParameterInfo> args, bool fullname)
        {
            sb.Append('(');
            Type paramArrayAttribute = Il2CppType.Of<Il2CppSystem.ParamArrayAttribute>();
            for (int i = 0; i < args.Length; i++)
            {
                ParameterInfo arg = args[i];
                if (arg.IsDefined(paramArrayAttribute, false)) sb.Append("params ");
                else if (arg.IsIn) sb.Append("in ");
                else if (arg.IsOut) sb.Append("out ");
                else if (arg.ParameterType.IsByRef) sb.Append("ref ");

                GenericTypeToString(sb, arg.ParameterType, fullname);

                sb.Append(' ');
                sb.Append(arg.Name);

                object? logicalDefault;
                bool hasDefault = Types.TryGetLogicalDefault(method, i, arg.ParameterType, out logicalDefault);
                if (!hasDefault && (arg.Attributes & ParameterAttributes.HasDefault) != 0)
                {
                    logicalDefault = arg.DefaultValue;
                    hasDefault = true;
                }
                if (hasDefault)
                {
                    sb.Append(" = ");
                    sb.Append(logicalDefault == null ? (arg.ParameterType.IsClass ? "null" : "default") : logicalDefault.ToString());
                }
                if (i + 1 < args.Length)
                    sb.Append(", ");
            }
            sb.Append(')');
            return sb;
        }

        public static void GenericTypeToString(StringBuilder sb, Type? type, bool fullname)
        {
            if (type == null)
                return;
            string name = fullname ? type.FullName ?? type.Name : type.Name;
            FixGenericString(sb, name);
            AddGenericArguments(sb, type.GetGenericArguments(), fullname);
        }

        internal static void FixGenericString(StringBuilder sb, string value)
        {
            foreach (char c in value)
            {
                if (c == '`')
                    break;
                sb.Append(c);
            }
        }

        internal static void AddGenericArguments(StringBuilder sb, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Type>? genericArgs, bool fullname)
        {
            if (genericArgs == null || genericArgs.Length == 0)
                return;
            sb.Append('<');
            for (int i = 0; i < genericArgs.Length; i++)
            {
                Type arg = genericArgs[i];
                FixGenericString(sb, fullname ? arg.FullName ?? arg.Name : arg.Name);
                if (arg.IsGenericType)
                    AddGenericArguments(sb, arg.GetGenericArguments(), fullname);
                if (i + 1 < genericArgs.Length)
                    sb.Append(", ");
            }
            sb.Append('>');
        }
    }
}
