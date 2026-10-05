using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using XQuinn.Runtime;
using FieldInfo = Il2CppSystem.Reflection.FieldInfo;
using PropertyInfo = Il2CppSystem.Reflection.PropertyInfo;
using Type = Il2CppSystem.Type;

namespace XQuinn.Runtime.NavigatorEngine
{
    internal abstract class CoreObject
    {
        protected readonly NavigatorCore _core;
        protected Il2CppObjectBase? _object => _core._object;
        protected Dictionary<string, FieldInfo> _fields => _core._fields;
        protected Dictionary<string, PropertyInfo> _props => _core._props;
        protected Dictionary<string, VariableBinding> _variables => _core._variables;

        protected CoreObject(NavigatorCore navig) => _core = navig;
    }

    internal abstract class ExpandedCoreObject : CoreObject
    {
        protected Type? _instanceType => _core._object_type;
        protected Type? _loadedType => _core._static_type;
        protected ExpandedCoreObject(NavigatorCore navig) : base(navig) { }
    }
}
