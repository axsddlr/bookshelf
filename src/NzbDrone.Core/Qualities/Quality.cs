using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Qualities
{
    public class Quality : IEmbeddedDocument, IEquatable<Quality>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public Quality()
        {
        }

        private Quality(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public bool Equals(Quality other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return Equals(obj as Quality);
        }

        public static bool operator ==(Quality left, Quality right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Quality left, Quality right)
        {
            return !Equals(left, right);
        }

        public static Quality Unknown => new Quality(0, "Unknown Text");
        public static Quality PDF => new Quality(1, "PDF");
        public static Quality MOBI => new Quality(2, "MOBI");
        public static Quality EPUB => new Quality(3, "EPUB");
        public static Quality AZW3 => new Quality(4, "AZW3");

        static Quality()
        {
            All = new List<Quality>
            {
                Unknown,
                PDF,
                MOBI,
                EPUB,
                AZW3
            };

            AllLookup = new Quality[All.Select(v => v.Id).Max() + 1];
            foreach (var quality in All)
            {
                AllLookup[quality.Id] = quality;
            }

            DefaultQualityDefinitions = new HashSet<QualityDefinition>
            {
                new QualityDefinition(Quality.Unknown)      { Weight = 1, MinSize = 0, MaxSize = 350, GroupWeight = 1 },
                new QualityDefinition(Quality.PDF)          { Weight = 5, MinSize = 0, MaxSize = 350, GroupWeight = 2 },
                new QualityDefinition(Quality.MOBI)         { Weight = 10, MinSize = 0, MaxSize = 350, GroupWeight = 10 },
                new QualityDefinition(Quality.EPUB)         { Weight = 11, MinSize = 0, MaxSize = 350, GroupWeight = 11 },
                new QualityDefinition(Quality.AZW3)         { Weight = 12, MinSize = 0, MaxSize = 350, GroupWeight = 12 },
            };
        }

        public static readonly List<Quality> All;

        public static readonly Quality[] AllLookup;

        public static readonly HashSet<QualityDefinition> DefaultQualityDefinitions;

        public static Quality FindById(int id)
        {
            if (id == 0)
            {
                return Unknown;
            }
            else if (id < 0 || id >= AllLookup.Length)
            {
                throw new ArgumentException("ID does not match a known quality", nameof(id));
            }

            var quality = AllLookup[id];

            if (quality == null)
            {
                throw new ArgumentException("ID does not match a known quality", nameof(id));
            }

            return quality;
        }

        public static explicit operator Quality(int id)
        {
            return FindById(id);
        }

        public static explicit operator int(Quality quality)
        {
            return quality.Id;
        }
    }
}
