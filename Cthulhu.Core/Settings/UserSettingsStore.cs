using System.Text.Json;

public static class UserSettingsStore {
  private const string AppFolderName = "Cthulhu";
  private const string FileName = "settings.json";

  public static string GetSettingsPath() {
    string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    string appDir = Path.Combine(baseDir, AppFolderName);
    Directory.CreateDirectory(appDir); // As per microsoft docs, if the directory exists it simply returns a DirectoryInfo object
    return Path.Combine(appDir, FileName);
  }

  public static UserSettings Load() {
    string path = GetSettingsPath();
    if (!File.Exists(path)) {
      return new UserSettings(); // Defaults
    }

    try {
      string json = File.ReadAllText(path);
      UserSettings settings = JsonSerializer.Deserialize<UserSettings>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? new UserSettings();

      if (settings.Version != UserSettings.CurrentVersion) {
        settings.Version = UserSettings.CurrentVersion;
        Save(settings);
      }

      return settings;
    } 
    catch {
      string backup = path + ".bak-" + DateTime.UtcNow.ToString("yyyyMMddGGmmss");
      try { File.Copy(path, backup, overwrite: false); } catch { }
      return new UserSettings();
    }
  }

  public static void Save(UserSettings settings) {
    string path = GetSettingsPath();
    string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions {
      WriteIndented = true
    });
    File.WriteAllText(path, json);
  }
}
