using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;

namespace XQuinn.IO
{

//Source: https://www.tutorialspoint.com/article/how-to-get-a-path-to-the-desktop-for-current-user-in-chash
public enum KnownFolder
{
    _invalid,
    Contacts,
    Downloads,
    Favorites,
    Links,
    SavedGames,
    SavedSearches
}
 
public static class KnownFolders
{
    public static readonly string Desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    private static readonly Dictionary<KnownFolder, Guid> s_guids = new()
    {
        [KnownFolder.Contacts] = new("56784854-C6CB-462B-8169-88E350ACB882"),
        [KnownFolder.Downloads] = new("374DE290-123F-4565-9164-39C4925E467B"),
        [KnownFolder.Favorites] = new("1777F761-68AD-4D8A-87BD-30B759FA33DD"),
        [KnownFolder.Links] = new("BFB9D5E0-C6A9-404C-B2B2-AE6DB6AF4968"),
        [KnownFolder.SavedGames] = new("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4"),
        [KnownFolder.SavedSearches] = new("7D1D3A04-DEBB-4115-95CF-2F29DA2920DA")
    };
 
    // c# shgetknownfolderpath directory path
    public static string GetPath(KnownFolder knownFolder)
    {
        return SHGetKnownFolderPath(s_guids[knownFolder], 0);
    }
 
    [DllImport("shell32",
        CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
    private static extern string SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags,
        nint hToken = 0);
}
}