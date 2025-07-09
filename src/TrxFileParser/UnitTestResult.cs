//  MIT License
//
//  Copyright (c) 2019 Hamed Fathi
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.

using System.Xml.Serialization;

namespace TestResultDB.TrxFileParser
{
    public class UnitTestResult
    {
        [XmlAttribute("executionId")]
        public string Id { get; set; }

        [XmlAttribute("parentExecutionId")]
        public string ParentId { get; set; }

        [XmlAttribute("testId")]
        public string TestId { get; set; }

        [XmlAttribute("testListId")]
        public string TestListId { get; set; }

        [XmlAttribute("testName")]
        public string TestName { get; set; }

        [XmlAttribute("computerName")]
        public string ComputerName { get; set; }

        [XmlAttribute("duration")]
        public string Duration { get; set; }

        [XmlAttribute("startTime")]
        public string StartTime { get; set; }

        [XmlAttribute("endTime")]
        public string EndTime { get; set; }

        [XmlAttribute("testType")]
        public string TestTypeId { get; set; }

        [XmlAttribute("relativeResultsDirectory")]
        public string RelativeResultsDirectoryId { get; set; }

        [XmlAttribute("outcome")]
        public string Outcome { get; set; }

        [XmlAttribute("resultType")]
        public string ResultType { get; set; }

        [XmlAttribute("dataRowInfo")]
        public string DataRowInfo { get; set; }

        [XmlElement("Output")]
        public Output Output { get; set; }

        [XmlElement("InnerResults")]
        public Results InnerResults { get; set; }
    }
}
