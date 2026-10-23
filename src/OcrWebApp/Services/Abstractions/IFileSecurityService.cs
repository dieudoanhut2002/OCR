namespace OcrWebApp.Services.Abstractions;

public interface IFileSecurityService
{
    /// <summary>
    /// Validates file size, extension, and binary magic bytes (JPEG, PNG, TIFF, PDF).
    /// </summary>
    (bool IsValid, string DetectedContentType, string? ErrorMessage) ValidateFile(IFormFile file);

    /// <summary>
    /// Saves the uploaded file to the secure storage directory under a randomized GUID name.
    /// Returns the generated stored file name.
    /// </summary>
    Task<string> SaveUploadedFileAsync(IFormFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a raw byte buffer as a file in secure storage (e.g. rendered page preview).
    /// </summary>
    Task<string> SaveFileBytesAsync(byte[] bytes, string extension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the absolute path for a stored file, enforcing boundary checks against path traversal.
    /// </summary>
    string GetSafeStoragePath(string storedFileName);

    /// <summary>
    /// Safely deletes a stored file.
    /// </summary>
    void DeleteStoredFile(string storedFileName);

    /// <summary>
    /// Checks if a stored file exists on disk.
    /// </summary>
    bool FileExists(string storedFileName);
}
