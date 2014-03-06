
namespace APFy.me.utilities
{
    public class ParameterSettings
    {
        public string Key { get; set; }
        //Used so that the api structure won't have to be updated if original site changes name on their side
        public string MappedKey { get; set; }
        public bool Required { get; set; }
        public bool IsFile { get; set; }
        public string ValidationRegExp { get; set; }
    }
}