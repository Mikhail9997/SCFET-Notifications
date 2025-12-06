using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace TelegramBotAdministrators.Services;

public class RedisCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCache> _logger;
    
    public RedisCache(IConnectionMultiplexer redis, ILogger<RedisCache> logger)
    {
        _redis = redis;
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from Redis for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, serializedValue, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in Redis for key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking key existence in Redis for key {Key}", key);
            return false;
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing key {key}", key);
        }
    }

    public async Task<bool> AcquireLockAsync(string key, TimeSpan expiry)
    {
        try
        {
            return await _database.LockTakeAsync(key, Environment.MachineName, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring lock in Redis for key {Key}", key);
            return false;
        }
    }

    public async Task ReleaseLockAsync(string key)
    {
        try
        {
            await _database.LockReleaseAsync(key, Environment.MachineName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock in Redis for key {Key}", key);
        }
    }
}
