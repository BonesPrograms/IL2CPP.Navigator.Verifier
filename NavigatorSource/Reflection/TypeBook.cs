#if XQUINN_ENABLE_LEGACY_MANAGED_REFLECTION
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using XQuinn.LangInterp.SyntaxTree;
using System.Runtime.CompilerServices;
using System.CodeDom.Compiler;
using XQuinn.Extensions;
using XQuinn.Runtime.NavigatorEngine;

namespace XQuinn.Reflection
{


    // 
    // Pro Tip: Shortnames on generic definitions will come out followed by a ` and the number of generic arguments.
    // IE. A generic definition named Generic<T> will have the short name Generic`1.
    // //It is recommended not to override this because there can be multiple generic definitions with the same name but a different number of generic parameter

    /// <summary>
    /// /// A case-insensitive readonly wrapper for a dictionary of type names for quick string lookup and caching. This isnt much more special than a dictionary, the only 
    /// difference is it simplifies creation.
    /// </summary>
    internal sealed class TypeBook : IReadOnlyDictionary<GenericKey, Type>
    {
        //readonly ConcurrentDictionary<string, Type> _book;
        readonly Dictionary<GenericKey, Type> _book;
        public int Count => _book.Count;
        public IEnumerable<GenericKey> Keys => _book.Keys;
        public IEnumerable<Type> Values => _book.Values;

        public Type this[GenericKey key] => _book[key];
        TypeBook(Dictionary<GenericKey, Type> book)
        {
            //_book = book;
            _book = book;
        }
        IEnumerator IEnumerable.GetEnumerator() => _book.GetEnumerator();
        public IEnumerator<KeyValuePair<GenericKey, Type>> GetEnumerator() => _book.GetEnumerator();

#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
        public bool TryGetValue(GenericKey key, out Type? value) => _book.TryGetValue(key, out value);
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
        // public bool TryAdd(string key, Type value) => _book.TryAdd(key, value);
        public bool ContainsKey(GenericKey key) => _book.ContainsKey(key);

        //ToString here is for custom filtering, ie. maybe one of your shortnames are already taken, you can have your filter pre-check if one of your types are already cached
        //and then return a different name, you can also return null and it will *skip* adding that type to the typebook. Using a ToString will also completely override
        //the default procedure for choosing keys.

        public static TypeBook New(IEnumerable<Type> types, Func<Type, string?> toString)
        {
            Dictionary<GenericKey, Type> book = new();
            foreach (Type type in types)
            {
                if (!type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))
#if NET7_0_OR_GREATER
                     && !IsGeneratedRegexType(type)
#endif
                )
                {
                    if (IsFileType(type))
                        continue;
                    string? key = toString(type);
                    if (key == null)
                        continue;
                    GenericKey realKey = new(key, type);
                    if (book.TryGetValue(realKey, out Type? cached))
                        throw new DuplicateKeyException(cached, type, key);
                    book[realKey] = type;
                }
            }
            return new(book);
        }

        // static bool CompilerGenerated(Type? type)
        // {
        //     while (type != typeof(object) && type != null)
        //     {
        //         if (type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))) return true;
        //         else return false;
        //     }
        //     return false;
        // }
#if NET7_0_OR_GREATER
        internal static bool IsGeneratedRegexType(Type type)
        {
            for (var current = type; current != null; current = current.DeclaringType)
            {
                if (current.IsDefined(typeof(GeneratedCodeAttribute), inherit: false))
                {
                    var attribute = current.GetCustomAttribute<GeneratedCodeAttribute>();

                    if (attribute?.Tool == "System.Text.RegularExpressions.Generator")
                        return true;
                }
            }

            return false;
        }
#endif
        internal static bool IsFileType(Type t)
        {
            if (!t.IsPublic && !t.IsNested)
                return t.Name.StartsWith("<");
            else return false;
        }

        //         if (type == null) throw new ArgumentNullException(nameof(type));

        //         // Look for the [CompilerFeatureRequired] attribute assigned by the compiler
        //         var attribute = type.GetCustomAttributes(typeof(CompilerFeatureRequiredAttribute), inherit: false)
        //                             .FirstOrDefault() as CompilerFeatureRequiredAttribute;

        //         // Check if the required feature matches "FileLocalTypes"
        //         return attribute != null && attribute.FeatureName == "FileLocalTypes";
        //     }
        // }
        public static TypeBook New(IEnumerable<Type> types, bool fullname, bool excludeFileScoped = true)
        {
            Dictionary<GenericKey, Type> book = new();
            foreach (Type type in types)
            {
                if (!type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
                {
                    if (excludeFileScoped && IsFileType(type))
                        continue;
                    string key = fullname == false ? type.Name : type.FullName ?? throw new ArgumentNullException();
                    GenericKey realKey = new(key, type);
                    if (book.TryGetValue(realKey, out Type? cached))
                        throw new DuplicateKeyException(cached, type, key);
                    book[realKey] = type;
                }
            }
            return new(book);
        }


    }
}
#endif
