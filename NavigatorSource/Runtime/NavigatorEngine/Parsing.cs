using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using XQuinn.Extensions;
using XQuinn.LangInterp.SyntaxTree;
using FieldInfo = Il2CppSystem.Reflection.FieldInfo;
using ParameterInfo = Il2CppSystem.Reflection.ParameterInfo;
using PropertyInfo = Il2CppSystem.Reflection.PropertyInfo;
using MethodBase = Il2CppSystem.Reflection.MethodBase;
using ParameterAttributes = Il2CppSystem.Reflection.ParameterAttributes;
using Type = Il2CppSystem.Type;

namespace XQuinn.Runtime.NavigatorEngine
{
    internal sealed class Parser : CoreObject
    {
        Invoker _invoker => _core._invoker;
        static Type ParamArrayAttributeType => Il2CppType.Of<Il2CppSystem.ParamArrayAttribute>();

        public Parser(XQuinn.Runtime.NavigatorCore navig) : base(navig) { }

        public object?[] ParseParameters(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<ParameterInfo> parameters, MethodString method, MethodBase call)
        {
            Parameter[] schema = new Parameter[parameters.Length];
            for (int i = 0; i < schema.Length; i++)
                schema[i] = new Parameter(parameters[i], call, i);
            return ParseParameters(schema, method);
        }

        internal object?[] ParseParameters(Parameter[] parameters, MethodString method)
        {
            int inputAmount = method.Params.Count;
            int requiredAmount = parameters.Length;
            int last = requiredAmount - 1;

            if (requiredAmount == 0)
            {
                if (inputAmount == 0)
                    return Array.Empty<object?>();
                throw new System.Reflection.TargetParameterCountException($"input param count: {inputAmount} required count: 0 method name {method.StringID}");
            }

            object?[] args = new object?[requiredAmount];
            if (inputAmount != requiredAmount)
                UnequalParamCount(inputAmount, requiredAmount, parameters, args, method, last);
            else if (parameters[last].IsParams)
                ParamsArray(last, parameters, args, method);
            else
                for (int i = 0; i < requiredAmount; i++)
                    args[i] = ParameterToObject(method.Params[i], parameters[i].ParameterType);
            return args;
        }

        void UnequalParamCount(int inputAmount, int requiredAmount,
            Parameter[] parameters,
            object?[] args, MethodString method, int last)
        {
            if (inputAmount < requiredAmount)
            {
                for (int i = inputAmount; i < requiredAmount; i++)
                {
                    Parameter parameter = parameters[i];
                    if (parameter.TryGetDefault(out object? defaultValue))
                        args[i] = defaultValue;
                    else if (parameter.IsParams)
                    {
                        Type elementType = parameter.ElementType;
                        args[i] = Il2CppRuntime.CreateArray(elementType, 0);
                    }
                    else
                        throw new System.Reflection.TargetParameterCountException($"Parameter {parameter} does not have a default value. Input param count {inputAmount} Required count {requiredAmount} method name {method.StringID}");
                }

                for (int i = 0; i < inputAmount; i++)
                    args[i] = ParameterToObject(method.Params[i], parameters[i].ParameterType);
                return;
            }

            if (last >= 0 && parameters[last].IsParams)
            {
                ParamsArray(last, parameters, args, method);
                return;
            }

            throw new System.Reflection.TargetParameterCountException($"input param count: {inputAmount} required count: {requiredAmount} method name {method.Name}");
        }

        void ParamsArray(int last,
            Parameter[] parameters,
            object?[] args, MethodString method)
        {
            for (int i = 0; i < last; i++)
                args[i] = ParameterToObject(method.Params[i], parameters[i].ParameterType);

            Type elementType = parameters[last].ElementType;

            // The managed implementation used a local defaultValue only to avoid writing
            // null/default elements into a freshly-created array. Il2CppRuntime.CreateArray
            // preserves that behavior by leaving null slots at their runtime default.
            if (method.Params.Count == parameters.Length)
            {
                ParameterString source = method.Params[last];
                if (source is ValueString literal &&
                    (literal.Argument.EqualsCaseless("null") || literal.Argument.EqualsCaseless("default")))
                {
                    args[last] = null;
                    return;
                }

                object? value = ParameterToObject(source, elementType);
                if (value is Il2CppObjectBase ilValue && Il2CppRuntime.TypeOf(ilValue).IsArray)
                {
                    // Match the managed implementation: an already-created array is passed
                    // through as the params argument and invocation performs the final type check.
                    args[last] = value;
                    return;
                }

                args[last] = Il2CppRuntime.CreateArray(elementType, new object?[] { value });
                return;
            }

