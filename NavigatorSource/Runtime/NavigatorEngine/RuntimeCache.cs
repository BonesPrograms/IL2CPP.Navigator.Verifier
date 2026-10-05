using System;
using System.Collections.Generic;
using MemberInfo = Il2CppSystem.Reflection.MemberInfo;
using MethodBase = Il2CppSystem.Reflection.MethodBase;
using Type = Il2CppSystem.Type;

namespace XQuinn.Runtime.NavigatorEngine
{
    internal static class RuntimeCache
    {
        static readonly Dictionary<IntPtr, HashSet<string>> s_ambiguous_matches = new();
        static readonly Dictionary<IntPtr, Dictionary<string, MemberInfo>> s_global_metadata = new();
        internal static readonly Dictionary<string, Type> s_reified_generic_types = new(StringComparer.OrdinalIgnoreCase);

        // Methods and fields/properties may legally share a name across an inheritance chain.
        // Keep their cache namespaces separate so looking up Foo() cannot return a cached Foo
        // field/property (or vice versa). Fields and properties intentionally remain together,
        // matching InvokeFieldOrProperty's field-first resolution contract.
        internal static string CacheKey<T>(string key) where T : MemberInfo
            => CacheKey(key, typeof(MethodBase).IsAssignableFrom(typeof(T)));

        static string CacheKey(string key, MemberInfo member)
            => CacheKey(key, member is MethodBase);

        static string CacheKey(string key, bool method)
            => (method ? "method:" : "value:") + key;

        static IntPtr Key(Type type) => Il2CppRuntime.ClassOf(type);

        static IntPtr AmbiguousKey(Type type)
        {
            Type cacheType = type.IsGenericType && !type.IsGenericTypeDefinition
                ? type.GetGenericTypeDefinition()
                : type;
            return Key(cacheType);
        }

        internal static bool CheckAmbiguousMatch(Type fromType, string methodName, out IntPtr cachedType, out HashSet<string>? cachedMatches)
        {
            cachedType = AmbiguousKey(fromType);
            s_ambiguous_matches.TryGetValue(cachedType, out cachedMatches);
            return cachedMatches?.Contains(methodName) ?? false;
        }

        internal static void CacheAmbiguousMatch(HashSet<string>? cachedMatches, IntPtr cachedType, string methodName)
        {
            if (cachedMatches == null)
            {
                cachedMatches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                s_ambiguous_matches[cachedType] = cachedMatches;
            }
            cachedMatches.Add(methodName);
        }

        public static void FlushStaticCache(bool ambiguousMatches = false, bool typeMembers = true, bool reifiedGenerics = true)
        {
            if (ambiguousMatches) s_ambiguous_matches.Clear();
            if (typeMembers) s_global_metadata.Clear();
            if (reifiedGenerics) s_reified_generic_types.Clear();
        }

        internal static T? FromCache<T>(string key, Type fromType, out bool typeCached, out bool memberCached)
            where T : MemberInfo
        {
            memberCached = false;
            typeCached = s_global_metadata.TryGetValue(Key(fromType), out Dictionary<string, MemberInfo>? cachedMembers);
            if (typeCached)
            {
                memberCached = cachedMembers!.TryGetValue(CacheKey<T>(key), out MemberInfo? member);
                if (memberCached)
                    return (T)member!;
            }
            return null;
        }

        internal static void CacheMember(bool typeCached, bool memberCached, Type fromType, MemberInfo member, string key)
        {
            if (memberCached)
                return;

            IntPtr typeKey = Key(fromType);
            if (!typeCached || !s_global_metadata.TryGetValue(typeKey, out Dictionary<string, MemberInfo>? cachedMembers))
            {
                cachedMembers = new Dictionary<string, MemberInfo>(StringComparer.OrdinalIgnoreCase);
                s_global_metadata[typeKey] = cachedMembers;
            }
            cachedMembers[CacheKey(key, member)] = member;
        }
    }
}
