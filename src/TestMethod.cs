﻿using System.Xml.Serialization;

namespace TestResultDB
{
    public class TestMethod
    {
        [XmlAttribute("adapterTypeName")]
        public string AdapterTypeName { get; set; }

        [XmlAttribute("className")]
        public string ClassName { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("codeBase")]
        public string CodeBase { get; set; }
    }
}
