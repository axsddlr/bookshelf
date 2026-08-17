using System.Collections.Generic;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Notifications.Audiobookshelf
{
    public interface IAudiobookshelfProxy
    {
        List<AudiobookshelfLibraryResource> GetLibraries(AudiobookshelfSettings settings);
        void Scan(AudiobookshelfSettings settings);
    }

    public class AudiobookshelfProxy : IAudiobookshelfProxy
    {
        private readonly IHttpClient _httpClient;

        public AudiobookshelfProxy(IHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public List<AudiobookshelfLibraryResource> GetLibraries(AudiobookshelfSettings settings)
        {
            var request = BuildRequest(settings).Resource("api/libraries").Build();
            var response = _httpClient.Get<AudiobookshelfLibrariesResource>(request);

            return response.Resource.Libraries ?? new List<AudiobookshelfLibraryResource>();
        }

        public void Scan(AudiobookshelfSettings settings)
        {
            foreach (var library in GetLibraries(settings))
            {
                var request = BuildRequest(settings).Resource($"api/libraries/{library.Id}/scan").Post().Build();
                _httpClient.Execute(request);
            }
        }

        private HttpRequestBuilder BuildRequest(AudiobookshelfSettings settings)
        {
            var baseUrl = HttpRequestBuilder.BuildBaseUrl(settings.UseSsl, settings.Host, settings.Port, settings.UrlBase);

            return new HttpRequestBuilder(baseUrl)
                .SetHeader("Authorization", $"Bearer {settings.ApiKey}");
        }
    }

    public class AudiobookshelfLibrariesResource
    {
        public List<AudiobookshelfLibraryResource> Libraries { get; set; }
    }

    public class AudiobookshelfLibraryResource
    {
        public string Id { get; set; }

        public string Name { get; set; }
    }
}
