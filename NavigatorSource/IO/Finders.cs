using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if NET6_0_OR_GREATER
namespace XQuinn.IO.Finders
{


    /// <summary>
    /// Finds the EXE of a Steam Game that is stored in the Common folder. Only for Windows 64 bit.
    /// </summary>
    public static class SteamGameFinder //beleive there is a better way to do this incase they do not store the game in common folder but for now idk
    {
        public static readonly string s_steam_common = Path.Combine(GetSteamPath(), @"steamapps\common");
        static readonly EnumerationOptions s_default = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
        };

        /// <summary>
        /// exeName should be the file name without the .exe extension.
        /// </summary>
        public static string? Find(string exeName, EnumerationOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(exeName))
                throw new ArgumentException("exeName cannot be whitespace or null.");
            return Directory.EnumerateFiles(s_steam_common, $"{exeName}*.exe", options ?? s_default).FirstOrDefault();
        }
        public static string FindOrThrow(string exeName, EnumerationOptions? options = null) => Find(exeName, options) ?? throw new FileNotFoundException(null, $"{exeName}.exe");

        static string GetSteamPath()
        {//if 32 bit, must change this string
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam"))
            {
                if (key?.GetValue("InstallPath") is string path)
                    return path;
            }
            throw new DirectoryNotFoundException("Steam directory not found!");
        }
    }


    /// <summary>
    /// Folder directory for the Vietnam War Mod Lab.
    /// </summary>
    internal static class VietnamWarModLab
    {
        public static readonly string s_path = System.IO.Path.Combine(CodeLabFinder.s_path, "VietnamWarModLab") ?? throw new DirectoryNotFoundException("VietnamWarModLab not found in C#Lab!");


    }

    internal static class VietnamWarSource
    {
        public static readonly string s_path = System.IO.Path.GetDirectoryName(SteamGameFinder.FindOrThrow("Vietnam War"))!;


    }

    internal static class CodeLabFinder
    {
        public static readonly string s_path = System.IO.Path.Combine(KnownFolders.Desktop, "C#Lab") ?? throw new DirectoryNotFoundException("C#Lab not found on Desktop!");
    }
}
#endif