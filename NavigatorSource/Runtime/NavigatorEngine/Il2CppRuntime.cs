using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppType = Il2CppSystem.Type;

namespace XQuinn.Runtime.NavigatorEngine
{
    /// <summary>
    /// Small boundary between XQuinn's managed control code and objects/types that live in IL2CPP.
    /// Runtime-facing metadata is always Il2CppSystem metadata; managed System.Type is never used
    /// to describe a game/runtime type.
    /// </summary>
    internal static class Il2CppRuntime
    {
        // Mirrors Array.Empty<T>() semantics for Types.Array<T>. These wrappers own native
        // GC handles, so keeping them here also keeps the IL2CPP singleton arrays rooted.
        static readonly Dictionary<IntPtr, Il2CppSystem.Array> s_empty_arrays = new();

        // Invoke the reflection entry point with the same argument ABI as the generated
        // proxy, but retain the exception pointer long enough to unwrap reflection's outer
        // TargetInvocationException. The generated proxy otherwise converts it to a managed
        // Il2CppException message before Navigator can access InnerException.
        internal static unsafe Il2CppSystem.Object? InvokeReflection(
            Il2CppSystem.Reflection.MethodBase method, Il2CppSystem.Object? target,
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object> arguments)
        {
            bool constructor = method is Il2CppSystem.Reflection.ConstructorInfo;
            IntPtr klass = constructor
                ? Il2CppClassPointerStore<Il2CppSystem.Reflection.ConstructorInfo>.NativeClassPtr
                : Il2CppClassPointerStore<Il2CppSystem.Reflection.MethodBase>.NativeClassPtr;
            IntPtr entry = IL2CPP.il2cpp_class_get_method_from_name(klass, "Invoke", constructor ? 1 : 2);
            if (entry == IntPtr.Zero)
                throw new MissingMethodException("The IL2CPP reflection Invoke entry point is unavailable.");

            void** nativeArguments = stackalloc void*[2];
            if (constructor)
                nativeArguments[0] = (void*)arguments.Pointer;
            else
            {
                nativeArguments[0] = (void*)(target?.Pointer ?? IntPtr.Zero);
                nativeArguments[1] = (void*)arguments.Pointer;
            }

            try
            {
                IntPtr exception = IntPtr.Zero;
                IntPtr result = IL2CPP.il2cpp_runtime_invoke(entry, method.Pointer, nativeArguments, ref exception);
                if (exception != IntPtr.Zero)
                {
                    var nativeException = new Il2CppSystem.Exception(exception);
                    var invocation = nativeException.TryCast<Il2CppSystem.Reflection.TargetInvocationException>();
                    Il2CppSystem.Exception cause = invocation?.InnerException ?? nativeException;
                    var managedException = new Il2CppException(cause.Pointer);
                    GC.KeepAlive(cause);
                    GC.KeepAlive(nativeException);
                    throw managedException;
                }
                return result == IntPtr.Zero ? null : Il2CppInterop.Runtime.Runtime.Il2CppObjectPool.Get<Il2CppSystem.Object>(result);
            }
            finally
            {
                GC.KeepAlive(method);
                GC.KeepAlive(target);
                GC.KeepAlive(arguments);
            }
        }

        internal static Il2CppType TypeOf(object value)
        {
            if (value is not Il2CppObjectBase obj)
                throw new ArgumentException("Managed-only objects do not have an IL2CPP runtime type.", nameof(value));
            return Il2CppInterop.Runtime.Il2CppType.TypeFromPointer(obj.ObjectClass, "IL2CPP object");
        }

        internal static bool SameType(Il2CppType? left, Il2CppType? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;
            return ClassOf(left) == ClassOf(right);
        }

        internal static bool SameObject(Il2CppObjectBase? left, Il2CppObjectBase? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;
            return left.Pointer == right.Pointer;
        }

        internal static IntPtr ClassOf(Il2CppType type)
        {
            IntPtr klass = IL2CPP.il2cpp_class_from_system_type(type.Pointer);
            if (klass == IntPtr.Zero)
                throw new ArgumentException($"Could not resolve an IL2CPP class for type {type}.", nameof(type));
            return klass;
        }

