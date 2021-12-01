using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using QBackup.Core;



namespace QBackup.ConsoleHelpers
{

    public class BackUpOperation
    {

        #region Properties

        [JsonConverter(typeof(StringEnumConverter))]
        public OperationTypes Type { get; set; }

        public string Arg1 { get; set; }
        public string Arg1Comment { get; set; }
        public string Arg2 { get; set; }
        public string Arg2Comment { get; set; }

        #endregion

    }

}