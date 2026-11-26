using QOL.Application.Audio;

namespace QOL.Infrastructure.Audio;

public sealed class FakeTextToSpeechProvider : ITextToSpeechProvider
{
    public Task<byte[]> SynthesizeAsync(
        string text,
        string languageCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(languageCode))
        {
            throw new ArgumentException("Text and language are required.");
        }

        const int sampleRate = 8000;
        const int sampleCount = sampleRate;
        using var stream = new MemoryStream(44 + sampleCount);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + sampleCount);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate);
        writer.Write((short)1);
        writer.Write((short)8);
        writer.Write("data"u8);
        writer.Write(sampleCount);
        writer.Write(new byte[sampleCount]);
        writer.Flush();
        return Task.FromResult(stream.ToArray());
    }
}