        internal static bool IsAssignableFrom(Il2CppType target, Il2CppType source)
        {
            return IL2CPP.il2cpp_class_is_assignable_from(ClassOf(target), ClassOf(source));
        }

        internal static Il2CppType? NullableUnderlyingType(Il2CppType type)
        {
            if (!type.IsGenericType)
                return null;
            Il2CppType def = type.GetGenericTypeDefinition();
            string name = def.FullName ?? def.Name;
            if (!name.StartsWith("System.Nullable`", StringComparison.Ordinal))
                return null;
            var args = type.GetGenericArguments();
            return args.Length == 1 ? args[0] : null;
        }

        internal static bool AllowsNull(Il2CppType type)
        {
            return !type.IsValueType || NullableUnderlyingType(type) != null;
        }

        internal static Il2CppSystem.Object? AsReference(object? value)
        {
            if (value == null)
                return null;
            if (value is Il2CppSystem.Object obj)
                return obj;
            if (value is Il2CppObjectBase ilBase)
                return new Il2CppSystem.Object(ilBase.Pointer);
            throw new ArgumentException("Managed-only objects cannot be used as IL2CPP target instances.", nameof(value));
        }

        internal static Il2CppSystem.Object? AsIl2CppObject(object? value, Il2CppType expectedType)
        {
            if (value == null)
                return null;

            Il2CppType? nullable = NullableUnderlyingType(expectedType);
            if (nullable != null)
                expectedType = nullable;

            if (value is Il2CppObjectBase ilBase)
            {
                Il2CppType actualType = TypeOf(ilBase);
                if (!IsAssignableFrom(expectedType, actualType))
                    throw new ArgumentException($"IL2CPP value of type {actualType} cannot be used as {expectedType}.", nameof(value));
                return value is Il2CppSystem.Object ilObject
                    ? ilObject
                    : new Il2CppSystem.Object(ilBase.Pointer);
            }

            // Use the exact primitive/string boxing operators supplied by the user's
            // Il2Cppmscorlib proxy assembly. Only the few types without an Object
            // conversion fall back to native value boxing below.
            if (value is string str) return str;
            if (value is bool boolean) return boolean;
            if (value is char character) return character;
            if (value is sbyte int8) return int8;
            if (value is byte uint8) return uint8;
            if (value is short int16) return int16;
            if (value is ushort uint16) return uint16;
            if (value is int int32) return int32;
            if (value is uint uint32) return uint32;
            if (value is long int64) return int64;
            if (value is ulong uint64) return uint64;
            if (value is float single) return single;
            if (value is double dbl) return dbl;

            return BoxManagedValue(value, expectedType);
        }

        /// <summary>
        /// Prepare a value for IL2CPP reflection invocation/assignment without pre-empting
        /// the runtime binder. Existing IL2CPP objects keep their actual runtime type, just as
        /// CLR reflection received the original object and performed the final conversion/check.
        /// Managed literal/default values still need boxing before they can cross the boundary.
        /// </summary>
        internal static Il2CppSystem.Object? AsInvocationObject(object? value, Il2CppType expectedType)
        {
            if (value == null)
                return null;
            if (value is Il2CppSystem.Object ilObject)
                return ilObject;
            if (value is Il2CppObjectBase ilBase)
                return new Il2CppSystem.Object(ilBase.Pointer);
            return AsIl2CppObject(value, expectedType);
        }

        internal static T RequireProxy<T>(object? value, string paramName) where T : Il2CppObjectBase
        {
            if (value is T typed)
                return typed;
            if (value is Il2CppObjectBase ilBase)
            {
                T? cast = ilBase.TryCast<T>();
                if (cast != null)
                    return cast;
            }
            throw new ArgumentException($"Value for {paramName} is not an IL2CPP {typeof(T).Name}.", paramName);
        }

