using static XQuinn.Parsing.NativeIntNet20;

namespace XQuinn.Parsing
{

    //To allow Nint/NUint tryparse on older net versions
    //Currently only for strings though
    internal static class NativeIntNet20
    {
        internal static readonly bool s_64bit;
        static NativeIntNet20()
        {
            unsafe
            {
                s_64bit = sizeof(nint) == sizeof(long);
            }
        }

    }
    public static class NIntNet20
    {
        public static bool TryParse(string value, out nint num)
        {
            num = default;
            if (s_64bit)
            {
                if (long.TryParse(value, out long int64))
                {
                    num = (nint)int64;
                    return true;
                }
            }
            else if (int.TryParse(value, out int int32))
            {
                num = int32;
                return true;
            }
            return false;
        }
    }

    public static class NUIntNet20
    {
        public static bool TryParse(string value, out nuint num)
        {
            num = default;
            if (s_64bit)
            {
                if (ulong.TryParse(value, out ulong uint64))
                {
                    num = (nuint)uint64;
                    return true;
                }
            }
            else if (uint.TryParse(value, out uint uint32))
            {
                num = uint32;
                return true;
            }
            return false;
        }

    }
}