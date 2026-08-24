using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json.Nodes;
using Dapper;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(041)]
    public class remove_audio_qualities : NzbDroneMigrationBase
    {
        // Audio quality ids removed from Quality.All: MP3 (10), FLAC (11), M4B (12), UnknownAudio (13).
        // Migration 011 wrote 10/12/13 into every existing QualityProfiles row, and imported audiobooks
        // wrote them into BookFiles/History/Blocklist. Quality.FindById throws for unknown ids, so these
        // must be remapped to Unknown (0) or the app cannot start / cannot render those pages.
        private const int UnknownQualityId = 0;

        private static readonly HashSet<int> RemovedAudioQualityIds = new HashSet<int> { 10, 11, 12, 13 };

        protected override void MainDbUpgrade()
        {
            Execute.WithConnection(RemapAudioQualities);
        }

        private void RemapAudioQualities(IDbConnection conn, IDbTransaction tran)
        {
            RemapQualityProfiles(conn, tran);
            RemapQualityModelColumn(conn, tran, "BookFiles");
            RemapQualityModelColumn(conn, tran, "History");
            RemapQualityModelColumn(conn, tran, "Blocklist");

            // QualityDefinitions.Quality is a plain int read through DapperQualityIntConverter at
            // startup (QualityDefinitionService.InsertMissingDefinitions). Stale audio rows would throw
            // before the service gets a chance to delete them, so drop them here.
            conn.Execute(
                "DELETE FROM \"QualityDefinitions\" WHERE \"Quality\" IN (10, 11, 12, 13)",
                transaction: tran);
        }

        private void RemapQualityProfiles(IDbConnection conn, IDbTransaction tran)
        {
            var profiles = conn.Query("SELECT \"Id\", \"Cutoff\", \"Items\" FROM \"QualityProfiles\"", transaction: tran)
                .Select(x => new { Id = (int)x.Id, Cutoff = (int)x.Cutoff, Items = x.Items as string })
                .ToList();

            foreach (var profile in profiles)
            {
                var changed = false;
                var items = profile.Items == null ? null : JsonNode.Parse(profile.Items);

                if (items is JsonArray itemArray)
                {
                    changed = RemapItems(itemArray);
                }

                var cutoff = profile.Cutoff;

                if (RemovedAudioQualityIds.Contains(cutoff))
                {
                    cutoff = UnknownQualityId;
                    changed = true;
                }

                if (changed)
                {
                    conn.Execute(
                        "UPDATE \"QualityProfiles\" SET \"Cutoff\" = @Cutoff, \"Items\" = @Items WHERE \"Id\" = @Id",
                        new { profile.Id, Cutoff = cutoff, Items = items?.ToJsonString() ?? profile.Items },
                        transaction: tran);
                }
            }
        }

        private bool RemapItems(JsonArray items)
        {
            var changed = false;

            foreach (var item in items)
            {
                if (item is not JsonObject obj)
                {
                    continue;
                }

                changed |= RemapQualityProperty(obj);

                if (obj["items"] is JsonArray nested)
                {
                    changed |= RemapItems(nested);
                }
            }

            return changed;
        }

        private void RemapQualityModelColumn(IDbConnection conn, IDbTransaction tran, string table)
        {
            var rows = conn.Query($"SELECT \"Id\", \"Quality\" FROM \"{table}\"", transaction: tran)
                .Select(x => new { Id = (int)x.Id, Quality = x.Quality as string })
                .ToList();

            foreach (var row in rows)
            {
                if (row.Quality == null)
                {
                    continue;
                }

                var quality = JsonNode.Parse(row.Quality) as JsonObject;

                if (quality == null || !RemapQualityProperty(quality))
                {
                    continue;
                }

                conn.Execute(
                    $"UPDATE \"{table}\" SET \"Quality\" = @Quality WHERE \"Id\" = @Id",
                    new { row.Id, Quality = quality.ToJsonString() },
                    transaction: tran);
            }
        }

        // The embedded-document serializer writes camelCase property names (EmbeddedDocumentConverter sets
        // PropertyNamingPolicy = CamelCase) but reads case-insensitively, so older rows may hold either casing.
        private bool RemapQualityProperty(JsonObject obj)
        {
            var key = obj.Select(kvp => kvp.Key).FirstOrDefault(k => k.Equals("quality", System.StringComparison.OrdinalIgnoreCase));

            if (key == null)
            {
                return false;
            }

            var value = obj[key];

            if (value is not JsonValue jsonValue || !jsonValue.TryGetValue<int>(out var id) || !RemovedAudioQualityIds.Contains(id))
            {
                return false;
            }

            obj[key] = UnknownQualityId;

            return true;
        }
    }
}
