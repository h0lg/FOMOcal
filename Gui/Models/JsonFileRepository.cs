﻿using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FomoCal;

public class SetJsonFileRepository<T>(JsonFileStore store, string fileName) where T : class
{
    public async Task<HashSet<T>> LoadAllAsync() => await store.LoadAsync<HashSet<T>>(fileName) ?? [];
    public Task SaveCompleteAsync(ISet<T> items) => store.SaveAsync(fileName, items);
    internal void ShareFile(string label) => store.ShareFile(label, fileName);

    public async Task AddOrUpdateAsync(IEnumerable<T> items)
    {
        var set = await LoadAllAsync();
        set.UpdateWith(items);
        await SaveCompleteAsync(set);
    }
}

public class SingletonJsonFileRepository<T>(JsonFileStore store, string fileName) where T : class
{
    public async Task<T?> LoadAsync() => await store.LoadAsync<T>(fileName);
    public Task SaveAsync(T value) => store.SaveAsync(fileName, value);
}

public class JsonFileStore(string storagePath)
{
    private static readonly SemaphoreSlim locker = new(1, 1);

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private string GetFilePath(string fileName) => Path.Combine(storagePath, fileName + ".json");

    public async Task SaveAsync<T>(string fileName, T value)
    {
        string filePath = GetFilePath(fileName);
        var json = JsonSerializer.Serialize(value, JsonOptions);
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
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    internal void ShareFile(string fileLabel, string fileName)
        => Export.ShareFile(fileLabel, GetFilePath(fileName), MediaTypeNames.Application.Json);
}
