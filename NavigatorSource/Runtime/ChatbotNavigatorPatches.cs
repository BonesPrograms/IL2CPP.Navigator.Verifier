using System;
using System.IO;
using BepInEx;
using HarmonyLib;
using XQuinn.IO;
using XQuinn.Runtime;

namespace XQuinn.BepInEx.Chatbot
{
    internal static class ChatbotNavigatorHooks
    {
        internal const string HarmonyId = "xquinn.navigator.chatbot";
    }

    [HarmonyPatch(typeof(ChatBot), nameof(ChatBot.ChatInput))]
    public static class ChatbotStringSnatcher
    {
        public static string Chat { get; private set; } = string.Empty;

        public static bool IsThorCommand { get; private set; }

        [HarmonyPrefix]
        public static void StringSnatcher(ref string chat)
        {
            IsThorCommand = chat.Length > 0 && chat[0] == '!';
            if (IsThorCommand)
                chat = chat.Substring(1);
            Chat = chat;
        }

        internal static bool ConsumeThorCommand()
        {
            if (!IsThorCommand)
                return false;
            IsThorCommand = false;
            return true;
        }
    }

    [HarmonyPatch(typeof(ChatBot), "CompareChatWords")]
    public static class CommandChatbot
    {
        static readonly string s_logPath = Path.Combine(Paths.BepInExRootPath, "NavigatorLog.log");
        static readonly object s_logLock = new();
        static readonly Logger s_navigatorLogger = Logger.New(s_logPath, safe: true);

        internal static readonly NavigationFeed Navigator = new(stacktrace: false);

        public static string LastNavigatorOutput { get; private set; } = string.Empty;

        internal static string LogPath => s_logPath;

        [HarmonyPrefix]
        public static bool Check()
        {
            if (ChatbotStringSnatcher.ConsumeThorCommand())
                return true;

            LastNavigatorOutput = Navigator.SafeInterface(ChatbotStringSnatcher.Chat);
            lock (s_logLock)
                s_navigatorLogger.Log(LastNavigatorOutput, newLinesBefore: 1, newLinesAfter: 1);
            return false;
        }
    }
}