using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.Audiobookshelf
{
    public class AudiobookshelfSettingsValidator : AbstractValidator<AudiobookshelfSettings>
    {
        public AudiobookshelfSettingsValidator()
        {
            RuleFor(c => c.Host).ValidHost();
            RuleFor(c => c.Port).InclusiveBetween(1, 65535);
            RuleFor(c => c.ApiKey).NotEmpty();
            RuleFor(c => c.UrlBase).ValidUrlBase().When(c => c.UrlBase.IsNotNullOrWhiteSpace());
        }
    }

    public class AudiobookshelfSettings : IProviderConfig
    {
        private static readonly AudiobookshelfSettingsValidator Validator = new AudiobookshelfSettingsValidator();

        public AudiobookshelfSettings()
        {
            Port = 13378;
        }

        [FieldDefinition(0, Label = "Host")]
        public string Host { get; set; }

        [FieldDefinition(1, Label = "Port")]
        public int Port { get; set; }

        [FieldDefinition(2, Label = "Use SSL", Type = FieldType.Checkbox, HelpText = "Connect to Audiobookshelf over HTTPS instead of HTTP")]
        public bool UseSsl { get; set; }

        [FieldDefinition(3, Label = "Url Base", Type = FieldType.Textbox, Advanced = true, HelpText = "Adds a prefix to the Audiobookshelf url, e.g. http://[host]:[port]/[urlBase]")]
        public string UrlBase { get; set; }

        [FieldDefinition(4, Label = "API Key", Privacy = PrivacyLevel.ApiKey, HelpText = "Audiobookshelf API key (Settings > Users > API Keys)")]
        public string ApiKey { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
