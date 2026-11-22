namespace Hackmum.Bethuya.Infrastructure.Services;

internal sealed class ArchivePreviewMismatchException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
