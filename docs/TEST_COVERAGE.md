# Native verifier: scope and operation

## Packaging and build

This folder is a complete standalone verifier project: `Plugin.cs` contains the 37 checks; `NavigatorSource\` is a **local snapshot** of the source compiled into the verifier; `XQuinn.NativeVerification.csproj` references only paths inside this folder and external game/BepInEx dependencies. It does not link live source from `..\IL2CPP.Navigator`. Its local snapshot was copied from the same recovered production runtime, and all 40 snapshot files matched their Navigator originals by SHA-256 when packaged. If production source changes later, this snapshot needs deliberate synchronization and another native run.

Build with `dotnet build 'C:\Users\user\Desktop\C#Lab\VietnamWarModLab\IL2CPP.Navigator.Verifier\XQuinn.NativeVerification.csproj' -c Release`. The default target game's BepInEx and generated interop references must exist; override the game directory with `-p:VietnamWarDirectory=<path>`. The output is `bin\Release\net6.0\IL2CPP.Navigator.Verifier.dll`. A build does not copy anything into the game.

For a native run, **close the game first**, remove/move production `IL2CPP.Navigator.dll` from `C:\Program Files (x86)\Steam\steamapps\common\VietnamWar\BepInEx\plugins`, copy in only the verifier DLL, start the game, and read `BepInEx\LogOutput.log`. The plugin logs `RUN` before each case, `PASS` or `FAIL` after it, then `NATIVE VERIFICATION RESULT: 37 passed, 0 failed` for a clean run. Also check `BepInEx\NavigatorLog.log` for `Types.String("a!b")` returning `a!b`. Restore the production plugin after testing; do not leave the two DLLs together.

## Case families (37 total)

1. Host registration of game and curated Unity types, duplicate/nested-type handling, and registry initialization.
2. Harmony patch installation, `!` routing to the original game path exactly once, normal chatbot routing, and log output.
3. Generic helper calls (`Types.Of`, `Types.Num`, enums, defaults, decimal literals), generic constraint checks, and reflection queries.
4. Both array overloads, expanded/empty/single/null `params`, string punctuation, and numeric/string element display through `NavigationFeed`.
5. Native constructors, property and nonpublic field reads, direct method calls, variable bindings used as arguments and method receivers.
6. Methods invoked through object-valued properties and fields (`Exception.InnerException` / `_innerException`), with the inner exception's message checked in both results.
7. Writable property and field assignment (`StringBuilder.Length` / `m_ChunkLength`) both on the current instance and on a saved variable, with reads and `ToString()` checking the state change.
8. Isolated native void `GC.KeepAlive(null)` returning `null` in both core and feed; cache separation; native field reads; reflection exception unwrapping; dictionary enumeration.

## Evidence and limitations

The last native launch of this revision returned **37 passed, 0 failed**; the verifier DLL tested in-game had SHA-256 `08E9C5B78D2B5B3AEFECD70C652710C5C50E4077BF0C95E6BD8123ED0C75F1DD`. Source was recovered from a deleted project, and its original recovered files were checked against their Recycle Bin copies. Later packaging made the verifier self-contained without changing the C# test logic. The locally packaged project builds successfully, but its newly rebuilt DLL has a different SHA-256 (`B45F85D59B14BE37CE2702D8FCA68464B7B0A3F21060B7BD95E5908EE1D99F83`) and has **not** itself been run in-game. A build alone does not constitute a new native run. A fresh native run should be made if the local snapshot or runtime changes. Results apply only to the tested Vietnam War IL2CPP/BepInEx runtime, not every Unity/IL2CPP installation.

Do not edit `bones` to run these tests. Do not change the installed production plugin merely to build the verifier; swapping DLLs is necessary only when intentionally doing another in-game verifier run.