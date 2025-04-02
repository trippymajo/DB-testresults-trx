using System.Collections.Generic;
using System.Xml.Serialization;

namespace TestResultDB
{
    public class RunInfos
    {
        [XmlElement("RunInfo")]
        public List<RunInfo> Infos { get; set; }
    }
}