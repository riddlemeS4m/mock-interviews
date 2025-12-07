using System.ComponentModel.DataAnnotations;

namespace sp2023_mis421_mockinterviews.Options;

public sealed class GoogleDriveOptions
{
    [Required]
    public string ApplicationName { get; set; } = "Mock Interviews App ASP.Net Core MVC";
    public string SiteContentFolderId { get; set; } = default!;
    public string ResumesFolderId { get; set; } = default!;
    public string PfpsFolderId { get; set; } = default!;
}

public sealed class GoogleCredentialOptions
{
    [Required]
    public string type { get; init; } = default!;
    [Required]
    public string project_id { get; init; } = default!;
    [Required]
    public string private_key_id { get; init; } = default!;
    [Required]
    public string private_key { get; init; } = default!;
    [Required]
    public string client_email { get; init; } = default!;
    [Required]
    public string client_id { get; init; } = default!;
    [Required]
    public string auth_uri { get; init; } = default!;
    [Required]
    public string token_uri { get; init; } = default!;
    [Required]
    public string auth_provider_x509_cert_url { get; init; } = default!;
    [Required]
    public string client_x509_cert_url { get; init; } = default!;
}