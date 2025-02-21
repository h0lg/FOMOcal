﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace FomoCal;

public class JsonFileRepository<T>(JsonFileStore store, string fileName) where T : class
{
    public async Task<ISet<T>> LoadAllAsync() => await store.LoadAsync<HashSet<T>>(fileName) ?? [];
    public Task SaveCompleteAsync(ISet<T> items) => store.SaveAsync(fileName, items);

    public async Task AddOrUpdateAsync(IEnumerable<T> items)
    {
        var set = await LoadAllAsync();

        foreach (var item in items)
        {
            set.Remove(item);
            set.Add(item);
        }

        await SaveCompleteAsync(set);
    }
}

public class JsonFileStore(string storagePath)
{
    private static readonly SemaphoreSlim locker = new(1, 1);

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = true,
        Converters = { new IgnoreEmptyStringConverter() }
    };

    private string GetFilePath(string fileName) => Path.Combine(storagePath, fileName + ".json");

    public async Task SaveAsync<T>(string fileName, T value)
    {
        string filePath = GetFilePath(fileName);
        var json = JsonSerializer.Serialize(value, jsonOptions);
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
            string json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json, jsonOptions);
        }
        finally { locker.Release(); }
    }
}

public class IgnoreEmptyStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.String ? reader.GetString() : null;

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value.HasSignificantValue())
            writer.WriteStringValue(value);
    }
}
