using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using XQuinn.BepInEx.Chatbot;
using XQuinn.Reflection;
using XQuinn.Runtime;
using XQuinn.Runtime.NavigatorEngine;
using Type = Il2CppSystem.Type;
using BindingFlags = Il2CppSystem.Reflection.BindingFlags;
using MemberInfo = Il2CppSystem.Reflection.MemberInfo;
using MethodBase = Il2CppSystem.Reflection.MethodBase;
using PropertyInfo = Il2CppSystem.Reflection.PropertyInfo;

namespace XQuinn.NativeVerification
{
    // Optional standalone test plugin. Do not load alongside another copy of XQuinn,
    // because injected native type names are global to the IL2CPP process.
    [BepInPlugin("xquinn.navigator.native-verification", "IL2CPP Navigator Verifier", "1.1.0")]
    [BepInIncompatibility("xquinn.navigator")]
    public sealed class Plugin : BasePlugin
    {
        int passed, failed;
        NavigatorCore core = null!;
        Harmony harmony = null!;

        public override void Load()
        {
            string navigatorLogPath = Path.Combine(Paths.BepInExRootPath, "NavigatorLog.log");
            if (File.Exists(navigatorLogPath))
                File.Delete(navigatorLogPath);

            HostRegistrationResult registration = default;
            Test("Vietnam War and curated Unity type registration", () =>
            {
                registration = HostTypeRegistration.Register(Log.LogWarning);
                Check(registration.GameTypeCount > 1000, "Assembly-CSharp type enumeration was unexpectedly small");
                Check(registration.UnityTypeCount >= 75, "fewer than 75 curated Unity types were available in this game: " + registration.UnityTypeCount);
                Check(registration.CachedTypeCount > 0, "host registration cached no new types");
                Check(TypeRegister.Contains("ChatBot"), "Assembly-CSharp ChatBot was not cached");
                Check(TypeRegister.Contains("GameObject"), "curated Unity GameObject was not cached");
                Check(TypeRegister.Contains("TypeRegister"), "privileged TypeRegister facade was not cached");
            });

            Test("Navigator and registry initialization", () =>
            {
                core = new NavigatorCore();
                Check(TypeRegister.GetTypeOrThrow("Types") != null, "Types not registered");
                Check(Il2CppRuntime.SameType(TypeRegister.GetTypeOrThrow("Decimal"), Il2CppType.Of<Il2CppSystem.Decimal>()), "Decimal type mismatch");
            });
            if (core == null)
            {
                Log.LogError("NATIVE VERIFICATION BLOCKED: Navigator initialization failed.");
                return;
            }

            Test("Duplicate retention and nested-type quiet skip", () =>
            {
                Check(registration.SkippedTypeCount == TypeRegister.SkippedTypes.Count, "skipped-type count mismatch");
                Check(TypeRegister.SkippedTypes.Any(entry =>
                    entry.key.Equals("Object", StringComparison.OrdinalIgnoreCase) &&
                    entry.type.FullName == "UnityEngine.Object"), "UnityEngine.Object duplicate was not retained");
                Check(ReadInt("TypeRegister.SkippedTypeCount()") == TypeRegister.SkippedTypes.Count,
                    "Navigator TypeRegister facade reported the wrong skipped count");

                var types = Il2CppSystem.Reflection.Assembly.Load("Assembly-CSharp").GetTypes();
                Type? nested = null;
                for (int i = 0; i < types.Length; i++)
                {
                    if (types[i].IsNested)
                    {
                        nested = types[i];
                        break;
                    }
                }
                Check(nested != null, "Assembly-CSharp exposed no nested type for the quiet-skip check");
                int cachedBefore = TypeRegister.Values.Count;
                List<(string key, Type type)> nestedSkipped =
                    TypeRegister.CacheTypesSkipDuplicates(new[] { nested! }, fullname: false);
                Check(TypeRegister.Values.Count == cachedBefore, "nested type changed the registry");
                Check(nestedSkipped.Count == 0, "nested type was incorrectly recorded as a duplicate");
            });

            Test("ChatBot Harmony patches install", () =>
            {
                NavigationFeed navigator = CommandChatbot.Navigator;
                Check(ReferenceEquals(navigator, CommandChatbot.Navigator),
                    "CommandChatbot did not retain one static NavigationFeed instance");
                harmony = new Harmony(ChatbotNavigatorHooks.HarmonyId);
                harmony.PatchAll(typeof(Plugin).Assembly);

                MethodInfo chatInput = AccessTools.Method(typeof(ChatBot), nameof(ChatBot.ChatInput)) ??
                    throw new MissingMethodException(typeof(ChatBot).FullName, nameof(ChatBot.ChatInput));
                MethodInfo compare = AccessTools.Method(typeof(ChatBot), "CompareChatWords") ??
                    throw new MissingMethodException(typeof(ChatBot).FullName, "CompareChatWords");
                Check(Harmony.GetPatchInfo(chatInput)?.Owners.Contains(ChatbotNavigatorHooks.HarmonyId) == true,
                    "ChatInput prefix was not installed");
                Check(Harmony.GetPatchInfo(compare)?.Owners.Contains(ChatbotNavigatorHooks.HarmonyId) == true,
                    "CompareChatWords prefix was not installed");
            });

            Test("Leading bang routes to Thor once", () =>
            {
                string chat = "!thor-test";
                ChatbotStringSnatcher.StringSnatcher(ref chat);
                Check(chat == "thor-test", "leading bang was not removed from the original chat argument");
                Check(ChatbotStringSnatcher.Chat == "thor-test", "stripped chat was not captured");
                Check(ChatbotStringSnatcher.IsThorCommand, "Thor flag was not set");
                Check(CommandChatbot.Check(), "Thor command did not allow the original method");
                Check(!ChatbotStringSnatcher.IsThorCommand, "Thor flag was not reset after consumption");
            });

            Test("Non-leading bang routes to Navigator and log", () =>
            {
                string instruction = "Types.String(\"a!b\")";
                ChatbotStringSnatcher.StringSnatcher(ref instruction);
                Check(instruction == "Types.String(\"a!b\")", "non-leading bang changed the chat argument");
                Check(!ChatbotStringSnatcher.IsThorCommand, "non-leading bang incorrectly set the Thor flag");
                Check(!CommandChatbot.Check(), "Navigator input did not suppress the original CompareChatWords method");
                Check(ReturnedLines(CommandChatbot.LastNavigatorOutput).SequenceEqual(new[] { "a!b" }),
                    "SafeInterface did not render the returned string value exactly");
                Check(CommandChatbot.LogPath == navigatorLogPath, "Navigator logger path was incorrect");
                string logText = ReadAllTextShared(navigatorLogPath);
                Check(logText.Contains("Types.String(\"a!b\")", StringComparison.Ordinal),
                    "Navigator instruction was not written to NavigatorLog.log");
                Check(ReturnedLines(logText).SequenceEqual(new[] { "a!b" }),
                    "NavigatorLog.log did not contain the returned string value exactly");
            });

            Test("Types.Of<int>", () => Check(Il2CppRuntime.SameType((Type)core.Interface("Types.Of<int>()")!, Il2CppType.Of<int>()), "wrong type"));
            Test("Types.Num default", () => Check(ReadInt("Types.Num<int>()") == 0, "wrong default"));
            Test("Types.Num values and cached call", () =>
            {
                Check(ReadInt("Types.Num<int>(42)") == 42, "wrong first result");
                Check(ReadInt("Types.Num<int>(43)") == 43, "stale cached argument");
            });
            Test("Native non-blittable value-type default", () =>
            {
                object value = core.Interface("Types.Num<KVP<int,String>>()")!;
                Check(Il2CppRuntime.TypeOf(value).IsValueType, "not a native value type");
            });
            Test("Decimal literal", () => Check(Il2CppRuntime.SameType(Il2CppRuntime.TypeOf(core.Interface("Types.Num<Decimal>(1.25)")!), Il2CppType.Of<Il2CppSystem.Decimal>()), "wrong decimal type"));
            Test("Num constraint remains enforced", () => Throws<ArgumentException>(() => core.Interface("Types.Num<String>()")));
            Test("Enum OR", () => Check(Il2CppRuntime.ReadBindingFlags(core.Interface("Types.Enum<BindingFlags>(Public|Instance)"), "flags") == (BindingFlags.Public | BindingFlags.Instance), "wrong enum value"));
            Test("String null", () => Check(core.Interface("Types.String(null)") == null, "null was not preserved"));
            Test("String value", () => Check(Il2CppRuntime.ReadString(core.Interface("Types.String(\"a,b\")"), "value") == "a,b", "wrong string"));
            Test("Generic metadata query", () =>
            {
                core.Interface("@Types");
                var rows = (IEnumerable<string>)core.Interface("?methods")!;
                var text = string.Join("\n", rows);
                Check(text.Contains("Num<T>") && text.Contains("params T[] arr"), "generic helpers missing from query");
            });
            Test("Generic member query", () => Check(Il2CppRuntime.TryEnumerate(core.Interface("Types.Fields<int>()"), out _), "query did not return an enumerable"));
            string? packed = null, sized = null;
            Test("Resolve both Array<T> overloads", () =>
            {
                var methods = Map(TypeRegister.GetTypeOrThrow("Types"));
                foreach (var pair in methods)
                {
                    if (!pair.Key.GKey.Name.Equals("Array", StringComparison.OrdinalIgnoreCase) || pair.Key.GKey.Args != 1) continue;
                    string key = "Types." + CallName(pair.Key, "int");
                    var parameters = pair.Value.GetParameters();
                    if (Il2CppRuntime.SameType(parameters[0].ParameterType, Il2CppType.Of<int>())) sized = key;
                    else packed = key;
                }
                Check(packed != null && sized != null, "Array overload missing");
            });
            if (packed != null && sized != null)
            {
                Test("Expanded params", () =>
                {
                    var array = Il2CppRuntime.RequireProxy<Il2CppSystem.Array>(core.Interface(packed + "(1,2,3)"), "array");
                    Check(array.Length == 3 &&
                        Enumerable.Range(0, 3).Select(i => Il2CppRuntime.ReadInt32(array.GetValue(i), "item"))
                            .SequenceEqual(new[] { 1, 2, 3 }), "bad params elements");
                });
                Test("Single expanded params element", () =>
                {
                    var array = Il2CppRuntime.RequireProxy<Il2CppSystem.Array>(core.Interface(packed + "(7)"), "array");
                    Check(array.Length == 1 && Il2CppRuntime.ReadInt32(array.GetValue(0), "item") == 7,
                        "single params element was lost or interpreted as an array size");
                });
                Test("Empty params", () => Check(Il2CppRuntime.RequireProxy<Il2CppSystem.Array>(core.Interface(packed + "()"), "array").Length == 0, "bad empty array"));
                Test("Explicit null params", () => Throws<NullReferenceException>(() => core.Interface(packed + "(null)")));
                Test("Sized array", () => Check(Il2CppRuntime.RequireProxy<Il2CppSystem.Array>(core.Interface(sized + "(4)"), "array").Length == 4, "bad array size"));
                Test("String params preserve punctuation and display values", () =>
                {
                    string input = packed.Replace("<int>", "<String>") + "(\"a,b\",\"c!d\")";
                    var array = Il2CppRuntime.RequireProxy<Il2CppSystem.Array>(core.Interface(input), "array");
                    Check(array.Length == 2 && Il2CppRuntime.ReadString(array.GetValue(0), "item") == "a,b" &&
                        Il2CppRuntime.ReadString(array.GetValue(1), "item") == "c!d", "string params lost element values");
                    string[] rendered = ReturnedLines(CommandChatbot.Navigator.SafeInterface(input));
                    Check(rendered.SequenceEqual(new[] { "[0] a,b", "[1] c!d" }),
                        "string params enumerable did not display values: " + string.Join(" | ", rendered));
                });
                Test("Variables and CastInstance", () =>
                {
                    core.Interface("*" + packed + "(1,2,3)");
                    core.Interface("+native_smoke_array");
                    core.Interface("^Array");
                    core.Interface("*native_smoke_array");
                    Check(Il2CppRuntime.RequireProxy<Il2CppSystem.Array>(core.Interface("this"), "array").Length == 3, "variable lost instance");
                    core.Interface("-native_smoke_array");
                });
                Test("Native array enumeration", () =>
                {
                    var value = core.Interface(packed + "(10,20)");
                    Check(Il2CppRuntime.TryEnumerate(value, out var sequence), "array not enumerable");
                    Check(sequence.Select(x => Il2CppRuntime.ReadInt32(x, "item")).SequenceEqual(new[] { 10, 20 }), "array enumeration mismatch");
                    string output = CommandChatbot.Navigator.SafeInterface(packed + "(10,20)");
                    Check(ReturnedLines(output).SequenceEqual(new[] { "[0] 10", "[1] 20" }),
                        "AppendMany did not render native enumerable element values: " +
                        string.Join(" | ", ReturnedLines(output).Select(line => "[" + line + "]")) +
                        "; full output: " + output.Replace(Environment.NewLine, "\\n"));
                });
            }
            Test("Native constructor invocation", () => Check(Il2CppRuntime.SameType(Il2CppRuntime.TypeOf(core.Interface("Object.new()")!), Il2CppType.Of<Il2CppSystem.Object>()), "constructor returned wrong type"));
            Test("Native properties and nonpublic instance fields", () =>
            {
                Type version = Il2CppType.Of<Il2CppSystem.Version>();
                TypeRegister.CacheType(version, "NativeSmokeVersion");
                string ctor = FindCall(version, "new", Il2CppType.Of<int>(), Il2CppType.Of<int>());
                core.Interface("@NativeSmokeVersion");
                Check(core._props.ContainsKey("Major"), "read-only property missing without an instance");
                string? majorField = core._fields.Values.FirstOrDefault(f => !f.IsStatic &&
                    f.Name.EndsWith("Major", StringComparison.OrdinalIgnoreCase))?.Name;
                Check(majorField != null, "nonpublic instance field missing without an instance");
                core.Interface("*NativeSmokeVersion." + ctor + "(1,2)");
                Check(ReadInt("Major") == 1, "native property read failed");
                Check(ReadInt(majorField!) == 1, "nonpublic field read failed");
                Throws<Exception>(() => core.Interface("Major = 9"));
                Check(ReadInt("Major") == 1, "failed setter modified read-only property");
            });
            Test("Native method invocation", () =>
            {
                core.Interface("*Types.String(\"abc\")");
                string method = FindCall(Il2CppType.Of<string>(), "Contains", Il2CppType.Of<string>());
                Check(Il2CppRuntime.ReadBoolean(core.Interface(method + "(\"b\")"), "contains"), "native method returned wrong result");
            });
            Test("Variables as arguments and instance member receivers", () =>
            {
                core.Interface("*Types.String(\"a!b\")");
                Check((string)core.Interface("+native_smoke_text")! == "Added current instance as variable native_smoke_text.",
                    "failed to create string variable");
                try
                {
                    Check(Il2CppRuntime.ReadString(core.Interface("Types.String(native_smoke_text)"), "value") == "a!b",
                        "variable was not passed as an argument");
                    string contains = FindCall(Il2CppType.Of<string>(), "Contains", Il2CppType.Of<string>());
                    Check(Il2CppRuntime.ReadBoolean(core.Interface("native_smoke_text." + contains + "(\"!\")"), "result"),
                        "instance method invoked through variable returned the wrong result");
                    Check(Il2CppRuntime.ReadInt32(core.Interface("native_smoke_text.Length"), "length") == 3,
                        "instance property accessed through variable returned the wrong result");
                    core.Interface("*native_smoke_text");
                    Check(Il2CppRuntime.ReadString(core.Interface("this"), "instance") == "a!b",
                        "loading an instance from its variable lost the value");
                }
                finally
                {
                    Check((string)core.Interface("-native_smoke_text")! == "native_smoke_text removed from variables.",
                        "failed to remove the variable");
                }
            });
            Test("Invoking methods through property and field members", () =>
            {
                Type exceptionType = Il2CppType.Of<Il2CppSystem.Exception>();
                TypeRegister.CacheType(exceptionType, "NativeSmokeException");
                string withMessage = FindCall(exceptionType, "new", Il2CppType.Of<string>());
                string withInner = FindCall(exceptionType, "new", Il2CppType.Of<string>(), exceptionType);
                core.Interface("*NativeSmokeException." + withMessage + "(\"inner-message\")");
                core.Interface("+native_smoke_inner");
                try
                {
                    core.Interface("*NativeSmokeException." + withInner + "(\"outer-message\",native_smoke_inner)");
                    Check(core._props.ContainsKey("InnerException") && core._fields.ContainsKey("_innerException"),
                        "exception property or backing field missing from the loaded instance");
                    string fromProperty = Il2CppRuntime.ReadString(core.Interface("InnerException.ToString()"), "property result");
                    string fromField = Il2CppRuntime.ReadString(core.Interface("_innerException.ToString()"), "field result");
                    Check(fromProperty.Contains("inner-message", StringComparison.Ordinal),
                        "method invoked through the property did not use the inner exception");
                    Check(fromField.Contains("inner-message", StringComparison.Ordinal),
                        "method invoked through the field did not use the inner exception");
                }
                finally
                {
                    core.Interface("-native_smoke_inner");
                }
            });
            Test("Assigning to a writable instance property and field", () =>
            {
                Type builderType = Il2CppType.Of<Il2CppSystem.Text.StringBuilder>();
                TypeRegister.CacheType(builderType, "NativeSmokeBuilder");
                string ctor = FindCall(builderType, "new", Il2CppType.Of<string>());
                core.Interface("*NativeSmokeBuilder." + ctor + "(\"abcdef\")");
                Check(ReadInt("Length") == 6, "builder started with the wrong length");
                Check(Il2CppRuntime.ReadInt32(core.Interface("Length = 3"), "property assignment result") == 3,
                    "property assignment did not return the assigned value");
                Check(ReadInt("Length") == 3 && Il2CppRuntime.ReadString(core.Interface("ToString()"), "builder") == "abc",
                    "writable property assignment did not change the native builder");
                Check(Il2CppRuntime.ReadInt32(core.Interface("m_ChunkLength = 2"), "field assignment result") == 2,
                    "field assignment did not return the assigned value");
                Check(ReadInt("m_ChunkLength") == 2 && ReadInt("Length") == 2 &&
                    Il2CppRuntime.ReadString(core.Interface("ToString()"), "builder") == "ab",
                    "instance field assignment did not change the native builder");
                core.Interface("+native_smoke_builder");
                try
                {
                    core.Interface("*Types.String(\"unrelated\")");
                    Check(Il2CppRuntime.ReadInt32(core.Interface("native_smoke_builder.Length = 1"), "variable property assignment") == 1,
                        "assignment through a variable to a property returned the wrong value");
                    Check(ReadInt("native_smoke_builder.Length") == 1 &&
                        Il2CppRuntime.ReadString(core.Interface("native_smoke_builder.ToString()"), "variable receiver") == "a",
                        "method invoked through the variable did not see the property assignment");
                    Check(Il2CppRuntime.ReadInt32(core.Interface("native_smoke_builder.m_ChunkLength = 0"), "variable field assignment") == 0,
                        "assignment through a variable to a field returned the wrong value");
                    Check(ReadInt("native_smoke_builder.m_ChunkLength") == 0 &&
                        Il2CppRuntime.ReadString(core.Interface("native_smoke_builder.ToString()"), "variable receiver") == "",
                        "method invoked through the variable did not see the field assignment");
                }
                finally
                {
                    core.Interface("-native_smoke_builder");
                }
            });
            Test("Isolated native void return and feed", () =>
            {
                Type gcType = Il2CppSystem.Reflection.Assembly.Load("mscorlib").GetType("System.GC", true, false)
                    ?? throw new MissingMemberException("System.GC");
                TypeRegister.CacheType(gcType, "NativeSmokeGC");
                string call = FindCall(gcType, "KeepAlive", Il2CppType.Of<Il2CppSystem.Object>());
                Check(core.Interface("NativeSmokeGC." + call + "(null)") == null,
                    "standalone native void call returned an object");
                var feed = new NavigationFeed(false);
                string output = feed.SafeInterface("NativeSmokeGC." + call + "(null)");
                Check(ReturnedLines(output).SequenceEqual(new[] { "null" }),
                    "isolated void call did not render a null result: " + output.Replace(Environment.NewLine, "\\n"));
            });
            Test("Method and value cache namespaces", () =>
            {
                Type text = Il2CppType.Of<string>();
                MethodBase method = Map(text).Values.First(x =>
                {
                    if (!x.Name.Equals("Contains", StringComparison.OrdinalIgnoreCase)) return false;
                    var parameters = x.GetParameters();
                    return parameters.Length == 1 && Il2CppRuntime.SameType(parameters[0].ParameterType, text);
                });
                PropertyInfo property = text.GetProperty("Length", NavigatorCore.Flag)
                    ?? throw new MissingMemberException("String.Length");

                RuntimeCache.FlushStaticCache(false, true, false);
                try
                {
                    const string collision = "shared-cache-key";
                    RuntimeCache.CacheMember(false, false, text, method, collision);
                    RuntimeCache.FromCache<MemberInfo>(collision, text, out bool typeCached, out bool valueCached);
                    Check(typeCached && !valueCached, "method leaked into the value cache namespace");
                    RuntimeCache.CacheMember(typeCached, valueCached, text, property, collision);

                    MethodBase? cachedMethod = RuntimeCache.FromCache<MethodBase>(collision, text, out _, out _);
                    MemberInfo? cachedValue = RuntimeCache.FromCache<MemberInfo>(collision, text, out _, out _);
                    Check(cachedMethod?.Pointer == method.Pointer, "method cache entry was overwritten");
                    Check(cachedValue?.Pointer == property.Pointer, "property cache entry was overwritten");
                }
                finally
                {
                    RuntimeCache.FlushStaticCache(false, true, false);
                }
            });
            Test("Native field read", () => Check(ReadInt("int.MaxValue") == int.MaxValue, "wrong native constant"));
            Test("Native reflection exception unwrapping", () =>
            {
                string parse = "int." + FindCall(Il2CppType.Of<int>(), "Parse", Il2CppType.Of<string>());
                try { core.Interface(parse + "(\"not-an-integer\")"); }
                catch (Il2CppException ex)
                {
                    Check(ex.Message.Contains("FormatException"), "inner exception absent");
                    Check(!ex.Message.StartsWith("System.Reflection.TargetInvocationException", StringComparison.Ordinal), "outer reflection exception was not unwrapped");
                    return;
                }
                throw new Exception("Expected a native format exception");
            });
            Test("Native dictionary enumeration", () =>
            {
                // Do not instantiate an arbitrary closed Dictionary<TKey,TValue> here. IL2CPP
                // can expose its proxy metadata even when the game did not generate AOT code for
                // that exact generic constructor. Hashtable is native, non-generic, and still
                // exercises TryEnumerate's IL2CPP IEnumerable/enumerator/disposal path.
                var dict = new Il2CppSystem.Collections.Hashtable();
                dict.Add(
                    Il2CppRuntime.AsIl2CppObject(1, Il2CppType.Of<int>())!,
                    Il2CppRuntime.AsIl2CppObject(2, Il2CppType.Of<int>())!);
                Check(Il2CppRuntime.TryEnumerate(dict, out var sequence), "native dictionary not enumerable");
                Check(sequence.Count() == 1, "native dictionary count mismatch");
            });
            Log.LogInfo($"NATIVE VERIFICATION RESULT: {passed} passed, {failed} failed. Native coverage applies to this game/runtime only.");
        }
        void Test(string name, Action test)
        {
            Log.LogInfo("RUN " + name);
            try { test(); passed++; Log.LogInfo("PASS " + name); }
            catch (Exception ex) { failed++; Log.LogError("FAIL " + name + "\n" + ex); }
        }
        static void Check(bool result, string message) { if (!result) throw new Exception(message); }
        static string ReadAllTextShared(string path)
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }
        static string[] ReturnedLines(string output)
        {
            var lines = new List<string>();
            using StringReader reader = new(output);
            bool returned = false;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!returned)
                {
                    if (line.TrimEnd().Equals("Returned:", StringComparison.Ordinal))
                        returned = true;
                    continue;
                }

