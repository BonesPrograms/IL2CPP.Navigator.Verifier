# Native verifier: scope and operation

## Packaging and build

This folder is a complete standalone verifier project: `Plugin.cs` and `InjectedProbe.cs` implement 40 checks; `NavigatorSource\` is a **local snapshot** of the source compiled into the verifier; `XQuinn.NativeVerification.csproj` references only paths inside this folder and external game/BepInEx dependencies. It does not link live source from the production Navigator project at `C:\Users\user\Desktop\C#Lab\VietnamWarModLab\IL2CPP.Navigator`. Its local snapshot was copied from the same recovered production runtime, and all 40 snapshot files matched their Navigator originals by SHA-256 when packaged. If production source changes later, this snapshot needs deliberate synchronization and another native run.

Build with `dotnet build 'C:\Users\user\Desktop\C#Lab\IL2CPP.Navigator.Verifier\XQuinn.NativeVerification.csproj' -c Release`. The default target game's BepInEx and generated interop references must exist; override the game directory with `-p:VietnamWarDirectory=<path>`. The output is `bin\Release\net6.0\IL2CPP.Navigator.Verifier.dll`. A build does not copy anything into the game.

For a native run, **close the game first**, remove/move production `IL2CPP.Navigator.dll` from `C:\Program Files (x86)\Steam\steamapps\common\VietnamWar\BepInEx\plugins`, copy in only the verifier DLL, start the game, and read `BepInEx\LogOutput.log`. The plugin logs `RUN` before each case, `PASS` or `FAIL` after it, then `NATIVE VERIFICATION RESULT: 40 passed, 0 failed` for a clean run. Also check `BepInEx\NavigatorLog.log` for `Types.String("a!b")` returning `a!b`. Remove the verifier after testing; install the production plugin separately if desired. Do not leave the two DLLs together.

## Case families (40 total)

1. Host registration of game and curated Unity types, duplicate/nested-type handling, and registry initialization.
2. Harmony patch installation, `!` routing to the original game path exactly once, normal chatbot routing, and log output.
3. Generic helper calls (`Types.Of`, `Types.Num`, enums, defaults, decimal literals), generic constraint checks, and reflection queries.
4. Both array overloads, expanded/empty/single/null `params`, string punctuation, and numeric/string element display through `NavigationFeed`.
5. Native constructors, property and nonpublic field reads, direct method calls, variable bindings used as arguments and method receivers.
6. Methods invoked through object-valued properties and fields (`Exception.InnerException` / `_innerException`), with the inner exception's message checked in both results.
7. Writable property and field assignment (`StringBuilder.Length` / `m_ChunkLength`) both on the current instance and on a saved variable, with reads and `ToString()` checking the state change.
8. Isolated native void `GC.KeepAlive(null)` returning `null` in both core and feed; cache separation; native field reads; reflection exception unwrapping; dictionary enumeration.
9. Construct `List<int>` using its `IEnumerable<int>` overload and an array produced by `Types.Array<int>`; verify list contents. Register a verifier-owned injected class, inspect reflected versus managed-only fields and `params int[]` method visibility, invoke a reflected constructor and ordinary methods, pass an explicit IL2CPP array to its method, read/write its number/string/list native fields, and preserve instance state through a Navigator variable.

## Evidence and limitations

The most recent in-game run of this revision, on October 7, 2026, returned **40 passed, 0 failed**, with only the verifier DLL loaded. The injected class's managed `SumParams(params int[])` carries `ParamArrayAttribute` in CLR reflection but was **not exposed** by IL2CPP reflection (`native method count=0`). The verifier does not claim that all possible IL2CPP-array or generic `params` signatures are unsupported: only this managed `int[]` fixture was exercised. `Il2CppReferenceField<Il2CppSystem.Collections.Generic.List<int>>` was reflected as an IL2CPP `List<int>` field, assigned an IL2CPP list in Navigator, and read back with the expected elements. Results apply only to this tested Vietnam War IL2CPP/BepInEx runtime; a build alone does not constitute a native run.

Do not edit `bones` to run these tests. Do not change the installed production plugin merely to build the verifier; swapping DLLs is necessary only when intentionally doing another in-game verifier run.