using System.Security;
using System.Text;
using Microsoft.Extensions.Options;
using QOL.Application.Audio;

namespace QOL.Infrastructure.Audio;

public sealed class AzureSpeechTtsProvider(
    HttpClient httpClient,
    IOptions<AzureSpeechOptions> options) : ITextToSpeechProvider
{
    private readonly AzureSpeechOptions _options = options.Value;

    public async Task<byte[]> SynthesizeAsync(
        string text,
        string languageCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.SubscriptionKey))
        {
            throw new InvalidOperationException("Azure Speech is enabled but its endpoint or key is missing.");
        }
        if (string.IsNullOrWhiteSpace(text) || text.Length > 5000)
        {
            throw new InvalidOperationException("TTS text must contain between 1 and 5000 characters.");
        }

        var (locale, voice) = GetVoice(languageCode);
        var escapedText = SecurityElement.Escape(text) ?? string.Empty;
        var ssml = $"<speak version=\"1.0\" xml:lang=\"{locale}\" xmlns=\"http://www.w3.org/2001/10/synthesis\"><voice name=\"{voice}\">{escapedText}</voice></speak>";
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml")
        };
        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _options.SubscriptionKey);
        request.Headers.TryAddWithoutValidation("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static (string Locale, string Voice) GetVoice(string languageCode) =>
        languageCode.Trim().ToLowerInvariant() switch
        {
            "vi" => ("vi-VN", "vi-VN-HoaiMyNeural"),
            "en" => ("en-US", "en-US-JennyNeural"),
            "ko" => ("ko-KR", "ko-KR-SunHiNeural"),
            "zh" => ("zh-CN", "zh-CN-XiaoxiaoNeural"),
            "ja" => ("ja-JP", "ja-JP-NanamiNeural"),
            _ => throw new InvalidOperationException("No configured Azure Speech voice exists for this language.")
        };
}
