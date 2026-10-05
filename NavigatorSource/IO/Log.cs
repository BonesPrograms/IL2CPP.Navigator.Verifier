using System;
using System.IO;
using System.Text;

namespace XQuinn.IO
{

    public sealed class Logger : IOStream
    {
        readonly StringBuilder sb = new();
        Logger()
        {
        }
        public static Logger New(string path, bool safe)
        {
            Logger logger = New<Logger>(new(), path, safe);
            logger.Log("Begin Log", 4, 2);
            return logger;
        }
        public void Log(string text, int newLinesBefore = 0, int newLinesAfter = 0)
        {
            for (int i = 1; i <= newLinesBefore; i++)
                sb.Append(Environment.NewLine);
            sb.Append($"[{DateTime.Now}] {text}");
            for (int i = 1; i <= newLinesAfter; i++)
                sb.Append(Environment.NewLine);
            Writer.WriteLine(sb);
            sb.Length = 0;
            Flush();
        }





    }

}