        /// <summary>
        /// Converts a Navigator result to the text a user should see. Managed proxy classes inherit
        /// System.Object.ToString(), which only reports names such as Il2CppSystem.String. Native
        /// reference types can use the native Object.ToString(); boxed value types must invoke
        /// their runtime method via reflection so the runtime supplies the correct unboxed receiver.
        /// </summary>
        internal static string ToDisplayString(object? value)
        {
            if (value == null)
                return "null";
            if (value is Il2CppObjectBase ilObject)
            {
                Il2CppSystem.Object native = new(ilObject.Pointer);
                Il2CppType runtimeType = TypeOf(ilObject);
                if (runtimeType.IsValueType)
                {
                    foreach (Il2CppSystem.Reflection.MethodInfo method in runtimeType.GetMethods(
                        Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.Instance))
                    {
                        if (method.Name != "ToString" || method.GetParameters().Length != 0)
                            continue;
                        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object> args = new(0);
                        Il2CppSystem.Object? result = InvokeReflection(method, native, args);
                        return result == null ? "null" : IL2CPP.Il2CppStringToManaged(result.Pointer) ?? "null";
                    }
                }
                return native.ToString() ?? "null";
            }
            return value.ToString() ?? "null";
        }

        internal static string? ReadNullableString(object? value, string paramName)
            => value == null ? null : ReadString(value, paramName);

        internal static string ReadString(object? value, string paramName)
        {
            if (value is string managed)
                return managed;
            if (value is Il2CppObjectBase ilBase)
            {
                Il2CppType actual = TypeOf(ilBase);
                if (SameType(actual, Il2CppInterop.Runtime.Il2CppType.Of<string>()))
                    return IL2CPP.Il2CppStringToManaged(ilBase.Pointer);
            }
            throw new ArgumentException($"Value for {paramName} is not an IL2CPP string.", paramName);
        }

        internal static int ReadInt32(object? value, string paramName)
        {
            if (value is int managed)
                return managed;
            if (value is Il2CppObjectBase ilBase && SameType(TypeOf(ilBase), Il2CppInterop.Runtime.Il2CppType.Of<int>()))
                return ilBase.Unbox<int>();
            throw new ArgumentException($"Value for {paramName} is not an IL2CPP Int32.", paramName);
        }

        internal static bool ReadBoolean(object? value, string paramName)
        {
            if (value is bool managed)
                return managed;
            if (value is Il2CppObjectBase ilBase && SameType(TypeOf(ilBase), Il2CppInterop.Runtime.Il2CppType.Of<bool>()))
                return ilBase.Unbox<bool>();
            throw new ArgumentException($"Value for {paramName} is not an IL2CPP Boolean.", paramName);
        }

        internal static Il2CppSystem.Reflection.BindingFlags ReadBindingFlags(object? value, string paramName)
        {
            if (value is Il2CppSystem.Reflection.BindingFlags managed)
                return managed;
            if (value is Il2CppObjectBase ilBase &&
                SameType(TypeOf(ilBase), Il2CppInterop.Runtime.Il2CppType.Of<Il2CppSystem.Reflection.BindingFlags>()))
                return ilBase.Unbox<Il2CppSystem.Reflection.BindingFlags>();
            throw new ArgumentException($"Value for {paramName} is not an IL2CPP BindingFlags value.", paramName);
        }

        internal static Il2CppSystem.Object CreateDefault(Il2CppType type)
        {
            if (!type.IsValueType)
                throw new ArgumentException($"{type} is not a value type.", nameof(type));

            IntPtr klass = ClassOf(type);
            uint align = 0;
            int size = IL2CPP.il2cpp_class_value_size(klass, ref align);
            IntPtr data = Marshal.AllocHGlobal(Math.Max(size, 1));
            try
            {
                byte[] zero = new byte[Math.Max(size, 1)];
                Marshal.Copy(zero, 0, data, zero.Length);
                IntPtr boxed = IL2CPP.il2cpp_value_box(klass, data);
                if (boxed == IntPtr.Zero)
                    throw new InvalidOperationException($"IL2CPP failed to box the default value for {type}.");
                return new Il2CppSystem.Object(boxed);
            }
            finally
            {
                Marshal.FreeHGlobal(data);
            }
        }

