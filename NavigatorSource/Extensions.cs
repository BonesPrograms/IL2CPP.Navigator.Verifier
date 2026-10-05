using System.Text;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Collections;

namespace XQuinn.Extensions
{

    public static class StringBuilderExtensions
    {

        public static StringBuilder AppendMany(this StringBuilder sb, IEnumerable many, string? delimiter = null, bool appendIndex = false, Func<object?, string?>? toString = null)
        {
            int i = 0;
            foreach (object? element in many)
                AppendMany(ref i, sb, element, delimiter, appendIndex, toString);
            return sb;
        }
        public static StringBuilder AppendMany<T>(this StringBuilder sb, IEnumerable<T?> many, string? delimiter = null, bool appendIndex = false, Func<T?, string?>? toString = null)
        {
            int i = 0;
            foreach (T? element in many)
                AppendMany(ref i, sb, element, delimiter, appendIndex, toString);
            return sb;
        }

        static void AppendMany<T>(ref int i, StringBuilder sb, T? element, string? delimiter = null, bool appendIndex = false, Func<T?, string?>? toString = null)
        {
            if (i > 0 && delimiter != null)
                sb.Append(delimiter);
            if (appendIndex)
                sb.Append($"[{i}] ");
            string? text;
            if (toString == null)
                text = element?.ToString();
            else
                text = toString.Invoke(element);
            sb.Append(text ?? "null");
            i++;
        }
        public static void CatchException(this StringBuilder sb, Exception ex, bool stacktrace)
        {
            sb.AppendLine($"Exception Type: {ex.GetType()}");
            sb.AppendLine($"Message: {ex.Message}");
            if (stacktrace)
            {
                sb.AppendLine($"Inner Exception: {ex.InnerException?.ToString() ?? "<none>"}");
                sb.AppendLine($"Stack Trace: {ex.StackTrace}");
                sb.AppendLine($"Data: {ex.Data}");
                sb.AppendLine($"Target Site: {ex.TargetSite?.ToString() ?? "<none>"}");
                sb.AppendLine($"Source: {ex.Source ?? "<none>"}");
            }
        }
    }



    public static class CollectionExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> objs, Action<T> action)
        {
            foreach (T obj in objs)
                action(obj);
        }
    }

    public static class StringExtensions
    {


        public static bool EqualsCaseless(this string strng, string? txt)
        {
            return strng.Equals(txt, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ContainsCaseless(this string strng, string? txt)
        {
            if (txt == null)
                return false;
            return strng.IndexOf(txt, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // /// <summary>
        // /// Remove all occurances of a specified series of characters.
        // /// </summary>
        // public static string RemoveChar(this string text, params char[] chars)
        // {
        //     StringBuilder sb = new();
        //     foreach (char c in text)
        //     {
        //         foreach (var bad in chars)
        //         {
        //             if (bad == c)
        //                 continue;
        //         }
        //         sb.Append(c);
        //     }
        //     return sb.ToString();
        // }

        //         public static string ReplaceChar(this string text, char replace, char replacewith)
        //         {
        // #if NET10_0_OR_GREATER
        //             ReadOnlySpan<char> span = text.AsSpan();
        //             Span<char> buffer = stackalloc char[text.Length];
        //             span.CopyTo(buffer);
        //             for(int i = 0; i < buffer.Length; i++)
        //             {
        //                 char c = buffer[i];
        //                 if(c == replace)
        //                 buffer[i] = replacewith;
        //             }
        //             return buffer.ToString();
        // #else
        //             StringBuilder sb = new();
        //             foreach (char c in text) sb.Append(c == replace ? replacewith : c);
        //             return sb.ToString();
        // #endif
        //         }


    }

}
