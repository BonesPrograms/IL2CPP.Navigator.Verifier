#if XQUINN_ENABLE_LEGACY_MANAGED_REFLECTION
using System.Reflection.Emit;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using System.Text;
using XQuinn.IO;
using System.Linq;
using static XQuinn.Reflection.ByteSizes;

namespace XQuinn.Reflection
{

    //Todo: Reading delegates, reading delegate bodies?


    //learn wtf an offset is in IL

    //Update:
    //This has gotten pretty good now, the only thing it cant really read yet are jump labels. Otherwise it is able to read shit pretty much just as good as the MonoCecil ILReader

    /// <summary>
    /// Converts a method into readable IL instructions. 
    /// </summary>
    public sealed class ILReader : IOStream
    {

        IList<LocalVariableInfo> _localvars = null!;
        ParameterInfo[] _params = null!;
        byte[] _il = null!;
        Module _module = null!;
        MethodBase _methodbase = null!;

        /// <summary>
        /// Some opcodes are 2 bytes long, they will always start with a "prefix" byte.
        /// </summary>
        const byte PrefixBit = 0xFE;
        Type[]? _generic_method_args;
        Type[]? _generic_type_args;
        ILReader()
        {
        }
        public static void PrintIL(MethodBase method, string outputFilePath, bool makeFileIfNotFound)
        {
            using ILReader reader = New(outputFilePath, makeFileIfNotFound);
            reader.Update(method);
            reader.PrintIL();
        }


        public static ILReader New(string outputFilePath, bool makeFileIfNotFound)
        {
            ILReader reader = IOStream.New<ILReader>(new(), outputFilePath, makeFileIfNotFound);
            return reader;
        }

        public void Update(MethodBase method)
        {
            if (Harmony.GetPatchInfo(method) != null)
                throw new NotSupportedException($"{method} in {method.DeclaringType} has been patched by harmony and it's actual behavior cannot properly be represented by ILReader.");
            MethodBody body = method.GetMethodBody() ?? throw new ArgumentException("Method body is null.");
            _methodbase = method;
            _module = method.Module;
            _localvars = body.LocalVariables;
            _il = body.GetILAsByteArray() ?? throw new ArgumentException("Byte array is null.");
            _params = method.GetParameters();
            if (method is MethodInfo)
                _generic_method_args = method.GetGenericArguments();
            _generic_type_args = method.DeclaringType?.GetGenericArguments();
        }


        // public string ReadIL()
        // {
        //     StringBuilder sb = new();
        // }

        /// <summary>
        /// Prints readable IL to a file.
        /// </summary>
        /// <param name="outputFilePath"></param>
        public void PrintIL() //sho
        {
            if (_methodbase == null)
                throw new ArgumentNullException(nameof(_methodbase));
            Writer.WriteLine("method");
            Writer.Write("  ");
            string methodstring;
            if (_methodbase is MethodInfo mthdinfo)
                methodstring = MetadataPrinter.MethodToString(new(), mthdinfo, true).ToString();
            else
                methodstring = MetadataPrinter.ConstructorToString(new(), (ConstructorInfo)_methodbase, false).ToString();
            Writer.WriteLine($"  {methodstring}");//,maybe should edit the stringbuilder to slip in parameter names
            Writer.WriteLine("");
            //  writer.WriteLine("	.maxstack 1");
            Writer.WriteLine("Locals");
            for (int i = 0; i < _localvars.Count; i++)
            {
                bool needcomma = For.SmartDelimiter(_localvars.Count, i);
                char? comma = needcomma ? ',' : null;
                Writer.WriteLine($"		[{i}] {_localvars[i].LocalType.Name}{comma}");

            }
            Writer.WriteLine("");
            List<ByteCode> codes = GetIL();
            foreach (ByteCode code in codes)
                Writer.WriteLine("	" + code.ToString());
            Flush();
        }

        /// <summary>
        /// Returns a list of readable IL.
        /// </summary>
        /// <returns></returns>
        List<ByteCode> GetIL()
        {
            int i = 0;
            List<ByteCode> codes = new();
            while (i < _il.Length)
                BytesToIL(codes, ref i);
            return codes;
        }

        void BytesToIL(List<ByteCode> codes, ref int i)
        {
            OpCode code = GetOpCode(ref i);
            int size = OperandSize(code.OperandType);
            object token = GetToken(i, ref size, code.OperandType);
            object? operand = size == x0bit ? null : GetOperand(code, token, i);
            ByteCode instruction = new(code, operand, i - 1);
            codes.Add(instruction); //for some reason it is off by 1, you want to shift the offset back by 1 or every label will be off by 1 individually and cumulatively so all labels will be off
            i += size;
        }

