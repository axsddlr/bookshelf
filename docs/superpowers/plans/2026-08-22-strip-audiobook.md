# Strip Audiobook Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove all audiobook-format support (MP3/FLAC/M4B quality, audio
extension mapping, audio tagging, audio distance-scoring, Audiobookshelf
notifier) from this Readarr fork so it becomes an ebook-only application.

**Architecture:** Audiobook support is not a separate module — it's quality
enum values plus an extension dictionary that other code branches on
(`MediaFileExtensions.AudioExtensions`). Emptying that dictionary makes
several `if (isAudio)` branches structurally dead; this plan removes the
enum values and dictionary first, then deletes the now-provably-dead
branches and their backing services, working from leaves (no other
consumers) up to consumers (`MetadataTagService`).

**Tech Stack:** .NET (C#), NUnit test projects (`*.Test`), no frontend
changes required — the audiobook-specific config field is a single settings
UI field, no component to remove.

**Spec:** `docs/superpowers/specs/2026-08-22-audiobookarr-split-design.md`
(Part 1: bookshelf strip). Note: this plan is *more precise* than the
spec's Part 1 — investigation during planning found real coupling the spec
didn't capture (`MetadataTagService`, `IConfigService` options, a frontend
settings field, 13 locale JSON files). The spec's intent (remove all
audiobook tagging support) is unchanged; this plan implements that intent
against the actual code, not the spec's simplified file list.

## Global Constraints

- Existing `Quality.Id` values for removed qualities (MP3=10, FLAC=11,
  M4B=12, UnknownAudio=13) must not be reassigned to new qualities — leave
  the numeric gap. A user with existing audiobook files has `Quality.Id`
  values of 10-13 stored in their database; reassigning those IDs to new
  ebook qualities would silently reclassify their files. Leaving the gap is
  correct and requires no migration.
- No test may be deleted without replacement unless the entire class under
  test is deleted in the same task.
- The test project's csproj is `src/NzbDrone.Core.Test/Readarr.Core.Test.csproj`
  (folder is `NzbDrone.Core.Test`, the project file itself is prefixed
  `Readarr.*.Test.csproj` — confirmed by running `dotnet test
  src/NzbDrone.Core.Test/Readarr.Core.Test.csproj --filter
  "FullyQualifiedName~Quality"` successfully: 123 passed, 0 failed, before
  any task in this plan started). Every `dotnet test` invocation in this
  plan must point at the `.csproj` file directly, not the folder — `dotnet
  test <folder>` fails with `MSB1009: Project file does not exist` on this
  repo because the project file name doesn't match the folder name.
- The .NET SDK is managed via `mise` (see `mise.toml`, pins dotnet
  6.0.428). If `dotnet` is not on PATH directly, prefix commands with
  `mise exec --` (e.g. `mise exec -- dotnet test ...`).
- Run the exact `dotnet test <csproj-path> --filter "..."` command shown in
  each task step after every task; do not proceed to the next task on a red
  build.

---

### Task 1: Remove audio Quality enum values and extension mapping

**Files:**
- Modify: `src/NzbDrone.Core/Qualities/Quality.cs:78-81,93-95,112-114`
- Modify: `src/NzbDrone.Core/MediaFiles/MediaFileExtensions.cs`
- Test: `src/NzbDrone.Core.Test/Qualities/QualityFixture.cs` (check for
  MP3/FLAC/M4B references first — read the file before editing)

**Interfaces:**
- Produces: `MediaFileExtensions.AudioExtensions` returns an empty
  `HashSet<string>` (property stays, dictionary becomes empty) — this is
  what makes Tasks 2-4's branches provably dead code, so later tasks depend
  on this being true.
- Produces: `Quality.All` no longer contains MP3/FLAC/M4B/UnknownAudio;
  `Quality.MP3`, `Quality.FLAC`, `Quality.M4B`, `Quality.UnknownAudio`
  static properties are deleted (any remaining reference is now a compile
  error, which is how Task 2+ dead branches get found mechanically).

- [ ] **Step 1: Read the existing quality test file**

