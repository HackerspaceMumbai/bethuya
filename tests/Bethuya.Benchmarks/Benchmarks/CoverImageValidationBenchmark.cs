using BenchmarkDotNet.Attributes;

namespace Bethuya.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks the cover image URL validation and magic-byte checking patterns
/// used in EventEndpoints and ImageEndpoints.
/// Target: 0 B hot-path allocations.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CoverImageValidationBenchmark
{
    private const int MaxUrlLength = 2048;

    private string _validUrl = null!;
    private string _invalidUrl = null!;
    private string _longUrl = null!;
    private byte[] _jpegHeader = null!;
    private byte[] _pngHeader = null!;
    private byte[] _webpHeader = null!;
    private byte[] _gifHeader = null!;
    private byte[] _invalidHeader = null!;

    [GlobalSetup]
    public void Setup()
    {
        _validUrl = "https://res.cloudinary.com/demo/image/upload/v1234567890/cover.jpg";
        _invalidUrl = "ftp://not-http.example.com/image.jpg";
        _longUrl = "https://example.com/" + new string('a', 3000);

        _jpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
        _pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        _webpHeader = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
        _gifHeader = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        _invalidHeader = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B];
    }

    [Benchmark(Baseline = true)]
    public bool ValidateUrl_Valid()
    {
        return ValidateCoverImageUrl(_validUrl);
    }

    [Benchmark]
    public bool ValidateUrl_Invalid()
    {
        return ValidateCoverImageUrl(_invalidUrl);
    }

    [Benchmark]
    public bool ValidateUrl_TooLong()
    {
        return ValidateCoverImageUrl(_longUrl);
    }

    [Benchmark]
    public bool ValidateUrl_Null()
    {
        return ValidateCoverImageUrl(null);
    }

    [Benchmark]
    public bool MagicBytes_Jpeg()
    {
        return IsValidImageContent(_jpegHeader);
    }

    [Benchmark]
    public bool MagicBytes_Png()
    {
        return IsValidImageContent(_pngHeader);
    }

    [Benchmark]
    public bool MagicBytes_WebP()
    {
        return IsValidImageContent(_webpHeader);
    }

    [Benchmark]
    public bool MagicBytes_Gif()
    {
        return IsValidImageContent(_gifHeader);
    }

    [Benchmark]
    public bool MagicBytes_Invalid()
    {
        return IsValidImageContent(_invalidHeader);
    }

    /// <summary>Replicates the validation pattern from EventEndpoints.ValidateCoverImageUrl.</summary>
    private static bool ValidateCoverImageUrl(string? coverImageUrl)
    {
        if (string.IsNullOrEmpty(coverImageUrl))
            return true; // null/empty is valid (field is optional)

        if (coverImageUrl.Length > MaxUrlLength)
            return false;

        if (!Uri.TryCreate(coverImageUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return false;

        return true;
    }

    /// <summary>Replicates the magic-byte checking pattern from ImageEndpoints.IsValidImageContentAsync.</summary>
    private static bool IsValidImageContent(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4) return false;

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return true;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header.Length >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return true;

        // GIF: GIF87a or GIF89a
        if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38
            && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61)
            return true;

        // WebP: RIFF....WEBP
        if (header.Length >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return true;

        return false;
    }
}
