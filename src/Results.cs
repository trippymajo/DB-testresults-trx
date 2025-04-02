using System.Collections.Generic;
using System.Xml.Serialization;

namespace TestResultDB
{
    public class Results
    {
        [XmlElement("UnitTestResult")]
        public List<UnitTestResult> UnitTestResults { get; set; }
    }
}