Run: read `src/NzbDrone.Core.Test/Qualities/QualityFixture.cs` in full. Note
any test referencing `Quality.MP3`, `Quality.FLAC`, `Quality.M4B`, or
`Quality.UnknownAudio` — these will need deleting in Step 3.

- [ ] **Step 2: Remove the enum values and definitions from Quality.cs**

Delete lines 78-81 (`MP3`, `FLAC`, `M4B`, `UnknownAudio` static
properties). Delete `UnknownAudio, MP3, M4B, FLAC` from the `All` list
initializer (was lines 92-95, keep the list syntactically valid — trailing
comma after `AZW3` if needed). Delete the three `DefaultQualityDefinitions`
entries for `MP3`, `M4B`, `FLAC` (lines 112-114) and the `UnknownAudio`
entry (line 111).

Result should look like:

```csharp
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
        new QualityDefinition(Quality.Unknown) { Weight = 1, MinSize = 0, MaxSize = 350, GroupWeight = 1 },
        new QualityDefinition(Quality.PDF)     { Weight = 5, MinSize = 0, MaxSize = 350, GroupWeight = 2 },
        new QualityDefinition(Quality.MOBI)    { Weight = 10, MinSize = 0, MaxSize = 350, GroupWeight = 10 },
        new QualityDefinition(Quality.EPUB)    { Weight = 11, MinSize = 0, MaxSize = 350, GroupWeight = 11 },
        new QualityDefinition(Quality.AZW3)    { Weight = 12, MinSize = 0, MaxSize = 350, GroupWeight = 12 },
    };
}
```

- [ ] **Step 3: Delete dead quality tests found in Step 1**

Remove any test method in `QualityFixture.cs` that references the deleted
static properties. Do not remove tests covering `PDF`/`MOBI`/`EPUB`/`AZW3`.

- [ ] **Step 4: Empty the audio extension dictionary in MediaFileExtensions.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles
{
    public static class MediaFileExtensions
    {
        private static readonly Dictionary<string, Quality> _textExtensions;
        private static readonly Dictionary<string, Quality> _audioExtensions;

        static MediaFileExtensions()
        {
            _textExtensions = new Dictionary<string, Quality>(StringComparer.OrdinalIgnoreCase)
            {
                { ".epub", Quality.EPUB },
                { ".kepub", Quality.EPUB },
                { ".mobi", Quality.MOBI },
                { ".azw3", Quality.AZW3 },
                { ".pdf", Quality.PDF },
            };

            _audioExtensions = new Dictionary<string, Quality>(StringComparer.OrdinalIgnoreCase);
        }

        public static HashSet<string> TextExtensions => new HashSet<string>(_textExtensions.Keys, StringComparer.OrdinalIgnoreCase);
        public static HashSet<string> AudioExtensions => new HashSet<string>(_audioExtensions.Keys, StringComparer.OrdinalIgnoreCase);
        public static HashSet<string> AllExtensions => new HashSet<string>(_textExtensions.Keys.Concat(_audioExtensions.Keys), StringComparer.OrdinalIgnoreCase);

        public static Quality GetQualityForExtension(string extension)
        {
            if (_textExtensions.ContainsKey(extension))
            {
                return _textExtensions[extension];
            }

            return Quality.Unknown;
        }
    }
}
```

Note: `_audioExtensions` field and `AudioExtensions` property are kept
(empty) rather than deleted in this task — Tasks 2-4 still reference
`MediaFileExtensions.AudioExtensions` and get cleaned up in the same task
that removes each reference, so the property isn't orphaned mid-plan. Task
4 deletes the field and property once the last consumer is gone.

- [ ] **Step 5: Build and run the quality/media-file test suite**

Run: `mise exec -- dotnet test src/NzbDrone.Core.Test/Readarr.Core.Test.csproj --filter "FullyQualifiedName~Quality|FullyQualifiedName~MediaFileExtensions"`
Expected: build succeeds (any remaining `Quality.MP3` etc. reference is now
a compile error — fix by proceeding to the next tasks, which remove those
references) and the filtered tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/NzbDrone.Core/Qualities/Quality.cs src/NzbDrone.Core/MediaFiles/MediaFileExtensions.cs src/NzbDrone.Core.Test/Qualities/QualityFixture.cs
git commit -m "refactor: remove audio quality enum values and extension mapping"
```

