using System;
using System.Text;
using XQuinn.LangInterp;
using XQuinn.Extensions;

namespace XQuinn.Runtime.NavigatorEngine
{

    static class MiniLexer
    {


        internal static bool AssignmentSubstring(string invocation, out string? left, out string? right)
        {
            left = null;
            right = null;
            int? assignment = null;
            for (int i = 0; i < invocation.Length; i++)
            {
                char value = invocation[i];
                if (value == CallLexer.MethodDeclr)
                    return false;
                if (value == '=')
                {
                    assignment = i;
                    break;
                }
            }
            if (assignment == null)
                return false;
            left = invocation.Remove(assignment.Value);
            right = invocation.Substring(assignment.Value + 1);
            return true;

        }

        internal static string? ResolveMemberAccessOrLiteral(string invocation, out string member, out bool notMethod) //returns typename, outputs the accessed member
        {
            int sublength = invocation.IndexOf(CallLexer.MethodDeclr);
            notMethod = sublength < 0;
            sublength = notMethod ? invocation.Length : sublength;
            member = invocation;
            int? lastAccessorIndex = null;
            if (notMethod)
            {
                for (int i = 0; i < sublength; i++)
                {
                    char value = invocation[i];
                    if (value != CallLexer.Whitespace)
                    {
                        if (IsLiteral(value)) //because floats will resolve as member access. lol.
                            return null;
                        else
                            break;
                    }
                }
                // for (int i = sublength - 1; i >= 0; i--)
                // {
                //     char c = invocation[i];
                //     if (c == CallLexer.MemberAccess)
                //     {
                //         lastAccessorIndex = i;
                //         break;
                //     }
                // }
            }
            for (int i = 0; i < sublength; i++)
            {
                char c = invocation[i];
                if (c == CallLexer.GenericDeclr)
                {
                    int nest = 0;
                    int lastcount = 0;
                    bool beginNest = false;
                    for (int x = i; x < sublength; x++)
                    {
                        c = invocation[x];
                        if (c == CallLexer.GenericDeclr)
                        {
                            if (beginNest)
                            {
                                nest++;
                                lastcount++;
                            }
                            else
                                beginNest = true;
                        }
                        else if (c == CallLexer.GenericTerminate)
                        {
                            if (lastcount > nest)
                                lastcount--;
                            if (lastcount == 0)
                            {
                                i = x;
                                break;
                            }
                            if (nest > 0)
                                nest--;
                        }
                    }
                    if (lastcount > 0)
                        throw new LexicalException("Nested generics not properly terminated. You can fix this by slapping an ext a > on the end, usually.", invocation);
                }
                else if (c == CallLexer.MemberAccessOrDecimal)
                {
                    lastAccessorIndex = i;
                }

            }
            if (lastAccessorIndex != null)
            {
                member = invocation.Substring(lastAccessorIndex.Value + 1);
                string typename = invocation.Remove(lastAccessorIndex.Value);
                return typename;
            }
            return null;
        }

        internal static bool ImplicitThisMethodCall(string righthand) //makes sure that the '(' contained is not part of a string and is actually a method parameter
        {
            for (int i = 0; i < righthand.Length; i++)
            {
                char value = righthand[i];
                if (IsLiteral(value))
                    return false;
                if (value == CallLexer.MethodDeclr)
                    return true;
            }
            return false;
        }

        static bool IsLiteral(char value) =>
        value switch
        {
            CallLexer.StringDeclr or CallLexer.CharDeclr or CallLexer.MemberAccessOrDecimal or CallLexer.NegSign or CallLexer.NoEscDeclr => true,
            _ => char.IsDigit(value)
        };
    }



}
