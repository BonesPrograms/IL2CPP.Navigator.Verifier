using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using XQuinn.Extensions;
using XQuinn.LangInterp;
using XQuinn.LangInterp.SyntaxTree;
using XQuinn.Reflection;
using XQuinn.Runtime.NavigatorEngine;
using BindingFlags = Il2CppSystem.Reflection.BindingFlags;
using FieldInfo = Il2CppSystem.Reflection.FieldInfo;
using MemberInfo = Il2CppSystem.Reflection.MemberInfo;
using MethodBase = Il2CppSystem.Reflection.MethodBase;
using PropertyInfo = Il2CppSystem.Reflection.PropertyInfo;
using Type = Il2CppSystem.Type;

namespace XQuinn.Runtime
{
    internal sealed class NavigatorCore
    {
        internal readonly CallLexer _lexer = new();
        internal readonly Parser _parser;
        internal readonly Reflector _reflector;
        internal readonly Invoker _invoker;

        internal Il2CppObjectBase? _object;
        internal string? _variable;
        internal Type? _object_type => _object == null ? null : Il2CppRuntime.TypeOf(_object);
        internal Type? _static_type;
        internal TypeString? _implicit_this;

        internal readonly Dictionary<MethodKey, MethodBase> _methods = new();
        internal readonly Dictionary<string, FieldInfo> _fields = new(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, PropertyInfo> _props = new(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, VariableBinding> _variables = new(StringComparer.OrdinalIgnoreCase);

        bool _chaining;
        public bool StackTrace;

        internal const BindingFlags Flag = BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase |
                                           BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance;

        public NavigatorCore()
        {
            // Ensure the XQuinn Types bridge exists in the IL2CPP domain before navigation starts.
            Types.RegisterInIl2Cpp();
            _parser = new Parser(this);
            _reflector = new Reflector(this);
            _invoker = new Invoker(this);
        }

        public object? Interface(string invocation)
        {
            char controller = default;
            int substring = -1;
            for (int i = 0; i < invocation.Length; i++)
            {
                if (invocation[i] != ' ')
                {
                    controller = invocation[i];
                    substring = i + 1;
                    break;
                }
            }
            if (substring == -1)
                return "No instruction detected.";

            return controller switch
            {
                '+' => AddVariable(invocation.Substring(substring)),
                '-' => RemoveVariable(invocation.Substring(substring)),
                '@' => LoadTypeStatic(invocation.Substring(substring)),
                '*' => LoadInstance(invocation.Substring(substring)),
                '^' => CastInstance(invocation.Substring(substring)),
                '~' => ChainInvoke(invocation.Substring(substring).Split(';')),
                '?' => Query(invocation.Substring(substring)),
                _ => InvokeOrAssign(invocation)
            };
        }

        IEnumerable<string> Query(string invocation)
        {
            invocation = invocation.Trim();
            if (invocation.EqualsCaseless("vars"))
                return _variables.Select(x => $"Key: {x.Key} :: {x.Value}");

            if (_static_type == null)
                throw new InvalidOperationException("No loaded type to query. Use the static methods Fields/Methods/Props in class Types to query unloaded types.");

            if (MiniLexer.ImplicitThisMethodCall(invocation))
            {
                MethodString query = _lexer.MethodTemplate(invocation, _implicit_this!, _implicit_this);
                if (query.Name.EqualsCaseless("methods")) return ParseQuery(_methods, query);
                if (query.Name.EqualsCaseless("fields")) return ParseQuery(_fields, query);
                if (query.Name.EqualsCaseless("props")) return ParseQuery(_props, query);
            }
            else
            {
                if (invocation.EqualsCaseless("methods")) return Types.ReadMembers(_methods, null, Flag, _static_type);
                if (invocation.EqualsCaseless("fields")) return Types.ReadMembers(_fields, null, Flag, _static_type);
                if (invocation.Equals("props")) return Types.ReadMembers(_props, null, Flag, _static_type);
            }
            throw new ArgumentException("Invalid query");
        }

        IEnumerable<string> ParseQuery<K, V>(Dictionary<K, V> dictionary, MethodString query)
            where K : notnull where V : MemberInfo
        {
            if (query.Params.Count > 2)
                throw new System.Reflection.TargetParameterCountException("Local query is max 2 params: a containig string and bindingflags search flags.");

            string? contains = null;
            if (query.Params.Count >= 1)
                contains = ParseManagedString(query.Params[0]);

            BindingFlags flags = Flag;
            if (query.Params.Count == 2)
            {
                object? parsed = _parser.ParameterToObject(query.Params[1], Il2CppType.Of<BindingFlags>());
                if (parsed is BindingFlags managedFlags)
                    flags = managedFlags;
                else if (parsed is Il2CppObjectBase boxedFlags &&
                         Il2CppRuntime.SameType(Il2CppRuntime.TypeOf(boxedFlags), Il2CppType.Of<BindingFlags>()))
                    flags = boxedFlags.Unbox<BindingFlags>();
                else
                    throw new InvalidCastException($"Query BindingFlags argument returned {parsed?.GetType().ToString() ?? "null"}.");
            }
            return Types.ReadMembers(dictionary, contains, flags, _static_type);
        }

        string? ParseManagedString(ParameterString parameter)
        {
            object? value = _parser.ParameterToObject(parameter, Il2CppType.Of<string>());
            if (value == null)
                return null;
            if (value is string text)
                return text;
            if (value is Il2CppObjectBase obj &&
                Il2CppRuntime.SameType(Il2CppRuntime.TypeOf(obj), Il2CppType.Of<string>()))
                return IL2CPP.Il2CppStringToManaged(obj.Pointer);
            throw new InvalidCastException($"Query string argument returned {value.GetType()}.");
        }

        List<string> ChainInvoke(params string[] commands)
        {
            if (_chaining)
                throw new NotSupportedException("Cannot invoke nested chains.");
            _chaining = true;
            List<string> invocations = new(commands.Length);
            for (int i = 0; i < commands.Length; i++)
            {
                string cmd = commands[i];
                try
                {
                    object? ret = Interface(cmd);
                    if (ret is string s && s == "No instruction detected.")
                        continue;
                    invocations.Add($"[Instruction: {cmd} :: Returned: {Il2CppRuntime.ToDisplayString(ret)}]");
                }
                catch (Exception ex)
                {
                    _chaining = false;
                    StringBuilder sb = new();
                    sb.CatchException(ex, StackTrace);
                    invocations.Insert(0, sb.ToString());
                    invocations.Add($"!!! EXCEPTION on [Instructinon: {cmd}]");
                    return invocations;
                }
            }
            _chaining = false;
            return invocations;
        }

        public Type LoadTypeStatic(string typeName)
        {
            TypeString typeString = TypeString.New(typeName.Trim());
            Type type = _reflector.FindType(typeString, true);
            _implicit_this = typeString;
            LoadTypeMembers(type);
            _object = null;
            _variable = null;
            return type;
        }

        void LoadInstance(object instance)
        {
            if (instance is not Il2CppObjectBase ilObject)
                throw new ArgumentException("Only IL2CPP runtime objects can be loaded.", nameof(instance));
            _object = ilObject;
            LoadTypeMembers(_object_type!);
            _implicit_this = TypeString.s_this;
        }

        void LoadTypeMembers(Type type)
        {
            _static_type = type;
            TypeMap.MapType(_methods, _fields, type, _props);
        }

        object LoadInstance(string invocation)
        {
            invocation = invocation.Trim();
            object? instance = null;
            VariableBinding? variable = null;
            _variable = null;
            if (_variables.TryGetValue(invocation, out variable))
            {
                _variable = invocation;
                instance = variable.Object;
            }
            instance ??= InvokeOrAssign(invocation)
                ?? throw new ArgumentException($"Failed to load new instance from {invocation}, invocation returned null!");
            if (instance is VariableBinding binding)
                instance = binding.Object;
            LoadInstance(instance);
            return variable ?? instance;
        }

        object? InvokeOrAssign(string invocation)
        {
            if (Assignment(invocation, out object? assigned))
                return assigned;

            string typeName = MiniLexer.ResolveMemberAccessOrLiteral(invocation, out string member, out bool field)
                ?? _implicit_this?.StringID
                ?? throw new ArgumentException($"No type loaded to return fields from, or no type name given for isolated invocation.");

            if (typeName != _implicit_this?.StringID)
                typeName = typeName.Trim();

            if (!field)
            {
                TypeString declaringType = ThisOrNew(typeName);
                MethodString method = _lexer.MethodTemplate(member, declaringType, _implicit_this);
                return _invoker.InvokeMethod(method);
            }

            member = member.Trim();
            if (member.EqualsCaseless("this"))
                return (object?)_object ?? "null";
            if (_variables.TryGetValue(member, out VariableBinding? variable))
                return variable;
            TypeString declaring = ThisOrNew(typeName);
            return _invoker.InvokeFieldOrProperty(new FieldString(member, declaring));
        }

        Type CastInstance(string invocation)
        {
            if (_object == null)
                throw new InvalidOperationException("Cannot cast, instance is null.");
            TypeString typeString = TypeString.New(invocation.Trim());
            Type type = _reflector.FindType(typeString, true);
            _implicit_this = typeString;
            if (!Il2CppRuntime.IsAssignableFrom(type, _object_type!))
                throw new InvalidCastException($"{_object_type} cannot cast to {type}.");
            LoadTypeMembers(type);
            return type;
        }

        bool Assignment(string invocation, out object? assignedValue)
        {
            assignedValue = null;
            if (!MiniLexer.AssignmentSubstring(invocation, out string? left, out string? right))
                return false;

            string leftHand = left!;
            string rightHand = right!;
            string? leftTypeName = MiniLexer.ResolveMemberAccessOrLiteral(leftHand, out leftHand, out bool leftIsField);
            leftHand = leftHand.Trim();
            rightHand = rightHand.Trim();
            if (!leftIsField)
                throw new ArgumentException($"Can only assign to fields or properties. Bad input: {leftHand}");

            Type leftType;
            Il2CppObjectBase? leftInstance;
            if (leftTypeName == null)
            {
                leftType = _static_type ?? throw new ArgumentException($"There is no loaded type to assign fields to. Bad input: {invocation}");
                leftInstance = _object;
            }
            else
            {
                TypeString typeString = ThisOrNew(leftTypeName);
                leftType = _reflector.FindReference(new FieldString(leftHand, typeString), out leftInstance);
            }

            if (!Il2CppRuntime.SameType(leftType, _static_type) && !leftType.IsClass)
                throw new NotSupportedException($"Assigning to the members of struct fields is currently unsupported due to constraints related to boxing.See \"Assignment\" in public API doc for more info.");

            Reflector.Assignment assignment;
            FieldInfo? field = leftType.GetField(leftHand, Flag);
            if (field != null)
                assignment = new Reflector.Assignment(field);
            else
            {
                PropertyInfo? prop = leftType.GetProperty(leftHand, Flag)
                    ?? throw new MissingMemberException($"No field or property found named {leftHand} in {leftType}.");
                assignment = new Reflector.Assignment(prop);
            }

            string? rightTypeName = MiniLexer.ResolveMemberAccessOrLiteral(rightHand, out rightHand, out bool rightIsField);
            rightHand = rightHand.Trim();
            if (rightTypeName != null)
            {
                TypeString typeString = ThisOrNew(rightTypeName.Trim());
                if (rightIsField)
                    assignedValue = _invoker.InvokeFieldOrProperty(new FieldString(rightHand, typeString));
                else
                    assignedValue = _invoker.InvokeMethod(_lexer.MethodTemplate(rightHand, typeString, _implicit_this));
            }
            else if (!rightIsField && MiniLexer.ImplicitThisMethodCall(rightHand))
            {
                if (_static_type == null)
                    throw new ArgumentException("Cannot perform implicit this call, no type is loaded.");
                assignedValue = _invoker.InvokeMethod(_lexer.MethodTemplate(rightHand, _implicit_this!, _implicit_this));
            }
            else
                assignedValue = _parser.ParseValue(new ValueString(rightHand), assignment.ObjectType);

            assignment.SetValue(_invoker.TargetInstance(leftType, leftInstance), assignedValue);
            return true;
        }

        string RemoveVariable(string key)
        {
            key = key.Trim();
            if (key.EqualsCaseless(_variable))
                _variable = null;
            return _variables.Remove(key) ? $"{key} removed from variables." : $"No variable named {key}.";
        }

        string AddVariable(string key)
        {
            if (_object == null)
                throw new InvalidOperationException("No instance is loaded.");
            key = key.Trim();
            TypeRegister.ThrowIfBadKey(key);
            if (TypeRegister.s_registry.ContainsKey(new GenericKey(key)))
                throw new ArgumentException($"Key {key} is taken by a cached type, and cannot be used as a name for a local variable. Names are not case sensitive.");
            if (_variables.TryGetValue(key, out VariableBinding? variable))
            {
                if (!Il2CppRuntime.SameObject(_object, variable.Object))
                    throw new ArgumentException("Duplicate keyname detected.");
                return $"The current instance already is a variable named {key}.";
            }
            _variables[key] = new VariableBinding(_object);
            _variable = key;
            return $"Added current instance as variable {key}.";
        }

        public static void FlushStaticCache(bool ambiguousMatches = false, bool typeMembers = true, bool reifiedGenerics = true)
            => RuntimeCache.FlushStaticCache(ambiguousMatches, typeMembers, reifiedGenerics);

        public void Clear()
        {
            _variables.Clear();
            _methods.Clear();
            _fields.Clear();
            _props.Clear();
            _object = null;
            _static_type = null;
            _implicit_this = null;
            _chaining = false;
            _variable = null;
        }

        TypeString ThisOrNew(string typeName)
            => typeName.EqualsCaseless(_implicit_this?.StringID) ? _implicit_this! : TypeString.New(typeName);
    }
}
