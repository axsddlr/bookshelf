using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace VersOne.Epub.Internal
{
    public static class XmlUtils
    {
        private const string OpfNamespace = "http://www.idpf.org/2007/opf";

        public static async Task<XDocument> LoadDocumentAsync(Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
                memoryStream.Position = 0;
                return await Task.Run(() => LoadXDocument(memoryStream)).ConfigureAwait(false);
            }
        }

        private static XDocument LoadXDocument(MemoryStream memoryStream)
        {
            var xmlReaderSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore
            };

            try
            {
                using (var xmlReader = XmlReader.Create(memoryStream, xmlReaderSettings))
                {
                    return XDocument.Load(xmlReader);
                }
            }
            catch (XmlException)
            {
                memoryStream.Position = 0;
                using (var sr = new StreamReader(memoryStream))
                {
                    var text = sr.ReadToEnd();

                    // .NET can't handle XML 1.1, so try sanitising and reading as 1.0
                    if (text.StartsWith(@"<?xml version=""1.1"""))
                    {
                        text = @"<?xml version=""1.0""" + text.Substring(19);

                        var chars = text.Where(x => XmlConvert.IsXmlChar(x)).ToArray();
                        var sanitised = new string(chars);

                        return XDocument.Parse(sanitised);
                    }

                    // Some EPUBs use the 'opf' prefix without declaring its namespace.
                    if (text.Contains("opf:") && !text.Contains("xmlns:opf"))
                    {
                        var packageIndex = text.IndexOf("<package", StringComparison.OrdinalIgnoreCase);
                        if (packageIndex >= 0)
                        {
                            var insertAt = packageIndex + "<package".Length;
                            text = text.Insert(insertAt, " xmlns:opf=\"" + OpfNamespace + "\"");

                            return XDocument.Parse(text);
                        }
                    }
                }

                throw;
            }
        }
    }
}
