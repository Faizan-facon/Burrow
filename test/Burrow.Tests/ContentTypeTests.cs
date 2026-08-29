using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Squirrel;
using Squirrel.Tests.TestHelpers;
using Xunit;

namespace Squirrel.Tests.Core
{
    public class ContentTypeTests
    {
        [Theory]
        [InlineData("basic.xml", "basic-merged.xml")]
        [InlineData("complex.xml", "complex-merged.xml")]
        public void MergeContentTypes(string inputFileName, string expectedFileName)
        {
            var inputFile = IntegrationTestHelper.GetPath("fixtures", "content-types", inputFileName);
            var expectedFile = IntegrationTestHelper.GetPath("fixtures", "content-types", expectedFileName);
            var tempFile = Path.GetTempFileName() + ".xml";

            var expected = new XmlDocument();
            expected.Load(expectedFile);

            var existingTypes = GetContentTypes(expected);

            try {
                File.Copy(inputFile, tempFile);

                var actual = new XmlDocument();
                actual.Load(tempFile);

                ContentType.Merge(actual);

                var actualTypes = GetContentTypes(actual);

                Assert.Equal(existingTypes, actualTypes);
            } finally {
                File.Delete(tempFile);
            }
        }

        static IEnumerable<string> GetContentTypes(XmlNode doc)
        {
            var typesElement = (doc is XmlDocument d ? d.DocumentElement : doc) ?? doc.FirstChild.NextSibling;
            return typesElement.ChildNodes.OfType<XmlElement>()
                .Select(x => $"{x.Name}:{x.GetAttribute("Extension")}:{x.GetAttribute("PartName")}:{x.GetAttribute("ContentType")}")
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