---

### Task 1.5: Fix remaining Quality.MP3/FLAC/M4B/UnknownAudio compile references

Task 1's implementer found three production files, not identified during
planning, that reference the deleted `Quality.MP3`/`Quality.FLAC`/
`Quality.M4B`/`Quality.UnknownAudio` static properties. These are compile
errors after Task 1, unrelated to Tasks 2-4's scope (DistanceCalculator,
ImportApprovedBooks, AudioTagService) — a separate gap this task closes.

**Files:**
- Modify: `src/NzbDrone.Core/Profiles/Qualities/QualityProfileService.cs:107-112`
- Modify: `src/NzbDrone.Core/Parser/QualityParser.cs:74-81,92,116-122`
- Modify: `src/NzbDrone.Core/Organizer/FileNameSampleService.cs:93,105`

- [ ] **Step 1: Remove the "Spoken" default quality profile from QualityProfileService.cs**

The `Handle(ApplicationStartedEvent message)` method sets up two default
profiles: `"eBook"` and `"Spoken"`. The `"Spoken"` profile only lists
audio qualities — with those qualities gone, delete the whole
`AddDefaultProfile("Spoken", ...)` call, keep `"eBook"`:

```csharp
public void Handle(ApplicationStartedEvent message)
{
    if (All().Any())
    {
        return;
    }

    _logger.Info("Setting up default quality profiles");

    AddDefaultProfile("eBook",
        Quality.MOBI,
        Quality.MOBI,
        Quality.EPUB,
        Quality.AZW3);
}
```

- [ ] **Step 2: Remove audio codec branches from QualityParser.cs**

In the `switch (codec)` block (around line 74-93), delete the
`case Codec.FLAC: case Codec.ALAC: case Codec.WAVPACK: result.Quality = Quality.FLAC; break;`
arm, the `case Codec.AAC: result.Quality = Quality.M4B; break;` arm, and
the `case Codec.MP1: case Codec.MP2: case Codec.MP3VBR: case Codec.MP3CBR: case Codec.APE: case Codec.WMA: case Codec.WAV: case Codec.AACVBR: case Codec.OGG: case Codec.OPUS: result.Quality = Quality.MP3; break;`
arm. Result:

```csharp
switch (codec)
{
    case Codec.PDF:
        result.Quality = Quality.PDF;
        break;
    case Codec.EPUB:
        result.Quality = Quality.EPUB;
        break;
    case Codec.MOBI:
        result.Quality = Quality.MOBI;
        break;
    case Codec.AZW3:
        result.Quality = Quality.AZW3;
        break;
    case Codec.Unknown:
    default:
        result.Quality = Quality.Unknown;
        break;
}
```

Further down (around line 116-122), the "Based on category" block sets
`result.Quality = Quality.UnknownAudio;` for a category range check. Delete
that whole `if (categories.Any(x => x >= 3000 && x < 4000))` block:

```csharp
//Based on category
if (result.Quality == Quality.Unknown && categories != null)
{
}
```

Leave this as a genuinely empty guarded block only if removing it entirely
breaks surrounding control flow — read the full method first (it is
`ParseQuality` or similarly named; read the whole function body before
editing) and prefer deleting the entire `if` statement outright if nothing
else depends on the block existing.

Do not touch the `Codec` enum itself, `ParseCodec`, or the PDF/EPUB/MOBI/AZW3
cases — those stay. Do not touch the "Based on extension" block that calls
`MediaFileExtensions.GetQualityForExtension` — Task 1 already made that
ebook-only.

- [ ] **Step 3: Fix FileNameSampleService.cs sample data**

Read the surrounding class first — `_singleTrackFile` and `_multiTrackFile`
are audio-format sample fixtures for a UI naming-preview feature. Since
audio files no longer exist as a quality, replace both `Quality.MP3`
references with an ebook quality so the file still compiles and the sample
still represents a valid `BookFile`:

