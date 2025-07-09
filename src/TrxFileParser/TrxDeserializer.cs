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

using System.Xml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace TestResultDB.TrxFileParser
{
    public static class TrxDeserializer
    {
        public static TestRun Deserialize(string filePath)
        {
            var rgx = new Regex("xmlns=\".*?\" ?");
            
            var fileContent = rgx.Replace(File.ReadAllText(filePath), string.Empty);
            return DeserializeContent(fileContent);
        }

        public static TestRun DeserializeContent(string fileContent)
        {
            var xmlNamespaceRegex = new Regex("xmlns=\".*?\" ?");
            var contentWithoutNamespace = xmlNamespaceRegex.Replace(fileContent, string.Empty);
            var xs = new XmlSerializer(typeof(TestRun));
            using (var reader = new StringReader(contentWithoutNamespace))
            {
                var testRun = (TestRun)xs.Deserialize(reader);
                return testRun;
            }
        }

        public static string ToMarkdown(this TestRun testRun, Header header = Header.H2)
        {
            var sb = new StringBuilder();
            var groups = testRun.Results.UnitTestResults
                .GroupBy(x => x.TestId)
                .ToList();
            var h = new string('#', (int)header);
            foreach (var group in groups)
            {
                var testName = @group.FirstOrDefault()?.TestName;
                var name = testName?.Substring(0, testName.IndexOf('('));
                sb.AppendLine($"{h} {name}");
                var i = 0;
                foreach (var g in @group.OrderBy(x => x.StartTime))
                {
                    if (testName == null) continue;
                    var text = g.TestName.Substring(testName.IndexOf(')') + 1).Trim();
                    sb.AppendLine($"{++i}. {text}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
