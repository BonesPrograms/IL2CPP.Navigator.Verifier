using Type = Il2CppSystem.Type;

namespace XQuinn.Parsing
{
    public static class EnumNet20
    {
        public static bool TryParse(string value, Type enumType, bool ignoreCase, out Il2CppSystem.Object? result)
        {
            result = null;
            try
            {
                result = Il2CppSystem.Enum.Parse(enumType, value, ignoreCase);
            }
            catch
            {
            }
            return result != null;
        }
    }
}