```csharp
_singleTrackFile = new BookFile
{
    Quality = new QualityModel(Quality.EPUB, new Revision(2)),
    Path = "/books/Author.Name.Book.Name.EPUB.epub",
    SceneName = "Author.Name.Book.Name.EPUB",
    ReleaseGroup = "RlsGrp",
    MediaInfo = mediaInfo,
    Edition = _standardEdition,
    Part = 1,
    PartCount = 1
};

_multiTrackFile = new BookFile
{
    Quality = new QualityModel(Quality.EPUB, new Revision(2)),
    Path = "/books/Author.Name.Book.Name.EPUB.epub",
    SceneName = "Author.Name.Book.Name.EPUB",
    ReleaseGroup = "RlsGrp",
    MediaInfo = mediaInfo,
    Edition = _standardEdition,
    Part = 1,
    PartCount = 2
};
```

Read the file's full context first — if `_singleTrackFile`/`_multiTrackFile`
names or the surrounding `mediaInfo`/`AudioFormat` fixture fields are used
elsewhere in this file in a way that assumes audio (e.g. a test asserting
on "Track" naming tokens), keep the path/name changes minimal and consistent
with what the rest of the file expects — the goal is compiling code with a
valid ebook quality, not a full audio-to-ebook fixture rename if that would
break other assertions in the same file. If in doubt, only change the
`Quality.MP3` → `Quality.EPUB` line and leave paths/names alone.

- [ ] **Step 4: Build the whole Core project and run the Quality/MediaFileExtensions test filter**

Run: `"C:\Users\aincrad\AppData\Local\Microsoft\WinGet\Packages\jdx.mise_Microsoft.Winget.Source_8wekyb3d8bbwe\mise\bin\mise.exe" exec -- dotnet build src/NzbDrone.Core/NzbDrone.Core.csproj`
Expected: build succeeds (no more `Quality.MP3`/`FLAC`/`M4B`/`UnknownAudio`
compile errors network-wide — Tasks 2-4 may still have their own dead-branch
compile errors if not yet done; this step is a checkpoint on this task's
scope, not the whole plan).

