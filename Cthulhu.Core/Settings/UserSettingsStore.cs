

public static class UserSettingsStore {
  private const string AppFolderName = "Cthulhu";
  private const string FileName = "settings.json";

  public static string GetSettingsPath() {
    var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    var appDir = Path.Combine(baseDir, AppFolderName);
    Directory.CreateDirectory(appDir);
    return Path.Combine(appDir, FileName);
  }

  public static UserSettings Load() {
    var path = GetSettingsPath();
    if (!File.Exists(path)) {
      return new UserSettings(); // Defaults
    }

    try {
      var json = File.ReadAllText(path);
      var settings = JsonSerializer.DeSerialize<UserSettings>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? new UserSettings();
    } 
    catch {

    }
  }
}
