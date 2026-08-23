using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class QualityParserFixture : CoreTest
    {
        public static object[] SelfQualityParserCases =
        {
            new object[] { Quality.EPUB },
            new object[] { Quality.MOBI },
            new object[] { Quality.AZW3 }
        };

        // Flack doesn't get match for 'FLAC' quality
        [TestCase("Roberta Flack 2006 - The Very Best of")]
        public void should_not_parse_flac_quality(string title)
        {
            ParseAndVerifyQuality(title, null, 0, Quality.Unknown);
        }

        [TestCase("The Chainsmokers & Coldplay - Something Just Like This")]
        [TestCase("Frank Ocean Blonde 2016")]
        public void quality_parse(string title)
        {
            ParseAndVerifyQuality(title, null, 0, Quality.Unknown);
        }

        [Test]
        [TestCaseSource(nameof(SelfQualityParserCases))]
        public void parsing_our_own_quality_enum_name(Quality quality)
        {
            var fileName = string.Format("Some book [{0}]", quality.Name);
            var result = QualityParser.ParseQuality(fileName);
            result.Quality.Should().Be(quality);
        }

        [Test]
        public void should_parse_null_quality_description_as_unknown()
        {
            QualityParser.ParseCodec(null, null).Should().Be(Codec.Unknown);
        }

        [TestCase("Author Title - Book Title 2017 REPACK FLAC aAF", true)]
        [TestCase("Author Title - Book Title 2017 RERIP FLAC aAF", true)]
        [TestCase("Author Title - Book Title 2017 PROPER FLAC aAF", false)]
        public void should_be_able_to_parse_repack(string title, bool isRepack)
        {
            var result = QualityParser.ParseQuality(title);
            result.Revision.Version.Should().Be(2);
            result.Revision.IsRepack.Should().Be(isRepack);
        }

        private void ParseAndVerifyQuality(string name, string desc, int bitrate, Quality quality, int sampleSize = 0)
        {
            var result = QualityParser.ParseQuality(name);
            result.Quality.Should().Be(quality);
        }
    }
}
