using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FomoCal;

public class SetJsonFileRepository<T>(JsonFileStore store, string fileName) where T : class
{
    public async Task<HashSet<T>> LoadAllAsync() => await store.LoadAsync<HashSet<T>>(fileName) ?? [];
    public Task SaveCompleteAsync(ISet<T> items) => store.SaveAsync(fileName, items);
    internal void ShareFile(string label) => store.ShareFile(label, fileName);
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
        Converters = {
            // migrates the old NavigateLinkToLoadMore to the new NavigateLinkToLoadDifferent
            new EnumAliasConverter<Venue.PagingStrategy>(new Dictionary<string, Venue.PagingStrategy>
            {
                ["NavigateLinkToLoadMore"] = Venue.PagingStrategy.NavigateLinkToLoadDifferent
            }),

            new JsonStringEnumConverter()
        }
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

    internal static async Task<T?> DeserializeFrom<T>(string filePath)
    {
        string json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<T>(json, jsonOptions);
    }

    internal void ShareFile(string fileLabel, string fileName)
        => Export.ShareFile(fileLabel, GetFilePath(fileName), MediaTypeNames.Application.Json);
}

/// <summary>Maps a string alias to an <see cref="Enum"/> value while reading.
/// Useful to migrate old string Enum values written with the <see cref="JsonStringEnumConverter"/>.</summary>
/// <typeparam name="T">An <see cref="Enum"/> type.</typeparam>
internal sealed class EnumAliasConverter<T> : JsonConverter<T> where T : struct, Enum
{
    private readonly Dictionary<string, T> aliases;

    internal EnumAliasConverter(IEnumerable<KeyValuePair<string, T>> aliases)
        => this.aliases = new Dictionary<string, T>(aliases, StringComparer.OrdinalIgnoreCase);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString()!;
            if (aliases.TryGetValue(str, out var mapped)) return mapped;
            if (Enum.TryParse<T>(str, ignoreCase: true, out var parsed)) return parsed;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var i))
            return (T)(object)i;

        throw new JsonException($"Cannot convert token '{reader.GetString()}' to enum {typeof(T)}.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