        OpCode GetOpCode(ref int i)
        {
            OpCode code;
            byte indexedbyte = _il[i];
            if (indexedbyte == PrefixBit)
            {
                byte nextbyte = _il[i + 1];
                short key = (short)((PrefixBit << 0x08) | nextbyte);
                code = OpCodeMap.s_opCodes[key];
                i += 2;
            }
            else
            {
                code = OpCodeMap.s_opCodes[indexedbyte];
                i++;
            }
            return code;
        }


        object GetToken(int i, ref int size, OperandType type)
        {
            if (size == x0bit)
                return x0bit; //no operand
            else if (size == x8bit)
                return _il[i];
            else if (size == x16bit)
                return LittleEndians.Int16(_il, i);
            else if (size == x32bit)
            {
                int token;
                if (type == OperandType.ShortInlineR)
                    return LittleEndians.Float32(_il, i);
                else
                    token = LittleEndians.Int32(_il, i);
                if (type == OperandType.InlineSwitch)
                    size += token * 4;
                return token;
            }
            else if (size == x64bit)
            {
                if (type == OperandType.InlineR)
                    return LittleEndians.Float64(_il, i);
                return LittleEndians.Int64(_il, i);
            }
            throw new InvalidOperationException("OperandSize(OperandType) returned an out-of-range value.");
        }

        object? GetOperand(OpCode code, object token, int i)
        =>
            code.OperandType switch
            {
                OperandType.ShortInlineBrTarget => (sbyte)(byte)token + i + 1,
                OperandType.ShortInlineI => (sbyte)(byte)token,
                OperandType.InlineI => (int)token, //these are kept for sorting later in bytecode; if you get a token that is int and its operand is not a valuetype, you know its a class
                OperandType.ShortInlineR => (float)token, //i could maybe just make all these return null but then id have to do a lot of patern matching to assign the 
                OperandType.InlineR => (double)token, //"token" value to the operand in the ByteCode constructor so fuck that
                OperandType.InlineI8 => (long)token, //maybe i could just check if the token is not int... would require changing the base constructor since it takes the operand first
                OperandType.ShortInlineVar => GetVariable(code, (byte)token), //or something
                OperandType.InlineVar => GetVariable(code, (short)token),
                OperandType.InlineMethod => _module.ResolveMethod((int)token, _generic_type_args, _generic_method_args),
                OperandType.InlineField => _module.ResolveField((int)token, _generic_type_args, _generic_method_args),
                OperandType.InlineType => _module.ResolveType((int)token, _generic_type_args, _generic_method_args),
                OperandType.InlineString => _module.ResolveString((int)token),
                OperandType.InlineTok => _module.ResolveMember((int)token, _generic_type_args, _generic_method_args),
                OperandType.InlineSig => _module.ResolveSignature((int)token),
                OperandType.InlineNone or OperandType.InlineSwitch => null, //the "token" is the jump target, and will carry over for display in ByteCode, so operand is unecessary
                OperandType.InlineBrTarget => (int)token + i + 1, //idk why u need to do +1 but it is always -1 instruction off from its actual jump target
                _ => throw new InvalidProgramException("Operand type does not exist.")
            };

        object GetVariable(OpCode opcode, int token)
        {
            if (opcode.Name?.Contains("loc") ?? false)
            {
                return _localvars[token];
            }
            else
            {
                if (_methodbase.IsStatic)
                {
                    return _params[token];
                }
                else if (token == 0) //idk why a token of 0 == this, but i guess that is the byte code for this, wonder where i can learn that
                    return "this"; //idk how to get this, was trying to research it monocecil, but cant access the class they use to resolve waht this is
                return _params[token - 1]; //not sure why they do -1 here, not sure why i do +1 for the jump label lol
            }                              //i suppose it has something to do with being nonstatic? but why -1? is "this" on the params list?
        }

#pragma warning disable CS8509 // OperandType.InlinePhi is excluded because it is obsolete.
        static byte OperandSize(OperandType type) =>
        type switch
        {
            OperandType.InlineNone => x0bit,
            OperandType.ShortInlineVar or OperandType.ShortInlineI or OperandType.ShortInlineBrTarget => x8bit,
            OperandType.InlineVar => x16bit,
            OperandType.InlineTok or OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineType or OperandType.ShortInlineR or OperandType.InlineSwitch => x32bit,
            OperandType.InlineR or OperandType.InlineI8 => x64bit
        };
#pragma warning restore CS8509
    }

    static class OpCodeMap
    {

