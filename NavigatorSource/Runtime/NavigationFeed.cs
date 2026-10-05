using System;
using System.Collections.Generic;
using System.Text;
using XQuinn.Extensions;
using XQuinn.Reflection;
using XQuinn.Runtime.NavigatorEngine;

namespace XQuinn.Runtime
{
    /// <summary>Monitor Navigator output and normalize both managed and IL2CPP enumerables into one feed path.</summary>
    public sealed class NavigationFeed
    {
        readonly StringBuilder _feed = new();
        internal readonly NavigatorCore _core = new();
        bool _stackTrace;

        public bool StackTrace
        {
            get => _stackTrace;
            set
            {
                _stackTrace = value;
                _core.StackTrace = value;
            }
        }

        /// <summary>Create a Navigator feed for a host chat, console, or logging integration.</summary>
        public NavigationFeed(bool stacktrace) => StackTrace = stacktrace;

        public string SafeInterface(string input)
        {
            _feed.Length = 0;
            try
            {
                return Interface(input);
            }
            catch (Exception ex)
            {
                _feed.Length = 0;
                _feed.CatchException(ex, StackTrace);
                string output = _feed.ToString();
                _feed.Length = 0;
                return output;
            }
        }

        internal string Interface(string input)
        {
            _feed.AppendLine();
            _feed.Append(DateTime.Now);
            _feed.AppendLine();
            _feed.Append("Instruction :");
            _feed.AppendLine(input);
            ProcessReturn(_core.Interface(input));
            AppendNavigData();
            string output = _feed.ToString();
            _feed.Length = 0;
            return output;
        }

        void ProcessReturn(object? ret)
        {
            VariableBinding? variable = ret as VariableBinding;
            if (variable != null)
            {
                _feed.Append("Variable: ");
                _feed.AppendLine(variable.ToString());
                ret = variable.Object;
            }

            bool enumerable = Il2CppRuntime.TryEnumerate(ret, out IEnumerable<object?> sequence);
            if (variable == null || enumerable)
                _feed.AppendLine("Returned: ");

            if (enumerable)
            {
                // AppendMany consumes either kind of enumerable once. Even an empty string
                // item writes its index, so a length change reliably detects a nonempty result.
                int start = _feed.Length;
                _feed.AppendMany(sequence, Environment.NewLine, true,
                    Il2CppRuntime.ToDisplayString);
                if (_feed.Length == start)
                    _feed.AppendLine("Enumerable is empty.");
                else
                    _feed.AppendLine();
            }
            else if (variable == null)
            {
                _feed.AppendLine(Il2CppRuntime.ToDisplayString(ret));
            }
        }

        void AppendNavigData()
        {
            if (_core._static_type == null)
                return;

            _feed.Append("Loaded Type: ");
            _feed.AppendLine(ReflectionPrinter.Print(_core._static_type, false));
            if (_core._object_type == null)
                return;

            _feed.Append("Loaded Instance Type: ");
            _feed.AppendLine(ReflectionPrinter.Print(_core._object_type, false));
            if (_core._variable != null)
            {
                _feed.Append("Loaded Variable: ");
                _feed.AppendLine(_core._variable);
            }
        }
    }
}
