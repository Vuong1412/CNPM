using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QOL.Domain.Entities;
using QOL.Infrastructure.Persistence;

namespace QOL.API.SeedData;

public static class DevelopmentDataSeeder
{
    private const string DemoEmail = "demo@qol.local";
    private const string DemoPassword = "QolDemo!2026";
    private const string SampleAudioTitle = "QOL sample audio";
    private const int SampleAudioDurationSeconds = 3;
    private const int SampleAudioSampleRate = 22050;

    private static readonly (string Name, string Address)[] SampleLocations =
    [
        ("Phố cổ Hội An", "Hội An, Quảng Nam"),
        ("Chùa Cầu", "Nguyễn Thị Minh Khai, Hội An"),
        ("Hội quán Phúc Kiến", "46 Trần Phú, Hội An")
    ];

    public static async Task SeedAsync(
        QolDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        string audioDirectory,
        CancellationToken cancellationToken = default)
    {
        foreach (var sample in SampleLocations)
        {
            if (!await dbContext.Locations.AnyAsync(
                    location => location.Name == sample.Name,
                    cancellationToken))
            {
                dbContext.Locations.Add(new Location(sample.Name, sample.Address));
            }
        }

        var demoUserExists = await dbContext.Users.AnyAsync(
            user => user.Email == DemoEmail,
            cancellationToken);
        if (!demoUserExists)
        {
            var demoUser = new User(DemoEmail, "QOL Demo User");
            demoUser.SetPasswordHash(passwordHasher.HashPassword(demoUser, DemoPassword));
            dbContext.Users.Add(demoUser);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var location = await dbContext.Locations.SingleAsync(
            item => item.Name == SampleLocations[0].Name,
            cancellationToken);
        var language = await dbContext.Languages.SingleAsync(
            item => item.Code == "vi",
            cancellationToken);
        var audioExists = await dbContext.AudioFiles.AnyAsync(
            audio => audio.LocationId == location.Id &&
                audio.LanguageId == language.Id &&
                audio.Title == SampleAudioTitle,
            cancellationToken);
        if (audioExists)
        {
            return;
        }

        Directory.CreateDirectory(audioDirectory);
        var fileName = $"qol-sample-{Guid.NewGuid():N}.wav";
        var filePath = Path.Combine(audioDirectory, fileName);
        await WriteSampleWaveAsync(filePath, cancellationToken);

        try
        {
            dbContext.AudioFiles.Add(new AudioFile(
                location.Id,
                language.Id,
                SampleAudioTitle,
                "Âm thanh mẫu được tạo bằng mã nguồn của dự án.",
                $"/media/audio/{fileName}",
                SampleAudioDurationSeconds));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            File.Delete(filePath);
            throw;
        }
    }

    private static async Task WriteSampleWaveAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        var sampleCount = SampleAudioSampleRate * SampleAudioDurationSeconds;
        var dataSize = sampleCount * channels * bitsPerSample / 8;
        var header = new byte[44];
        using (var stream = new MemoryStream(header))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(SampleAudioSampleRate);
            writer.Write(SampleAudioSampleRate * channels * bitsPerSample / 8);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            writer.Write("data"u8);
            writer.Write(dataSize);
        }

        await using var output = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await output.WriteAsync(header, cancellationToken);

        var sampleBuffer = new byte[8192];
        var bytesWritten = 0;
        for (var index = 0; index < sampleCount; index++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * 440 * index / SampleAudioSampleRate) * 1000);
            var offset = bytesWritten % sampleBuffer.Length;
            sampleBuffer[offset] = (byte)(sample & 0xFF);
            sampleBuffer[offset + 1] = (byte)((sample >> 8) & 0xFF);
            bytesWritten += 2;

            if (bytesWritten % sampleBuffer.Length == 0)
            {
                await output.WriteAsync(sampleBuffer, cancellationToken);
            }
        }

        var remaining = bytesWritten % sampleBuffer.Length;
        if (remaining > 0)
        {
            await output.WriteAsync(sampleBuffer.AsMemory(0, remaining), cancellationToken);
        }
    }
}
