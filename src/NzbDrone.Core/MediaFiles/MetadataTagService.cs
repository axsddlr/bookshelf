using System.Collections.Generic;
using System.IO.Abstractions;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles
{
    public interface IMetadataTagService
    {
        ParsedTrackInfo ReadTags(IFileInfo file);
        void WriteTags(BookFile trackfile, bool newDownload, bool force = false);
        void SyncTags(List<Edition> books);
        List<RetagBookFilePreview> GetRetagPreviewsByAuthor(int authorId);
        List<RetagBookFilePreview> GetRetagPreviewsByBook(int authorId);
    }

    public class MetadataTagService : IMetadataTagService,
        IExecute<RetagFilesCommand>,
        IExecute<RetagAuthorCommand>
    {
        private readonly IEBookTagService _eBookTagService;

        public MetadataTagService(IEBookTagService eBookTagService)
        {
            _eBookTagService = eBookTagService;
        }

        public ParsedTrackInfo ReadTags(IFileInfo file)
        {
            return _eBookTagService.ReadTags(file);
        }

        public void WriteTags(BookFile bookFile, bool newDownload, bool force = false)
        {
            if (bookFile.CalibreId > 0)
            {
                _eBookTagService.WriteTags(bookFile, newDownload, force);
            }
        }

        public void SyncTags(List<Edition> editions)
        {
            _eBookTagService.SyncTags(editions);
        }

        public List<RetagBookFilePreview> GetRetagPreviewsByAuthor(int authorId)
        {
            return _eBookTagService.GetRetagPreviewsByAuthor(authorId);
        }

        public List<RetagBookFilePreview> GetRetagPreviewsByBook(int bookId)
        {
            return _eBookTagService.GetRetagPreviewsByBook(bookId);
        }

        public void Execute(RetagFilesCommand message)
        {
            _eBookTagService.RetagFiles(message);
        }

        public void Execute(RetagAuthorCommand message)
        {
            _eBookTagService.RetagAuthor(message);
        }
    }
}