        static Il2CppSystem.Object BoxManagedValue(object value, Il2CppType expectedType)
        {
            IntPtr klass = ClassOf(expectedType);
            uint align = 0;
            int nativeSize = IL2CPP.il2cpp_class_value_size(klass, ref align);
            IntPtr data = Marshal.AllocHGlobal(Math.Max(nativeSize, 1));
            try
            {
                Zero(data, nativeSize);
                WriteManagedValue(data, value, expectedType);
                IntPtr boxed = IL2CPP.il2cpp_value_box(klass, data);
                if (boxed == IntPtr.Zero)
                    throw new InvalidOperationException($"IL2CPP failed to box {value} as {expectedType}.");
                return new Il2CppSystem.Object(boxed);
            }
            finally
            {
                Marshal.FreeHGlobal(data);
            }
        }

        static void Zero(IntPtr ptr, int size)
        {
            if (size <= 0)
                return;
            Marshal.Copy(new byte[size], 0, ptr, size);
        }

        static void WriteManagedValue(IntPtr ptr, object value, Il2CppType expectedType)
        {
            string name = expectedType.FullName ?? expectedType.Name;
            switch (name)
            {
                case "System.Boolean": Marshal.WriteByte(ptr, (bool)value ? (byte)1 : (byte)0); return;
                case "System.Byte": Marshal.WriteByte(ptr, (byte)value); return;
                case "System.SByte": Marshal.WriteByte(ptr, unchecked((byte)(sbyte)value)); return;
                case "System.Int16": Marshal.WriteInt16(ptr, (short)value); return;
                case "System.UInt16": Marshal.WriteInt16(ptr, unchecked((short)(ushort)value)); return;
                case "System.Char": Marshal.WriteInt16(ptr, unchecked((short)(char)value)); return;
                case "System.Int32": Marshal.WriteInt32(ptr, (int)value); return;
                case "System.UInt32": Marshal.WriteInt32(ptr, unchecked((int)(uint)value)); return;
                case "System.Int64": Marshal.WriteInt64(ptr, (long)value); return;
                case "System.UInt64": Marshal.WriteInt64(ptr, unchecked((long)(ulong)value)); return;
                case "System.IntPtr": Marshal.WriteIntPtr(ptr, (IntPtr)(nint)value); return;
                case "System.UIntPtr": Marshal.WriteIntPtr(ptr, unchecked((IntPtr)(nint)(nuint)value)); return;
                case "System.Single":
                {
                    byte[] bytes = BitConverter.GetBytes((float)value);
                    Marshal.Copy(bytes, 0, ptr, bytes.Length);
                    return;
                }
                case "System.Double":
                {
                    byte[] bytes = BitConverter.GetBytes((double)value);
                    Marshal.Copy(bytes, 0, ptr, bytes.Length);
                    return;
                }
                case "System.Decimal":
                    Marshal.StructureToPtr((decimal)value, ptr, false);
                    return;
            }

            if (expectedType.IsEnum)
            {
                Il2CppType underlying = Il2CppSystem.Enum.GetUnderlyingType(expectedType);
                WriteEnumValue(ptr, value, underlying);
                return;
            }

            throw new NotSupportedException(
                $"Cannot marshal this managed value into IL2CPP value type {expectedType}. " +
                "Only primitive/enum literals are marshalled from managed memory; other value types must come from IL2CPP.");
        }

        static void WriteEnumValue(IntPtr ptr, object value, Il2CppType underlying)
        {
            string name = underlying.FullName ?? underlying.Name;
            switch (name)
            {
                case "System.SByte": Marshal.WriteByte(ptr, unchecked((byte)Convert.ToSByte(value))); return;
                case "System.Byte": Marshal.WriteByte(ptr, Convert.ToByte(value)); return;
                case "System.Int16": Marshal.WriteInt16(ptr, Convert.ToInt16(value)); return;
                case "System.UInt16": Marshal.WriteInt16(ptr, unchecked((short)Convert.ToUInt16(value))); return;
                case "System.Int32": Marshal.WriteInt32(ptr, Convert.ToInt32(value)); return;
                case "System.UInt32": Marshal.WriteInt32(ptr, unchecked((int)Convert.ToUInt32(value))); return;
                case "System.Int64": Marshal.WriteInt64(ptr, Convert.ToInt64(value)); return;
                case "System.UInt64": Marshal.WriteInt64(ptr, unchecked((long)Convert.ToUInt64(value))); return;
                default: throw new NotSupportedException($"Unsupported enum backing type {name}.");
            }
        }