        /// <summary>
        /// A dictionary of opcodes that can be indexed by their short value (OpCode.Value).
        /// </summary>
        public static readonly IReadOnlyDictionary<short, OpCode> s_opCodes = Map();

        static Dictionary<short, OpCode> Map()
        {
            Dictionary<short, OpCode> dic = typeof(OpCodes)
            .GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(x => x.FieldType == typeof(OpCode))
            .Select(x => (OpCode)x.GetValue(null)!).ToDictionary(x => x.Value, v => v);
            return dic;
        }

    }
    /// <summary>
    /// Readable IL instruction.
    /// </summary>
    readonly struct ByteCode
    {

        public static bool ShowOperandType = false;

        readonly int Offset; //this is kind of fucking useless to end users right now, since this isnt really part of some sort of reflection.emit system. emit.label?
        public readonly OpCode OpCode;
        public readonly object? Operand;
        // public readonly int? Token;
        internal ByteCode(OpCode opcode, object? operand, int offset)
        {
            Operand = operand;
            OpCode = opcode;
            Offset = offset;
            //  Token = null;
            // if (operand is not null and not LocalVariableInfo and not ParameterInfo and not ValueType)
            // {
            //     if (token is int integer)
            //       Token = integer;
            // }

        }

        public readonly override string ToString()
        {
            StringBuilder sb = new();
            sb.Append($"IL_{Offset:x4}: ");
            sb.Append(OpCode.ToString());
            if (ShowOperandType)
                sb.Append($" {OpCode.OperandType}");
            if (Operand is ConstructorInfo)
                sb.Append(" instance void");
            sb.Append(' ');
            if (Operand != null)
                OperandToString(sb);
            return sb.ToString();
        }
        readonly void OperandToString(StringBuilder sb)
        {
            if (Operand is LocalVariableInfo lvar)
            {
                MetadataPrinter.GenericTypeToString(sb, lvar.LocalType, false);
                sb.Append($" {lvar.LocalIndex}");
            }
            else if (Operand is ParameterInfo info)
            {
                MetadataPrinter.GenericTypeToString(sb, info.ParameterType, false);
                sb.Append($" {info.Name}");
            }
            else if (OpCode.OperandType == OperandType.InlineBrTarget || OpCode.OperandType == OperandType.ShortInlineBrTarget)
            {
                if (Operand == null)
                    throw new InvalidOperationException();
                int num = (int)Operand;
                sb.Append($"IL_{num:x4}");
            }
            else if (Operand is string)
                sb.Append($"\"{Operand}\"");
            else if (Operand is MemberInfo inf)
                MetadataPrinter.BuildPrint(sb, inf!, false);
            else
                sb.Append($"{Operand?.ToString()}");
        }
    }


    static class LittleEndians
    {
        public static short Int16(byte[] il, int i)
        {
            return BitConverter.ToInt16(TruncateReverse(il, i, x16bit, out int start), start);
        }
        public static float Float32(byte[] il, int i)
        {
            return BitConverter.ToSingle(TruncateReverse(il, i, x32bit, out int start), start);

        }

        public static int Int32(byte[] il, int i)
        {
            return BitConverter.ToInt32(TruncateReverse(il, i, x32bit, out int start), start);
        }

        public static double Float64(byte[] il, int i)
        {
            return BitConverter.ToDouble(TruncateReverse(il, i, x64bit, out int start), start);
        }

        public static long Int64(byte[] il, int i)
        {
            return BitConverter.ToInt64(TruncateReverse(il, i, x64bit, out int start), start);
        }


        static byte[] TruncateReverse(byte[] il, int i, int size, out int start)
        {
            if (BitConverter.IsLittleEndian)
            {
                start = i;
                return il;
            }
            start = 0;
            byte[] reversed = new byte[size];
            int cap = i + size;
            int insert = size - 1;
            for (int x = i; x < cap; x++)
            {
                reversed[insert] = il[x];
                insert--;
            }
            return reversed;
        }
    }
    static class ByteSizes
    {
        /// <summary>
        /// 0 bytes.
        /// </summary>
        public const int x0bit = 0x00;

        /// <summary>
        /// 1 byte.
        /// </summary>
        public const int x8bit = sizeof(byte);
        /// <summary>
        /// 2 bytes.
        /// </summary>
        public const int x16bit = sizeof(short);
        /// <summary>
        /// 4 bytes.
        /// </summary>
        public const int x32bit = sizeof(int);
        /// <summary>
        /// 8 bytes.
        /// </summary>
        public const int x64bit = sizeof(long);

        //  public const int x128bit = 0x16;

        //  public const int x256bit = 0x32;
    }

}

#endif