            object?[] values = new object?[method.Params.Count - last];
            for (int i = last; i < method.Params.Count; i++)
                values[i - last] = ParameterToObject(method.Params[i], elementType);
            args[last] = Il2CppRuntime.CreateArray(elementType, values);
        }

        // Both native reflected methods and the logical Types helpers use this same parser.
        // Logical schemas carry IL2CPP types directly and never inflate a CLR generic method.
        internal readonly struct Parameter
        {
            readonly ParameterInfo? _native;
            readonly MethodBase? _call;
            readonly int _index;
            readonly bool _hasDefault;
            readonly object? _default;
            readonly Func<object?>? _defaultFactory;
            readonly Type? _element;
            internal Type ParameterType { get; }
            internal bool IsParams => _native != null
                ? IsParamArray(_native, _call!, _index) : _element != null;
            internal Type ElementType => _native != null
                ? ParamArrayElementType(_native, _call!, _index) : _element!;

            internal Parameter(ParameterInfo native, MethodBase call, int index)
            {
                _native = native;
                _call = call;
                _index = index;
                ParameterType = native.ParameterType;
                _hasDefault = false;
                _default = null;
                _defaultFactory = null;
                _element = null;
            }

            internal Parameter(Type type, bool hasDefault = false, object? defaultValue = null,
                Type? elementType = null, Func<object?>? defaultFactory = null)
            {
                _native = null;
                _call = null;
                _index = 0;
                ParameterType = type;
                _hasDefault = hasDefault;
                _default = defaultValue;
                _defaultFactory = defaultFactory;
                _element = elementType;
            }

            internal bool TryGetDefault(out object? value)
            {
                if (_native != null)
                    return TryGetDefaultValue(_native, _call!, _index, out value);
                value = _hasDefault && _defaultFactory != null ? _defaultFactory() : _default;
                return _hasDefault;
            }

            public override string ToString() => _native?.ToString() ?? ParameterType.ToString();
        }

        static bool IsParamArray(ParameterInfo parameter, MethodBase call, int parameterIndex)
            => parameter.IsDefined(ParamArrayAttributeType, false);

        static Type ParamArrayElementType(ParameterInfo parameter, MethodBase call, int parameterIndex)
        {
            return parameter.ParameterType.GetElementType()
                ?? throw new ArgumentNullException();
        }

        static bool TryGetDefaultValue(ParameterInfo parameter, MethodBase call, int parameterIndex, out object? value)
        {
            if (Types.TryGetLogicalDefault(call, parameterIndex, parameter.ParameterType, out value))
                return true;

            // The supplied IL2CPP proxy does not expose HasDefaultValue. The metadata
            // HasDefault bit is the closest direct equivalent. IsOptional alone is NOT enough:
            // CLR reflection can report Optional with no default value at all.
            if ((parameter.Attributes & ParameterAttributes.HasDefault) != 0)
            {
                value = parameter.DefaultValue;
                return true;
            }

            value = null;
            return false;
        }

        internal object? ParameterToObject(ParameterString arg, Type paramType)
        {
            object? obj = arg switch
            {
                FieldString field => _invoker.InvokeFieldOrProperty(field),
                MethodString method => _invoker.InvokeMethod(method),
                ValueString value => ParseValue(value, paramType),
                _ => throw new NotSupportedException()
            };
            Reflector.NotNullable(paramType, obj);
            return obj;
        }

        internal object? ParseValue(ValueString value, Type paramType)
        {
            string text = value.Argument;
            if (text.EqualsCaseless("this"))
            {
                if (_object == null)
                    throw new InvalidOperationException("Cannot pass this as parameter, instance is null.");
                return _object;
            }

            if (_variables.TryGetValue(text, out VariableBinding? variable))
                return variable.Object;

            if (_fields.TryGetValue(text, out FieldInfo? field))
            {
                Il2CppSystem.Object? target = field.IsStatic ? null : Il2CppRuntime.AsReference(_object);
                return field.GetValue(target);
            }

            if (_props.TryGetValue(text, out PropertyInfo? prop))
            {
                // Preserve the CLR implementation: attempt the read and let PropertyInfo.GetValue
                // report a missing getter rather than imposing a separate accessor policy here.
                return prop.GetValue(Il2CppRuntime.AsReference(_object), null);
            }

            return value.Parse(paramType);
        }
    }
}
