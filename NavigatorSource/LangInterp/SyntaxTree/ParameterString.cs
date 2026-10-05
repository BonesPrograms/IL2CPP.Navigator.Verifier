namespace XQuinn.LangInterp.SyntaxTree
{
    internal abstract class ParameterString
    {
        protected string _arg; //This is, generally speaking, init-only, but GenericString needs it to be *not* readonly for it's factory method so it isnt.
        internal ParameterString(string String) //Regardless, the values of any ParameterString inheritor are intended to be immutable after initialization.
        {
            _arg = String;
        }
        public override string ToString()
        {
            return _arg;
        }


    }

    internal abstract class MetadataString : ParameterString 
    {
        public string Name => _arg;
        protected MetadataString(string arg) : base(arg)
        {

        }

    }
}