using System;
using System.Collections.Generic;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Notifications.Audiobookshelf
{
    public class Audiobookshelf : NotificationBase<AudiobookshelfSettings>
    {
        private readonly IAudiobookshelfProxy _proxy;
        private readonly Logger _logger;

        public Audiobookshelf(IAudiobookshelfProxy proxy, Logger logger)
        {
            _proxy = proxy;
            _logger = logger;
        }

        public override string Name => "Audiobookshelf";

        public override string Link => "https://www.audiobookshelf.org/";

        public override void OnReleaseImport(BookDownloadMessage message)
        {
            Scan();
        }

        public override void OnRename(Author author, List<RenamedBookFile> renamedFiles)
        {
            Scan();
        }

        public override void OnBookDelete(BookDeleteMessage deleteMessage)
        {
            if (deleteMessage.DeletedFiles)
            {
                Scan();
            }
        }

        public override void OnBookFileDelete(BookFileDeleteMessage deleteMessage)
        {
            Scan();
        }

        public override void OnAuthorDelete(AuthorDeleteMessage deleteMessage)
        {
            if (deleteMessage.DeletedFiles)
            {
                Scan();
            }
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            try
            {
                _proxy.GetLibraries(Settings);
            }
            catch (HttpException ex)
            {
                _logger.Error(ex, "Unable to connect to Audiobookshelf");
                failures.Add(new ValidationFailure("ApiKey", "Unable to connect to Audiobookshelf. Check host, port, and API key."));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to connect to Audiobookshelf");
                failures.Add(new ValidationFailure("Host", "Unable to connect to Audiobookshelf"));
            }

            return new ValidationResult(failures);
        }

        private void Scan()
        {
            try
            {
                _proxy.Scan(Settings);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to trigger Audiobookshelf library scan");
            }
        }
    }
}
