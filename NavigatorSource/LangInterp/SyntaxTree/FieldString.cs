namespace XQuinn.LangInterp.SyntaxTree
{

    internal interface IMemberString
    {
        public TypeString DeclaringType { get; }
    }



    internal sealed class FieldString : MetadataString, IMemberString
    {

        public TypeString DeclaringType => _type;
        readonly TypeString _type;
        internal FieldString(string name, TypeString declaredIn) : base(name)
        {
            _type = declaredIn;
        }

        public override string ToString()
        {
        return $"{DeclaringType.StringID}.{Name}";
        }

    }
}