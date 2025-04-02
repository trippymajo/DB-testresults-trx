using System.Collections.Generic;
using System.Xml.Serialization;

namespace TestResultDB
{
    public class TestDefinitions
    {
        [XmlElement("UnitTest")]
        public List<UnitTest> UnitTests { get; set; }
    }
}
