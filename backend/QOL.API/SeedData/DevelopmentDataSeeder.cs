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

    private static readonly (
        string Name,
        string Address,
        decimal Latitude,
        decimal Longitude,
        int RadiusMeters,
        int Priority,
        string OriginalDescription,
        string DirectionLink)[] SampleLocations =
    [
        (
            "Phố cổ Hội An",
            "Hội An, Quảng Nam",
            15.8801m,
            108.3380m,
            500,
            3,
            "Khu phố cổ Hội An bên sông Thu Bồn, nổi bật với kiến trúc giao thoa Việt, Hoa và Nhật.",
            "https://maps.google.com/?q=15.8801,108.3380"),
        (
            "Chùa Cầu",
            "Nguyễn Thị Minh Khai, Hội An",
            15.8775m,
            108.3269m,
            100,
            2,
            "Cây cầu có mái che được xây dựng vào đầu thế kỷ XVII, biểu tượng của Hội An.",
            "https://maps.google.com/?q=15.8775,108.3269"),
        (
            "Hội quán Phúc Kiến",
            "46 Trần Phú, Hội An",
            15.8774m,
            108.3287m,
            100,
            1,
            "Hội quán do cộng đồng người Phúc Kiến xây dựng, thờ Thiên Hậu Thánh Mẫu.",
            "https://maps.google.com/?q=15.8774,108.3287")
    ];

    public static async Task SeedAsync(
        QolDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        string audioDirectory,
        CancellationToken cancellationToken = default)
    {
        foreach (var sample in SampleLocations)
        {
            var existingLocation = await dbContext.Locations.SingleOrDefaultAsync(
                location => location.Name == sample.Name,
                cancellationToken);
            if (existingLocation is null)
            {
                dbContext.Locations.Add(new Location(
                    sample.Name,
                    sample.Address,
                    sample.Latitude,
                    sample.Longitude,
                    sample.RadiusMeters,
                    sample.Priority,
                    sample.OriginalDescription,
                    sample.DirectionLink));
            }
            else if (!existingLocation.Latitude.HasValue || !existingLocation.Longitude.HasValue)
            {
                existingLocation.UpdateDetails(
                    existingLocation.Name,
                    existingLocation.Address,
                    sample.Latitude,
                    sample.Longitude,
                    sample.RadiusMeters,
                    sample.Priority,
                    sample.OriginalDescription,
                    sample.DirectionLink,
                    existingLocation.Status);
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
