using Google.Apis.Download;
using Google.Apis.Upload;
using sp2023_mis421_mockinterviews.Interfaces.IServices;
using sp2023_mis421_mockinterviews.Options;
using System.Text.Json;

namespace sp2023_mis421_mockinterviews.Services.GoogleDrive
{
    public class GoogleDriveUtility
    {
        private readonly ILogger<IGoogleDrive> _logger;

        public GoogleDriveUtility(ILogger<IGoogleDrive> logger)
        {
            _logger = logger;
        }

        private static readonly Dictionary<string, string> MimeTypes = new()
        {
            { ".pdf", "application/pdf" },
            { ".doc", "application/msword" },
            { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            { ".jpg", "image/jpeg" },
            { ".png", "image/png" },
            // Add other MIME types as needed
        };

        public static string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return MimeTypes.ContainsKey(extension) ? MimeTypes[extension] : "application/octet-stream";
        }

        public static string SerializeCredentials(IConfigurationSection googleCredentialSection)
        {
            var googleCredentialJson = new
            {
                type = googleCredentialSection["type"],
                project_id = googleCredentialSection["project_id"],
                private_key_id = googleCredentialSection["private_key_id"],
                private_key = googleCredentialSection["private_key"],
                client_email = googleCredentialSection["client_email"],
                client_id = googleCredentialSection["client_id"],
                auth_uri = googleCredentialSection["auth_uri"],
                token_uri = googleCredentialSection["token_uri"],
                auth_provider_x509_cert_url = googleCredentialSection["auth_provider_x509_cert_url"],
                client_x509_cert_url = googleCredentialSection["client_x509_cert_url"]
            };

            return JsonSerializer.Serialize(googleCredentialJson);
        }

        // Overload for Options pattern
        public static string SerializeCredentials(GoogleCredentialOptions credentialOptions)
        {
            var googleCredentialJson = new
            {
                type = credentialOptions.type,
                project_id = credentialOptions.project_id,
                private_key_id = credentialOptions.private_key_id,
                private_key = credentialOptions.private_key,
                client_email = credentialOptions.client_email,
                client_id = credentialOptions.client_id,
                auth_uri = credentialOptions.auth_uri,
                token_uri = credentialOptions.token_uri,
                auth_provider_x509_cert_url = credentialOptions.auth_provider_x509_cert_url,
                client_x509_cert_url = credentialOptions.client_x509_cert_url
            };

            return JsonSerializer.Serialize(googleCredentialJson);
        }

        public void Upload_ProgressChanged(IUploadProgress progress)
        {
            switch (progress.Status)
            {
                case UploadStatus.Uploading:
                    _logger.LogInformation($"Uploading: {progress.BytesSent} bytes sent.");
                    break;
                case UploadStatus.Completed:
                    _logger.LogInformation("Upload completed successfully.");
                    break;
                case UploadStatus.Failed:
                    _logger.LogError($"Upload failed: {progress.Exception.Message}");
                    break;
            }
        }

        public void Download_ProgressChanged(IDownloadProgress progress)
        {
            switch (progress.Status)
            {
                case DownloadStatus.Downloading:
                    _logger.LogInformation($"Downloading: {progress.BytesDownloaded} bytes downloaded.");
                    break;
                case DownloadStatus.Completed:
                    _logger.LogInformation("Download completed successfully.");
                    break;
                case DownloadStatus.Failed:
                    _logger.LogError($"Download failed: {progress.Exception.Message}");
                    break;
            }
        }
    }
}