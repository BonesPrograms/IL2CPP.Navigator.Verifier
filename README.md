# IL2CPP Navigator — Vietnam War automated verifier

This standalone BepInEx IL2CPP verifier embeds a **snapshot** of the Navigator runtime and chatbot Harmony integration in `NavigatorSource\`. It is intended to be the only Navigator-related DLL in `BepInEx\plugins` while testing. `Plugin.cs` implements the 37 native cases, and `XQuinn.NativeVerification.csproj` compiles this folder without referencing the sibling Navigator project.

The snapshot contains the same `Runtime`, `Reflection`, `LangInterp`, parser-helper, and managed `XQuinn.IO` logger source used by the main plugin at the time it was packaged. It does **not** update itself when the main project changes: copy new runtime files into `NavigatorSource\` intentionally if you need to test a later revision. The chatbot owns one static `NavigationFeed`; non-Thor chat is passed to its `SafeInterface`, and the returned text is logged through `XQuinn.IO.Logger`.

1. With the game closed, temporarily move `IL2CPP.Navigator.dll` out of `BepInEx\plugins` (keep a backup).
2. Build the verifier and put **only** `bin\Release\net6.0\IL2CPP.Navigator.Verifier.dll` into `BepInEx\plugins`.
3. Launch Vietnam War and inspect `BepInEx\LogOutput.log`.
4. Look for `NATIVE VERIFICATION RESULT: 37 passed, 0 failed.`
5. Confirm `BepInEx\NavigatorLog.log` contains the automated `Types.String("a!b")` instruction and result.
6. Close the game, remove the verifier DLL, and restore the production Navigator DLL when finished.

The verifier runs 37 native and host/chat integration cases, including invoking methods through variables, properties and fields, and assigning to writable properties and fields. You do not need to type into game chat for this verification. It writes `RUN` before every case so the last line identifies an active case if the native process terminates unexpectedly. The last successful native run for this revision reported **37 passed, 0 failed**; a build by itself cannot establish in-game correctness. See `docs\TEST_COVERAGE.md` for the test matrix and limits.

To rebuild the plugin against another game's reference assemblies:

```powershell
dotnet build 'C:\Users\user\Desktop\C#Lab\VietnamWarModLab\IL2CPP.Navigator.Verifier\XQuinn.NativeVerification.csproj' -c Release
```

`build.ps1` is an equivalent shortcut if PowerShell permits local scripts. Builds do not alter `BepInEx\plugins`. The project expects the game's BepInEx and generated interop assemblies and accepts `-p:VietnamWarDirectory=<path>` for other installs.
