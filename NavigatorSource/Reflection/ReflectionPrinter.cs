using System;
using System.Text;
using Il2CppInterop.Runtime;
using ConstructorInfo = Il2CppSystem.Reflection.ConstructorInfo;
using FieldAttributes = Il2CppSystem.Reflection.FieldAttributes;
using FieldInfo = Il2CppSystem.Reflection.FieldInfo;
using MemberInfo = Il2CppSystem.Reflection.MemberInfo;
using MethodAttributes = Il2CppSystem.Reflection.MethodAttributes;
using MethodBase = Il2CppSystem.Reflection.MethodBase;
using MethodInfo = Il2CppSystem.Reflection.MethodInfo;
using PropertyInfo = Il2CppSystem.Reflection.PropertyInfo;
using Type = Il2CppSystem.Type;

namespace XQuinn.Reflection
{
    public static class ReflectionPrinter
    {
        public static string Print(MemberInfo info, bool fullname)
        {
            StringBuilder sb = Prefix(new StringBuilder(), info);
            MetadataPrinter.BuildPrint(sb, info, fullname);
            if (info is Type type && type.BaseType != null &&
                !XQuinn.Runtime.NavigatorEngine.Il2CppRuntime.SameType(type.BaseType, Il2CppType.Of<Il2CppSystem.Object>()))
            {
                sb.Append(" : ");
                MetadataPrinter.GenericTypeToString(sb, type.BaseType, fullname);
            }
            return sb.ToString();
        }

        static StringBuilder Prefix(StringBuilder sb, MemberInfo info)
        {
            if (info is Type type)
                return TypePrefix(sb, type);
            if (info is MethodBase method)
                return MethodPrefix(sb, method);
            if (info is FieldInfo field)
                return FieldPrefix(sb, field);
            if (info is PropertyInfo prop)
                return PropertyPrefix(sb, prop);
            return sb;
        }

        static StringBuilder PropertyPrefix(StringBuilder sb, PropertyInfo prop)
        {
            sb.Append("Property ");
            sb.Append(prop.Name);
            sb.Append(' ');
            MethodInfo? getter = prop.GetGetMethod(true);
            MethodInfo? setter = prop.GetSetMethod(true);
            if (getter != null)
            {
                sb.Append(Access(getter.Attributes));
                sb.Append(" get; ");
            }
            if (setter != null)
            {
                sb.Append(Access(setter.Attributes));
                sb.Append(" set; ");
            }
            return sb;
        }

        static StringBuilder FieldPrefix(StringBuilder sb, FieldInfo field)
        {
            FieldAttributes attrs = field.Attributes;
            sb.Append(Access(attrs));
            sb.Append(' ');
            if ((attrs & FieldAttributes.Literal) != 0) sb.Append("const ");
            else if ((attrs & FieldAttributes.Static) != 0) sb.Append("static ");
            if ((attrs & FieldAttributes.InitOnly) != 0) sb.Append("readonly ");
            return sb;
        }

        static StringBuilder MethodPrefix(StringBuilder sb, MethodBase method)
        {
            MethodAttributes attrs = method.Attributes;
            sb.Append(Access(attrs));
            sb.Append(' ');
            if (method is ConstructorInfo)
                return (attrs & MethodAttributes.Static) != 0 ? sb.Append("static ") : sb;

            MethodInfo info = (MethodInfo)method;
            bool overridden = false;
            if (info.DeclaringType != null && (attrs & MethodAttributes.Virtual) != 0)
            {
                MethodInfo? baseMethod = info.GetBaseMethod();
                overridden = baseMethod?.DeclaringType != null &&
                             !XQuinn.Runtime.NavigatorEngine.Il2CppRuntime.SameType(info.DeclaringType, baseMethod.DeclaringType);
                if (overridden)
                    sb.Append("override ");
            }

            if ((attrs & MethodAttributes.Final) != 0) sb.Append("sealed ");
            else if ((attrs & MethodAttributes.Abstract) != 0) sb.Append("abstract ");
            else if (!overridden && (attrs & MethodAttributes.Virtual) != 0) sb.Append("virtual ");
            return sb;
        }

        static StringBuilder TypePrefix(StringBuilder sb, Type type)
        {
            // ClassInjector copies most class flags from Il2CppSystem.Object and does not
            // preserve the managed static-class (abstract+sealed) flags. Types is logically
            // still the original static helper class.
            if (XQuinn.Runtime.NavigatorEngine.Types.IsTypesType(type) ||
                XQuinn.Runtime.NavigatorBridge.TypeRegister.IsTypeRegisterType(type)) return sb.Append("static ");
            if (type.IsAbstract && type.IsSealed) return sb.Append("static ");
            if (type.IsAbstract) return sb.Append("abstract ");
            if (type.IsSealed) return sb.Append("sealed ");
            return sb;
        }

        static string Access(MethodAttributes attrs)
        {
            MethodAttributes access = attrs & MethodAttributes.MemberAccessMask;
            if (access == MethodAttributes.Public) return "public";
            if (access == MethodAttributes.Family) return "protected";
            if (access == MethodAttributes.Private) return "private";
            if (access == MethodAttributes.Assembly) return "internal";
            if (access == MethodAttributes.FamANDAssem) return "private protected";
            if (access == MethodAttributes.FamORAssem) return "protected internal";
            throw new InvalidOperationException("FieldInfo or MethodBase object has invalid access modifiers.");
        }

        static string Access(FieldAttributes attrs)
        {
            FieldAttributes access = attrs & FieldAttributes.FieldAccessMask;
            if (access == FieldAttributes.Public) return "public";
            if (access == FieldAttributes.Family) return "protected";
            if (access == FieldAttributes.Private) return "private";
            if (access == FieldAttributes.Assembly) return "internal";
            if (access == FieldAttributes.FamANDAssem) return "private protected";
            if (access == FieldAttributes.FamORAssem) return "protected internal";
            throw new InvalidOperationException("FieldInfo or MethodBase object has invalid access modifiers.");
        }
    }
}
