using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace ProductShop.Api.Services;

// Password reset er OTP memory te thake (database lage na). API restart hole purono OTP batil.
public class OtpStore
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResendGap = TimeSpan.FromMinutes(1);
    private const int MaxAttempts = 5;

    private readonly IMemoryCache _cache;

    public OtpStore(IMemoryCache cache) => _cache = cache;

    private sealed class Entry
    {
        public required string Code { get; init; }
        public DateTime SentAt { get; init; }
        public int Attempts;
    }

    private static string Key(int userId) => $"pw-reset-otp:{userId}";

    // Notun 6 digit OTP; 1 minute er moddhe abar chaile null
    public string? Create(int userId)
    {
        if (_cache.TryGetValue(Key(userId), out Entry? old) && DateTime.UtcNow - old!.SentAt < ResendGap)
            return null;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        _cache.Set(Key(userId), new Entry { Code = code, SentAt = DateTime.UtcNow }, Lifetime);
        return code;
    }

    // Thik hole OTP muche jay (ekbar-i use kora jay). 5 bar vul dile o muche jay.
    public bool Verify(int userId, string code)
    {
        if (!_cache.TryGetValue(Key(userId), out Entry? entry)) return false;

        if (Interlocked.Increment(ref entry!.Attempts) > MaxAttempts)
        {
            _cache.Remove(Key(userId));
            return false;
        }

        var ok = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(entry.Code), Encoding.ASCII.GetBytes(code ?? string.Empty));
        if (ok) _cache.Remove(Key(userId));
        return ok;
    }
}
