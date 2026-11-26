using QOL.Application.Audio;

namespace QOL.Infrastructure.Audio;

public sealed class DisabledTextToSpeechProvider : ITextToSpeechProvider
{
    public Task<byte[]> SynthesizeAsync(
        string text,
        string languageCode,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Text-to-speech is not configured.");
}
