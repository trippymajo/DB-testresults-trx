using System.Xml.Serialization;

namespace TestResultDB
{
    public class ErrorInfo
    {
        [XmlElement("Message")]
        public string Message { get; set; }

        [XmlElement("StackTrace")]
        public string StackTrace { get; set; }
    }
}
