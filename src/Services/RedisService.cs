using StackExchange.Redis;

namespace fbtracker.Services;

public interface IRedisService
{
    public void AddValueToDb(string key, string value);
    public string? GetValueFromDb(string key);
    public bool IsExist(string key);

}

public class RedisService : IRedisService
{
    private readonly ILogger<RedisService> _logger;
    private readonly IDatabase _redisDb;

    public RedisService(ILogger<RedisService> logger )
    {
       _logger = logger;
       _redisDb = ConnectionMultiplexer.Connect("redis").GetDatabase();
       
    }
   
    public void AddValueToDb(string key, string value)
    {
        _redisDb.StringSet(key, value, TimeSpan.FromMinutes(20));
        _logger.LogInformation($"Key: {key} with value: {value} was added to db");
    }
    
    public string? GetValueFromDb(string key)
    {
        string value = _redisDb.StringGet(key);
        if (value is not null)
        {
            _logger.LogInformation($"We got data from with key: {key}");
            return value;
        }

        _logger.LogInformation($"Dont exist item with: {key}");
        return default;
    }
    public bool IsExist(string key)
    {
        return _redisDb.KeyExists(key);
    }
  
}