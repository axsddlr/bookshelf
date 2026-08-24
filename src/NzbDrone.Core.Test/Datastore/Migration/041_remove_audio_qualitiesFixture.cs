using System;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class remove_audio_qualitiesFixture : MigrationTest<remove_audio_qualities>
    {
        [Test]
        public void should_remap_audio_qualities_in_quality_profiles()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("QualityProfiles").Row(new
                {
                    Name = "Any",
                    Cutoff = 10,
                    Items = "[ { \"quality\": 3, \"allowed\": true }, { \"quality\": 12, \"allowed\": true }, { \"name\": \"Audio\", \"items\": [ { \"quality\": 10, \"allowed\": true }, { \"quality\": 13, \"allowed\": false } ], \"allowed\": true } ]",
                    MinFormatScore = 0,
                    CutoffFormatScore = 0,
                    UpgradeAllowed = true,
                    FormatItems = "[]"
                });
            });

            var profiles = db.Query<QualityProfile041>("SELECT \"Id\", \"Cutoff\", \"Items\" FROM \"QualityProfiles\"");

            profiles.Should().HaveCount(1);

            var profile = profiles.First();
            profile.Cutoff.Should().Be(0);

            var items = profile.Items;
            items.Should().HaveCount(3);

            // Untouched ebook quality
            items[0].Value<int>("quality").Should().Be(3);

            // Top-level M4B (12) -> Unknown (0)
            items[1].Value<int>("quality").Should().Be(0);

            // Nested group: MP3 (10) and UnknownAudio (13) -> Unknown (0), other properties preserved
            var nested = (JArray)items[2]["items"];
            nested.Should().HaveCount(2);
            nested[0].Value<int>("quality").Should().Be(0);
            nested[0].Value<bool>("allowed").Should().BeTrue();
            nested[1].Value<int>("quality").Should().Be(0);
            nested[1].Value<bool>("allowed").Should().BeFalse();
            items[2].Value<string>("name").Should().Be("Audio");
        }

        [Test]
        public void should_remap_audio_quality_in_book_files()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("BookFiles").Row(new
                {
                    EditionId = 1,
                    CalibreId = 0,
                    Part = 1,
                    Quality = "{ \"quality\": 13, \"revision\": { \"version\": 1, \"real\": 0, \"isRepack\": false } }",
                    Size = 1000,
                    SceneName = "audio.book",
                    DateAdded = DateTime.UtcNow,
                    Modified = DateTime.UtcNow,
                    Path = "/books/audio.m4b"
                });

                c.Insert.IntoTable("BookFiles").Row(new
                {
                    EditionId = 2,
                    CalibreId = 0,
                    Part = 1,
                    Quality = "{ \"quality\": 3, \"revision\": { \"version\": 1, \"real\": 0, \"isRepack\": false } }",
                    Size = 1000,
                    SceneName = "ebook",
                    DateAdded = DateTime.UtcNow,
                    Modified = DateTime.UtcNow,
                    Path = "/books/ebook.epub"
                });
            });

            var files = db.Query<BookFile041>("SELECT \"Id\", \"Quality\" FROM \"BookFiles\" ORDER BY \"Id\"");

            files.Should().HaveCount(2);
            files.First().Quality.Value<int>("quality").Should().Be(0);

            // Revision must survive the patch untouched
            files.First().Quality["revision"].Value<int>("version").Should().Be(1);

            // Ebook quality untouched
            files.Last().Quality.Value<int>("quality").Should().Be(3);
        }

        [Test]
        public void should_delete_stale_audio_quality_definitions()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("QualityDefinitions").Row(new
                {
                    Quality = 12,
                    Title = "M4B",
                    MinSize = 0,
                    MaxSize = 350
                });

                c.Insert.IntoTable("QualityDefinitions").Row(new
                {
                    Quality = 3,
                    Title = "EPUB",
                    MinSize = 0,
                    MaxSize = 350
                });
            });

            var definitions = db.Query<QualityDefinition041>("SELECT \"Id\", \"Quality\" FROM \"QualityDefinitions\"");

            definitions.Should().HaveCount(1);
            definitions.First().Quality.Should().Be(3);
        }

        private class QualityProfile041
        {
            public int Id { get; set; }
            public int Cutoff { get; set; }
            public JArray Items { get; set; }
        }

        private class BookFile041
        {
            public int Id { get; set; }
            public JObject Quality { get; set; }
        }

        private class QualityDefinition041
        {
            public int Id { get; set; }
            public int Quality { get; set; }
        }
    }
}
