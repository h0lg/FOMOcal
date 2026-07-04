using System.Text.Json;
using System.Text.Json.Serialization;

namespace FomoCal;

public class SetJsonFileRepository<T>(JsonFileStore store, string fileName) where T : class
{
    public async Task<HashSet<T>> LoadAllAsync() => await store.LoadAsync<HashSet<T>>(fileName) ?? [];
    public Task SaveCompleteAsync(ISet<T> items) => store.SaveAsync(fileName, items);
}

public class SingletonJsonFileRepository<T>(JsonFileStore store, string fileName) where T : class
{
    public async Task<T?> LoadAsync() => await store.LoadAsync<T>(fileName);
    public Task SaveAsync(T value) => store.SaveAsync(fileName, value);
}

public class JsonFileStore(string storagePath)
{
    private static readonly SemaphoreSlim locker = new(1, 1);

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static string Serialize<T>(T value) => JsonSerializer.Serialize(value, jsonOptions);
    private string GetFilePath(string fileName) => Path.Combine(storagePath, fileName + ".json");

    public async Task SaveAsync<T>(string fileName, T value)
    {
        string filePath = GetFilePath(fileName);
        string json = Serialize(value);
        await locker.WaitAsync();

        try { await File.WriteAllTextAsync(filePath, json); }
        finally { locker.Release(); }
    }

    public async Task<T?> LoadAsync<T>(string fileName)
    {
        string filePath = GetFilePath(fileName);
        if (!File.Exists(filePath)) return default;
        await locker.WaitAsync();

        try
        {
            return await DeserializeFrom<T>(filePath);
        }
        finally { locker.Release(); }
    }

    public static async Task<T?> DeserializeFrom<T>(string filePath)
    {
        string json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<T>(json, jsonOptions);
    }

    public static ValueTask<T?> DeserializeFromAsync<T>(Stream utf8Json)
        => JsonSerializer.DeserializeAsync<T>(utf8Json, jsonOptions);
}