                if (line.StartsWith("Loaded Type:", StringComparison.Ordinal) ||
                    line.StartsWith("Loaded Instance Type:", StringComparison.Ordinal) ||
                    line.StartsWith("Loaded Variable:", StringComparison.Ordinal))
                    break;
                lines.Add(line);
            }

            while (lines.Count > 0 && lines[^1].Length == 0)
                lines.RemoveAt(lines.Count - 1);
            return lines.ToArray();
        }
        static void Throws<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new Exception("Expected " + typeof(T).Name);
        }
        int ReadInt(string input) => Il2CppRuntime.ReadInt32(core.Interface(input), "result");
        static Dictionary<MethodKey, MethodBase> Map(Type type)
        {
            var methods = new Dictionary<MethodKey, MethodBase>();
            TypeMap.MapType(methods, null, type, null);
            return methods;
        }
        static string CallName(MethodKey key, string? argument = null)
            => key.GKey.Name + (key.MatchIndex == 0 ? "" : ":" + key.MatchIndex) + (argument == null ? "" : "<" + argument + ">");
        static string FindCall(Type type, string name, params Type[] parameterTypes)
        {
            foreach (var pair in Map(type))
            {
                if (!pair.Key.GKey.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || pair.Key.GKey.Args != 0) continue;
                var args = pair.Value.GetParameters();
                if (args.Length != parameterTypes.Length) continue;
                bool match = true;
                for (int i = 0; i < args.Length; i++)
                    match &= Il2CppRuntime.SameType(args[i].ParameterType, parameterTypes[i]);
                if (match) return CallName(pair.Key);
            }
            throw new MissingMethodException(name);
        }
    }
}
