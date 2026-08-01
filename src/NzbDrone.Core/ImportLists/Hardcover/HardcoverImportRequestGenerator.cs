using System.Collections.Generic;
using System.Text.Json;
using NLog;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.ImportLists.Hardcover
{
    public class HardcoverImportRequestGenerator : IImportListRequestGenerator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public HardcoverImportSettings Settings { get; set; }

        public int MaxPages { get; set; } = 1;
        public int PageSize { get; set; } = 200;

        public ImportListPageableRequestChain GetListItems()
        {
            var pageableRequests = new ImportListPageableRequestChain();

            pageableRequests.Add(GetPagedRequests());

            return pageableRequests;
        }

        private IEnumerable<ImportListRequest> GetPagedRequests()
        {
            var apiKey = NormalizeApiKey(Settings.ApiKey);

            var listIds = (Settings.ListIds ?? Enumerable.Empty<string>()).ToArray();
            Logger.Info("Hardcover: Fetching books for lists '{0}'", string.Join(", ", listIds));

            var graphQlBody = JsonSerializer.Serialize(new
            {
                query = @"
                    query ListBooks($slugs: [String!]!) { me { lists(where: { slug: { _in: $slugs } } ) { slug name list_books { book { id title contributions { author { id name } } } } } } }
                ",
                variables = new
                {
                    slugs = listIds
                }
            });

            var request = new HttpRequestBuilder($"{Settings.BaseUrl.TrimEnd('/')}/v1/graphql")
                .Post()
                .Accept(HttpAccept.Json)
                .SetHeader("Authorization", $"Bearer {apiKey}")
                .SetHeader("X-Api-Key", apiKey)
                .SetHeader("User-Agent", "Readarr (Hardcover Import)")
                .SetHeader("Content-Type", "application/json")
                .KeepAlive()
                .Build();

            request.SetContent(graphQlBody);

            yield return new ImportListRequest(request);
        }

        private string NormalizeApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return string.Empty;
            }

            var trimmed = apiKey.Trim();
            const string bearerPrefix = "bearer ";

            if (trimmed.StartsWith(bearerPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(bearerPrefix.Length).Trim();
            }

            return trimmed;
        }
    }
}
