using System.Diagnostics.Metrics;
using System.Xml.Serialization;

namespace TestResultDB
{
    public class ResultSummary
    {
        [XmlAttribute("outcome")]
        public string Outcome { get; set; }

        [XmlElement("Counters")]
        public Counters Counters { get; set; }

        [XmlElement("RunInfos")]
        public RunInfos RunInfos { get; set; }
    }
}