Then run: `"C:\Users\aincrad\AppData\Local\Microsoft\WinGet\Packages\jdx.mise_Microsoft.Winget.Source_8wekyb3d8bbwe\mise\bin\mise.exe" exec -- dotnet test src/NzbDrone.Core.Test/Readarr.Core.Test.csproj --filter "FullyQualifiedName~Quality|FullyQualifiedName~MediaFileExtensions|FullyQualifiedName~FileNameSampleService|FullyQualifiedName~QualityProfileService"`
Expected: PASS (this may still fail to build if Tasks 2-4's files haven't
landed yet — if so, note that in the report the same way Task 1 did, and
confirm via `dotnet build src/NzbDrone.Core/NzbDrone.Core.csproj` that
this task's own files compile clean).

- [ ] **Step 5: Commit**

```bash
git add src/NzbDrone.Core/Profiles/Qualities/QualityProfileService.cs src/NzbDrone.Core/Parser/QualityParser.cs src/NzbDrone.Core/Organizer/FileNameSampleService.cs
git commit -m "refactor: remove remaining audio quality references in profile/parser/sample services"
```

---

### Task 2: Remove audio branch from DistanceCalculator

**Files:**
- Modify: `src/NzbDrone.Core/MediaFiles/BookImport/Identification/DistanceCalculator.cs:27,140-157`
- Test: `src/NzbDrone.Core.Test/MediaFiles/BookImport/Identification/DistanceCalculatorFixture.cs`
  (read first — check for any test setting up audio-format files)

**Interfaces:**
- Consumes: `MediaFileExtensions.AudioExtensions` (now always empty, from
  Task 1) — this task removes the branch that depended on it, it does not
  depend on any new interface.

- [ ] **Step 1: Read the existing distance calculator test file**

Run: read `src/NzbDrone.Core.Test/MediaFiles/BookImport/Identification/DistanceCalculatorFixture.cs`
in full. Note any test that sets up an audio file extension (`.mp3`,
`.m4b`, `.flac`) to exercise the `isAudio` branch — delete those tests in
Step 3, they test code that no longer exists.

- [ ] **Step 2: Simplify the format-scoring block**

Replace lines 139-157 (from `// try to tilt it towards the correct "type" of release`
through the closing brace of the `if (edition.Format.IsNotNullOrWhiteSpace())`
block):

```csharp
// try to tilt it towards the correct "type" of release
if (edition.Format.IsNotNullOrWhiteSpace())
{
    // text books should prefer ebook formats
    dist.AddBool("ebook_format", !EbookFormats.Contains(edition.Format));
}
```

Delete line 27 (`private static readonly List<string> AudiobookFormats = ...`).

- [ ] **Step 3: Delete dead audio-branch tests found in Step 1**

Remove test methods that only exercised the deleted `isAudio` / `wrong_format`
/ `audio_format` scoring paths.

- [ ] **Step 4: Run the distance calculator tests**

Run: `mise exec -- dotnet test src/NzbDrone.Core.Test/Readarr.Core.Test.csproj --filter "FullyQualifiedName~DistanceCalculator"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/NzbDrone.Core/MediaFiles/BookImport/Identification/DistanceCalculator.cs src/NzbDrone.Core.Test/MediaFiles/BookImport/Identification/DistanceCalculatorFixture.cs
git commit -m "refactor: remove audiobook format scoring from DistanceCalculator"
```

---

### Task 3: Remove audio-only part-number inference from ImportApprovedBooks

**Files:**
- Modify: `src/NzbDrone.Core/MediaFiles/BookImport/ImportApprovedBooks.cs:133-142`
  (read the surrounding method first to confirm exact line range before
  editing — line numbers may have shifted)
- Test: `src/NzbDrone.Core.Test/MediaFiles/BookImport/ImportApprovedBooksFixture.cs`
  (read first — check for any test exercising audio part-number inference)

- [ ] **Step 1: Read the existing import test file**

Run: read `src/NzbDrone.Core.Test/MediaFiles/BookImport/ImportApprovedBooksFixture.cs`
in full. Note any test that relies on all-audio-file part-number inference
— delete it in Step 3.

- [ ] **Step 2: Delete the audio-only part-number block**

Remove:

```csharp
// Make sure part numbers are populated for audiobooks
// If all audio files and all part numbers are zero, set them by filename order
if (decisionList.All(b => MediaFileExtensions.AudioExtensions.Contains(Path.GetExtension(b.Item.Path)) && b.Item.Part == 0))
{
    var part = 1;
    foreach (var d in decisionList.OrderBy(x => PadNumbers.Replace(x.Item.Path)))
    {
        d.Item.Part = part++;
    }
}
```

Leave any surrounding non-audio import logic untouched.

- [ ] **Step 3: Delete dead tests found in Step 1**

- [ ] **Step 4: Run the import tests**

Run: `mise exec -- dotnet test src/NzbDrone.Core.Test/Readarr.Core.Test.csproj --filter "FullyQualifiedName~ImportApprovedBooks"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/NzbDrone.Core/MediaFiles/BookImport/ImportApprovedBooks.cs src/NzbDrone.Core.Test/MediaFiles/BookImport/ImportApprovedBooksFixture.cs
git commit -m "refactor: remove audiobook part-number inference from import"
```

---

### Task 4: Delete AudioTagService and its config options

**Files:**
- Delete: `src/NzbDrone.Core/MediaFiles/AudioTag.cs`
- Delete: `src/NzbDrone.Core/MediaFiles/AudioTagService.cs`
- Delete: `src/NzbDrone.Core.Test/MediaFiles/AudioTagServiceFixture.cs`
- Delete: `src/NzbDrone.Core/Configuration/WriteAudioTagsType.cs`
- Modify: `src/NzbDrone.Core/MediaFiles/MetadataTagService.cs`
- Modify: `src/NzbDrone.Core/Configuration/IConfigService.cs`
- Modify: `src/NzbDrone.Core/Configuration/ConfigService.cs`
- Modify: `frontend/src/Settings/Metadata/MetadataProvider/MetadataProvider.js`
- Modify: `src/NzbDrone.Core/Localization/Core/en.json` (and the 12 other
  locale JSON files listed below, same key removal in each)
- Modify: `src/NzbDrone.Core/MediaFiles/MediaFileExtensions.cs` (remove
  now-unused `_audioExtensions` field and `AudioExtensions` property)

Locale files with `WriteAudioTags`/`ScrubAudioTags` translation keys:
`ca.json`, `de.json`, `el.json`, `en.json`, `es.json`, `fi.json`, `fr.json`,
`hu.json`, `pt.json`, `pt_BR.json`, `sv.json`, `uk.json`, `zh_CN.json`.

**Interfaces:**
- Consumes: none new.
- Produces: `MetadataTagService` no longer implements audio tag dispatch —
  `ReadTags`/`WriteTags`/`SyncTags`/`GetRetagPreviewsByAuthor`/
  `GetRetagPreviewsByBook` delegate to `IEBookTagService` only.

- [ ] **Step 1: Read IConfigService.cs and ConfigService.cs for the exact WriteAudioTags/ScrubAudioTags property declarations**

Run: grep `WriteAudioTags|ScrubAudioTags` in both files and read the
surrounding 5 lines of each match before editing, so the property removal
doesn't break adjacent unrelated config properties.

- [ ] **Step 2: Simplify MetadataTagService.cs to ebook-only**

```csharp
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using NLog;
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
        private readonly Logger _logger;

        public MetadataTagService(IEBookTagService eBookTagService,
            Logger logger)
        {
            _eBookTagService = eBookTagService;

            _logger = logger;
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
```

- [ ] **Step 3: Delete AudioTag.cs, AudioTagService.cs, AudioTagServiceFixture.cs, WriteAudioTagsType.cs**

```bash
rm src/NzbDrone.Core/MediaFiles/AudioTag.cs
rm src/NzbDrone.Core/MediaFiles/AudioTagService.cs
rm src/NzbDrone.Core.Test/MediaFiles/AudioTagServiceFixture.cs
rm src/NzbDrone.Core/Configuration/WriteAudioTagsType.cs
```

- [ ] **Step 4: Remove WriteAudioTags/ScrubAudioTags properties from IConfigService.cs and ConfigService.cs**

Using the exact declarations read in Step 1, remove the `WriteAudioTags`
and `ScrubAudioTags` property declarations (interface signature in
`IConfigService.cs`, implementation/backing config key in
`ConfigService.cs`). Do not touch any `WriteEbookTags` / ebook-equivalent
property — those stay.

- [ ] **Step 5: Remove writeAudioTags field from MetadataProvider.js**

Read `frontend/src/Settings/Metadata/MetadataProvider/MetadataProvider.js`
in full first. Remove the form field/section wired to `writeAudioTags` (and
`scrubAudioTags` if present as a separate field), following the same
pattern used for the ebook-equivalent field that remains. Do not remove the
ebook tag-writing field.

- [ ] **Step 6: Remove WriteAudioTags/ScrubAudioTags translation keys from all 13 locale files**

For each of `ca.json`, `de.json`, `el.json`, `en.json`, `es.json`,
`fi.json`, `fr.json`, `hu.json`, `pt.json`, `pt_BR.json`, `sv.json`,
`uk.json`, `zh_CN.json`: remove the JSON key(s) matching
`WriteAudioTags*`/`ScrubAudioTags*` (read each file's matching key names
first — locale files use the same English key names as values, so `grep
-n "WriteAudioTags\|ScrubAudioTags"` on each file gives the exact line to
remove). Keep the JSON valid (no trailing commas).

- [ ] **Step 7: Delete the now-unused audio extension field from MediaFileExtensions.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles
{
    public static class MediaFileExtensions
    {
        private static readonly Dictionary<string, Quality> _textExtensions;

        static MediaFileExtensions()
        {
            _textExtensions = new Dictionary<string, Quality>(StringComparer.OrdinalIgnoreCase)
            {
                { ".epub", Quality.EPUB },
                { ".kepub", Quality.EPUB },
                { ".mobi", Quality.MOBI },
                { ".azw3", Quality.AZW3 },
                { ".pdf", Quality.PDF },
            };
        }

        public static HashSet<string> TextExtensions => new HashSet<string>(_textExtensions.Keys, StringComparer.OrdinalIgnoreCase);
        public static HashSet<string> AllExtensions => new HashSet<string>(_textExtensions.Keys, StringComparer.OrdinalIgnoreCase);

        public static Quality GetQualityForExtension(string extension)
        {
            if (_textExtensions.ContainsKey(extension))
            {
                return _textExtensions[extension];
            }

            return Quality.Unknown;
        }
    }
}
```

Note: `AllExtensions` changes from a `Concat` of two key sets to just
`_textExtensions.Keys` — verify no caller of `AllExtensions` still expects
audio extensions in the result (grep `AllExtensions` across `src/` and
confirm each call site is fine with ebook-only extensions).

- [ ] **Step 8: Build and run the full Core test project**

Run: `mise exec -- dotnet test src/NzbDrone.Core.Test/Readarr.Core.Test.csproj`
Expected: PASS, zero build errors referencing `AudioTag`, `WriteAudioTags`,
or `ScrubAudioTags`.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "refactor: remove audio tag service, config options, and settings UI field"
```

---

### Task 5: Remove Audiobookshelf notifier (if present on this branch)

This branch was cut from `origin/develop`, which does not yet contain the
Audiobookshelf notifier (added in commit `2fc6d7236` on branch `update`,
not yet merged to `develop`). Check before acting — do not fail the plan if
the folder doesn't exist.

**Files:**
- Delete (if present): `src/NzbDrone.Core/Notifications/Audiobookshelf/`
  (`Audiobookshelf.cs`, `AudiobookshelfProxy.cs`, `AudiobookshelfSettings.cs`)
- Modify (if present): whichever notifier registry file lists all notifier
  implementations (same pattern as Plex/Kavita/Subsonic registration — grep
  `Audiobookshelf` to find it)

- [ ] **Step 1: Check whether the notifier exists on this branch**

Run: `ls src/NzbDrone.Core/Notifications/Audiobookshelf/ 2>/dev/null || echo "not present"`

If "not present": skip to Step 4 (nothing to do, commit is a no-op — do not
create an empty commit, just mark this task done).

- [ ] **Step 2: Delete the notifier folder and its registration**

```bash
rm -rf src/NzbDrone.Core/Notifications/Audiobookshelf/
```

Grep `Audiobookshelf` across `src/NzbDrone.Core` and `frontend/src` for any
remaining reference (registry list, frontend notification type dropdown)
and remove each.

- [ ] **Step 3: Run the notifications test suite**

Run: `mise exec -- dotnet test src/NzbDrone.Core.Test/Readarr.Core.Test.csproj --filter "FullyQualifiedName~Notifications"`
Expected: PASS

- [ ] **Step 4: Commit (only if changes were made in Step 2)**

```bash
git add -A
git commit -m "refactor: remove Audiobookshelf notification connector"
```

---

### Task 6: Full-repo verification sweep

**Files:** none modified — verification only.

- [ ] **Step 1: Grep for any remaining audio-format references in src/**

Run: `grep -rn "MP3\|FLAC\|M4B\|AudioTag\|WriteAudioTags\|ScrubAudioTags\|Audiobookshelf" src/NzbDrone.Core src/NzbDrone.Api.V1 src/Readarr.Api.V1 --include="*.cs"`
Expected: no matches (or only matches in comments/strings unrelated to the
removed features — inspect any hit manually).

- [ ] **Step 2: Grep frontend for the same terms**

Run: `grep -rln "writeAudioTags\|scrubAudioTags\|Audiobookshelf" frontend/src`
Expected: no matches.

- [ ] **Step 3: Run the full test suite**

Run: `mise exec -- dotnet test src/NzbDrone.Core.Test/Readarr.Core.Test.csproj`
(and any other `*.Test` project listed in `src/Readarr.sln` if unsure —
each follows the same `Readarr.<Area>.Test.csproj` naming pattern inside
its `NzbDrone.<Area>.Test` folder)
Expected: all PASS, zero build warnings about unresolved `Quality.MP3`
etc.

- [ ] **Step 4: Update README.md project description**

Read `README.md`, remove "audiobook" from the project description line if
present, keep "ebook" language intact.

- [ ] **Step 5: Commit**

```bash
git add README.md
git commit -m "docs: update README to reflect ebook-only scope"
```
