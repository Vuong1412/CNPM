using QOL.Domain.Entities;

namespace QOL.Infrastructure.Persistence.SeedData;

internal static class LanguageSeedData
{
    public static readonly Language[] Languages =
    [
        new(Guid.Parse("10000000-0000-0000-0000-000000000001"), "vi", "Vietnamese", "Tiếng Việt"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000002"), "en", "English", "English"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000003"), "ko", "Korean", "한국어"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000004"), "zh", "Chinese", "中文"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000005"), "ja", "Japanese", "日本語")
    ];
}
