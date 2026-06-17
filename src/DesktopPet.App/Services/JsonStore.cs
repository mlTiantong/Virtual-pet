using System.IO;
using System.Text.Json;

namespace DesktopPet.App.Services;

public sealed class JsonStore<T> where T : new()
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public JsonStore(string fileName)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopPetM1");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, fileName);
    }

    public T Load()
    {
        if (!File.Exists(_path))
        {
            var created = new T();
            Save(created);
            return created;
        }

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<T>(json, _options) ?? new T();
        }
        catch
        {
            return new T();
        }
    }

    public void Save(T value)
    {
        var json = JsonSerializer.Serialize(value, _options);
        File.WriteAllText(_path, json);
    }

    public string PathOnDisk => _path;
}