        internal static Il2CppSystem.Array CreateArray(Il2CppType elementType, IReadOnlyList<object?> values)
        {
            Il2CppSystem.Array array = Il2CppSystem.Array.CreateInstance(elementType, values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                object? value = values[i];
                // IL2CPP arrays are already initialized to the element type's default value.
                // Preserve the original params-array behavior by leaving null/default-null slots
                // untouched instead of asking reflection to SetValue(null) into a value array.
                if (value == null)
                    continue;
                array.SetValue(AsInvocationObject(value, elementType), i);
            }
            return array;
        }

        internal static Il2CppSystem.Array CreateArray(Il2CppType elementType, int length)
        {
            return Il2CppSystem.Array.CreateInstance(elementType, length);
        }

        internal static Il2CppSystem.Array EmptyArray(Il2CppType elementType)
        {
            IntPtr key = ClassOf(elementType);
            lock (s_empty_arrays)
            {
                if (!s_empty_arrays.TryGetValue(key, out Il2CppSystem.Array? empty))
                {
                    empty = Il2CppSystem.Array.CreateInstance(elementType, 0);
                    s_empty_arrays[key] = empty;
                }
                return empty;
            }
        }

        internal static bool TryGetEnumerableCount(object? value, out int count)
        {
            if (value is ICollection managed)
            {
                count = managed.Count;
                return true;
            }

            if (value is Il2CppObjectBase ilObject)
            {
                Il2CppSystem.Collections.ICollection? collection = ilObject.TryCast<Il2CppSystem.Collections.ICollection>();
                if (collection != null)
                {
                    count = collection.Count;
                    return true;
                }
            }

            count = 0;
            return false;
        }

        internal static bool TryEnumerate(object? value, out IEnumerable<object?> enumerable)
        {
            if (value == null || value is string)
            {
                enumerable = Array.Empty<object?>();
                return false;
            }

            if (value is IEnumerable managed)
            {
                enumerable = EnumerateManaged(managed);
                return true;
            }

            if (value is Il2CppObjectBase ilObject)
            {
                Il2CppType runtimeType = TypeOf(ilObject);
                if ((runtimeType.FullName ?? runtimeType.Name) == "System.String")
                {
                    enumerable = Array.Empty<object?>();
                    return false;
                }

                // Prefer IList when available. This avoids relying on an IL2CPP enumerator for
                // arrays/List<T> and still feeds the exact same managed sequence into AppendMany.
                Il2CppSystem.Collections.IList? ilList = ilObject.TryCast<Il2CppSystem.Collections.IList>();
                if (ilList != null)
                {
                    enumerable = EnumerateIl2CppList(ilList);
                    return true;
                }

                Il2CppSystem.Collections.IEnumerable? ilEnumerable = ilObject.TryCast<Il2CppSystem.Collections.IEnumerable>();
                if (ilEnumerable != null)
                {
                    enumerable = EnumerateIl2Cpp(ilEnumerable);
                    return true;
                }
            }

            enumerable = Array.Empty<object?>();
            return false;
        }

        static IEnumerable<object?> EnumerateManaged(IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
                yield return item;
        }

        static IEnumerable<object?> EnumerateIl2CppList(Il2CppSystem.Collections.IList list)
        {
            int count = list.Cast<Il2CppSystem.Collections.ICollection>().Count;
            for (int i = 0; i < count; i++)
                yield return list[i];
        }

        static IEnumerable<object?> EnumerateIl2Cpp(Il2CppSystem.Collections.IEnumerable enumerable)
        {
            Il2CppSystem.Collections.IEnumerator enumerator = enumerable.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                    yield return enumerator.Current;
            }
            finally
            {
                enumerator.TryCast<Il2CppSystem.IDisposable>()?.Dispose();
            }
        }
    }
}
