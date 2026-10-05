using System;
using System.Text;
using Il2CppInterop.Runtime;
using XQuinn.Extensions;
using XQuinn.Parsing;
using XQuinn.Runtime.NavigatorEngine;
using Type = Il2CppSystem.Type;

namespace XQuinn.LangInterp.SyntaxTree
{
    internal sealed class ValueString : ParameterString
    {
        public string Argument => _arg;

        internal ValueString(string value) : base(value) { }

        /// <summary>Parse a literal for an IL2CPP runtime type.</summary>
        public object? Parse(Type asType)
        {
            if (_arg.EqualsCaseless("default"))
            {
                if (Il2CppRuntime.AllowsNull(asType))
                    return null;
                return Il2CppRuntime.CreateDefault(asType);
            }

            Type? nullable = Il2CppRuntime.NullableUnderlyingType(asType);
            if (nullable != null)
            {
                if (_arg.EqualsCaseless("null"))
                    return null;
                asType = nullable;
            }

            string fullName = asType.FullName ?? asType.Name;
            bool asObject = fullName == "System.Object";
            bool asString = fullName == "System.String";

            if (asType.IsClass)
            {
                if (_arg.EqualsCaseless("null"))
                    return null;
                if (asString || asObject)
                {
                    if (StringFormat(asString, out string? text))
                    {
                        if (asObject)
                            return Il2CppRuntime.AsIl2CppObject(text, Il2CppType.Of<string>());
                        return text;
                    }
                }
                if (!asObject)
                    throw new NotSupportedException($"Cannot convert values to reference type instances. class type: {asType} Value: {_arg}");
            }

            if (asType.IsEnum)
            {
                if (EnumNet20.TryParse(_arg.Replace('|', ','), asType, true, out Il2CppSystem.Object? enumValue))
                    return enumValue;
            }

            if (TryParsePrimitive(asType, asObject, out object? primitive, out Type? primitiveType))
            {
                if (asObject)
                    return Il2CppRuntime.AsIl2CppObject(primitive, primitiveType!);
                return primitive;
            }

            bool decimalType = fullName == "System.Decimal";
            if (asType.IsPrimitive || asType.IsEnum || decimalType || asObject)
                throw new FormatException($"Failed to convert {_arg} to {asType}.");

            throw new NotSupportedException($"Cannot convert values to user-defined struct instances. struct type: {asType}. Value: {_arg}");
        }

        bool TryParsePrimitive(Type requestedType, bool asObject, out object? primitive, out Type? primitiveType)
        {
            primitive = null;
            primitiveType = null;
            string fullName = requestedType.FullName ?? requestedType.Name;

            bool Want(string typeName) => asObject || fullName == typeName;

            if (Want("System.Boolean") && bool.TryParse(_arg, out bool boolean))
                return Set(boolean, Il2CppType.Of<bool>(), out primitive, out primitiveType);
            if (Want("System.Int32") && int.TryParse(_arg, out int int32))
                return Set(int32, Il2CppType.Of<int>(), out primitive, out primitiveType);
            if (Want("System.UInt32") && uint.TryParse(_arg, out uint uint32))
                return Set(uint32, Il2CppType.Of<uint>(), out primitive, out primitiveType);
            if (Want("System.Int64") && long.TryParse(_arg, out long int64))
                return Set(int64, Il2CppType.Of<long>(), out primitive, out primitiveType);
            if (Want("System.UInt64") && ulong.TryParse(_arg, out ulong uint64))
                return Set(uint64, Il2CppType.Of<ulong>(), out primitive, out primitiveType);
            if (Want("System.Single") && float.TryParse(_arg, out float single))
                return Set(single, Il2CppType.Of<float>(), out primitive, out primitiveType);
            if (Want("System.Double") && double.TryParse(_arg, out double dbl))
                return Set(dbl, Il2CppType.Of<double>(), out primitive, out primitiveType);
            if (Want("System.Decimal") && decimal.TryParse(_arg, out decimal dec))
                return Set(dec, Il2CppType.Of<Il2CppSystem.Decimal>(), out primitive, out primitiveType);

            if (Want("System.Char") && CharTryParse(out char ch, fullName == "System.Char"))
                return Set(ch, Il2CppType.Of<char>(), out primitive, out primitiveType);

            if (!asObject)
            {
                if (fullName == "System.IntPtr" && NIntNet20.TryParse(_arg, out nint nintValue))
                    return Set(nintValue, Il2CppType.Of<nint>(), out primitive, out primitiveType);
                if (fullName == "System.UIntPtr" && NUIntNet20.TryParse(_arg, out nuint nuintValue))
                    return Set(nuintValue, Il2CppType.Of<nuint>(), out primitive, out primitiveType);
                if (fullName == "System.Byte" && byte.TryParse(_arg, out byte byteValue))
                    return Set(byteValue, Il2CppType.Of<byte>(), out primitive, out primitiveType);
                if (fullName == "System.SByte" && sbyte.TryParse(_arg, out sbyte sbyteValue))
                    return Set(sbyteValue, Il2CppType.Of<sbyte>(), out primitive, out primitiveType);
                if (fullName == "System.Int16" && short.TryParse(_arg, out short shortValue))
                    return Set(shortValue, Il2CppType.Of<short>(), out primitive, out primitiveType);
                if (fullName == "System.UInt16" && ushort.TryParse(_arg, out ushort ushortValue))
                    return Set(ushortValue, Il2CppType.Of<ushort>(), out primitive, out primitiveType);
            }

            return false;
        }

        static bool Set(object value, Type type, out object? primitive, out Type? primitiveType)
        {
            primitive = value;
            primitiveType = type;
            return true;
        }

        bool StringFormat(bool formatException, out string? extract)
        {
            extract = null;
            if (Argument.Length >= 2)
            {
                string arg = Argument;
                bool noEscape = false;
                if (arg[0] == CallLexer.NoEscDeclr)
                {
                    if (arg[1] != CallLexer.StringDeclr)
                        throw new LexicalException("Invalid string format. A quote char must immediately follow the @ char.", arg);
                    noEscape = true;
                    arg = arg.Substring(1);
                }
                int lastIndex = arg.Length - 1;
                if (arg[0] == '"' && arg[lastIndex] == '"')
                {
                    if (arg.Length <= 3)
                        extract = arg.Length == 2 ? string.Empty : arg[1].ToString();
                    else
                    {
                        StringBuilder sb = new();
                        bool escaping = false;
                        for (int i = 1; i < lastIndex; i++)
                        {
                            char val = arg[i];
                            if (escaping)
                            {
                                if (val != CallLexer.EscSeq && val != CallLexer.StringDeclr)
                                    throw new LexicalException("Can only escape the quote char \" or escape char \\. ", arg);
                                escaping = false;
                            }
                            else if (val == CallLexer.EscSeq && !noEscape)
                            {
                                escaping = true;
                                continue;
                            }
                            else if (val == CallLexer.StringDeclr)
                                throw new LexicalException($"Invalid string format ", arg);
                            sb.Append(val);
                        }
                        extract = sb.ToString();
                    }
                    return true;
                }
            }
            return formatException ? throw new FormatException($"Strings values must have one beginning and ending quotation mark, and be at least 2 chars in size (including quotations). Value: {_arg}") : false;
        }

        bool CharTryParse(out char value, bool formatException)
        {
            value = default;
            if (_arg.Length == 3 && _arg[0] == '\'' && _arg[2] == '\'')
            {
                value = _arg[1];
                return true;
            }
            return formatException ? throw new FormatException($"Invalid char format. Input: {_arg}. Must be surrounzed by apostrophes, must be a single char.") : false;
        }
    }
}
