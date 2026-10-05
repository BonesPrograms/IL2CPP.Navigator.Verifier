using System;
using System.Collections.Generic;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using XQuinn.Extensions;
using XQuinn.Runtime.NavigatorEngine;
using MethodInfo = Il2CppSystem.Reflection.MethodInfo;

namespace XQuinn.LangInterp.SyntaxTree
{
    internal sealed class MethodString : GenericString, IMemberString
    {
        internal readonly MethodString? _subParamOf;
        public TypeString DeclaringType => _type;
        readonly TypeString _type;
        public IReadOnlyList<ParameterString> Params => _args == null ? Array.Empty<ParameterString>() : _args;
        List<ParameterString>? _args;

        internal static MethodString New(string name, MethodString? paramOf, TypeString declaredIn)
        {
            return New<MethodString>(new(name, paramOf, declaredIn));
        }

        MethodString(string nameForSnipping, MethodString? paramOf, TypeString type) : base(nameForSnipping)
        {
            _subParamOf = paramOf;
            _type = type;
        }

        public MethodInfo ConvertToGeneric(MethodInfo genericMethodDef)
        {
            Il2CppReferenceArray<Il2CppSystem.Type> arguments = ConvertGenericArguments();
            return genericMethodDef.MakeGenericMethod(arguments);
        }

        internal void AddParameter(ParameterString param)
        {
            _args ??= new List<ParameterString>();
            _args.Add(param);
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append($"{DeclaringType.StringID}.{StringID}");
            sb.Append('(');
            sb.AppendMany(Params, ", ", false, x => x is MethodString ms ? $"{ms.DeclaringType.StringID}.{ms.StringID}()" : x!.ToString());
            sb.Append(')');
            return sb.ToString();
        }
    }
}
