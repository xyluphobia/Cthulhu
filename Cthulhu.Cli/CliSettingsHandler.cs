public class CliSettingsHandler {

  public static void HandleSettings() {
    UserSettings settings = UserSettingsStore.Load();

    if (string.IsNullOrEmpty(settings.DownloadDirectory)) {
      string? downloadPath = null;
      do {
        string message = downloadPath is null ? 
          "Please enter download directory path: " :
          "Path not valid, please enter a valid path: ";
        Console.Write(message);
        Console.WriteLine(downloadPath);
        downloadPath = Console.ReadLine();
      } while (!Directory.Exists(downloadPath));

      settings.DownloadDirectory = downloadPath + downloadPath[downloadPath.Length - 1] == '\\' ? "" : "\\";
      UserSettingsStore.Save(settings);
      Console.WriteLine("Download Path Set.");
    }
  }

  public static void SetNewDownloadDirectory(string directory) {
    UserSettings settings = UserSettingsStore.Load();
    if (Directory.Exists(directory)) {
      settings.DownloadDirectory = directory + "\\";
      UserSettingsStore.Save(settings);
      Console.WriteLine("New Download Path Set.");
    }
    else {
      Console.WriteLine("'" + directory + "' is not a valid directory.");
    }
  }

  public static void SetNewConnectionsCount(string count) {
    UserSettings settings = UserSettingsStore.Load();
    if (int.TryParse(count, out int countInt)) {
      settings.MaxConnections = countInt;
      UserSettingsStore.Save(settings);
      Console.WriteLine("Maximum connections set to: " + countInt);
    }
    else {
      Console.WriteLine(count + " is not a valid integer.");
    }
  }

  public static void SetNewChunkSize(string size) {
    UserSettings settings = UserSettingsStore.Load();
    try {
      long newSizeBytes = ParseChunkSize.ParseSize(size);
      settings.ChunkSizeBytes = newSizeBytes;
      UserSettingsStore.Save(settings);
      Console.WriteLine($"New Chunk Size is {newSizeBytes}bytes.");
    }
    catch (Exception exception) {
      Console.WriteLine($"Failed to Chunk Size: {exception}");
    }
  }
}
