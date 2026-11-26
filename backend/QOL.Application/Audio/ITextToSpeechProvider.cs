namespace QOL.Application.Audio;

public interface ITextToSpeechProvider
{
    Task<byte[]> SynthesizeAsync(
        string text,
        string languageCode,
        CancellationToken cancellationToken);
}
