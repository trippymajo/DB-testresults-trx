
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TestResultDB.TrxFileParser
{
    public class TestDefinitions
    {
        [XmlElement("UnitTest")]
        public List<UnitTest> UnitTests { get; set; }
    }
}
