using System.Xml.Serialization;

namespace TestResultDB
{
    public class Execution
    {
        [XmlAttribute("id")]
        public string Id { get; set; }
    }
}
