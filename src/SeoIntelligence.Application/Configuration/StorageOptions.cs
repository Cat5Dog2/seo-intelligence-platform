namespace SeoIntelligence.Application.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public const string LocalProvider = "Local";
    public const string MinioProvider = "MinIO";

    public string Provider { get; set; } = LocalProvider;

    public string? BasePath { get; set; } = "./.data/storage";

    public string? Endpoint { get; set; }

    public string BucketName { get; set; } = "seo-intelligence";

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.Equals(Provider, LocalProvider, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(BasePath))
            {
                errors.Add("Storage:BasePath is required when Storage:Provider is Local.");
            }
        }
        else if (string.Equals(Provider, MinioProvider, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(BucketName))
            {
                errors.Add("Storage:BucketName is required when Storage:Provider is MinIO.");
            }

            if (string.IsNullOrWhiteSpace(Endpoint)
                || !IsHttpUri(Endpoint))
            {
                errors.Add("Storage:Endpoint must be an absolute URI when Storage:Provider is MinIO.");
            }
        }
        else
        {
            errors.Add("Storage:Provider must be Local or MinIO.");
        }

        return errors;
    }

    private static bool IsHttpUri(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
