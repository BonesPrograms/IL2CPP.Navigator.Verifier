using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Fields;

namespace XQuinn.NativeVerification
{
    // This fixture is deliberately not part of the production Navigator assembly.
    public sealed class InjectedProbe : Il2CppSystem.Object
    {
        // ClassInjector initializes these field accessors on its managed and native construction paths.
        public Il2CppValueField<int> Number = null!;
        public Il2CppStringField Label = null!;
        public Il2CppReferenceField<Il2CppSystem.Collections.Generic.List<int>> Items = null!;
        public int ManagedOnly;

        public InjectedProbe(IntPtr pointer) : base(pointer) { }

        public InjectedProbe() : base(ClassInjector.DerivedConstructorPointer<InjectedProbe>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }

        public int Add(int value)
        {
            Number.Value += value;
            return Number.Value;
        }

        public int ArrayLength(Il2CppSystem.Array values) => values.Length;

        // Compare CLR and IL2CPP reflection: managed T[] is not a supported injected signature.
        public int SumParams(params int[] values)
        {
            int sum = 0;
            foreach (int value in values) sum += value;
            return sum;
        }
    }
}