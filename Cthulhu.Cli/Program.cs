using Cthulhu.Core;

CliSettingsHandler.HandleSettings();
UserSettings settings = UserSettingsStore.Load();

if (args.Length == 0) { // Entry
  Console.WriteLine("Usage: Cthulhu get <URL>");
  return;
} else if (args[0].ToLower() != "cthulhu") {
  return;
} else { // Always active args
  for (int i = 2; i < args.Length; i++) {
    switch (args[i]) {
      case "--download-dir": // Set default download directory
        if (i + 1 > args.Length) return; 
        CliSettingsHandler.SetNewDownloadDirectory(args[i +1]);
        settings = UserSettingsStore.Load();
        break;
      case "--max-connections": // Set default max connection count
        if (i + 1 > args.Length) return; 
        CliSettingsHandler.SetNewConnectionsCount(args[i + 1]);   
        settings = UserSettingsStore.Load();
        break;
      case "--chunk-size": // Set default chunk size
        if (i + 1 > args.Length) return;
        CliSettingsHandler.SetNewChunkSize(args[i + 1]);
        settings = UserSettingsStore.Load();
        break;
    }
  }
}

if (args[1].ToLower() == "get") { // cthulhu get, initiate get sequence
  string urlString = args[2];
  if (!Uri.IsWellFormedUriString(urlString, UriKind.Absolute)) {
    Console.WriteLine("Please enter a valid URL.");
    return;
  }
  Uri url = new Uri(urlString);

  string? outputPath = settings.DownloadDirectory;
  bool? explicitPath = null;
  int connections = settings.MaxConnections;
  long chunkSize = settings.ChunkSizeBytes;
  for (int i = 2; i < args.Length; i++) {
    switch (args[i]) {
      // Expects -o C:\Users\Downloads\
      case "-output":
      case "-o": // Download directory override
        if (i + 1 > args.Length) return; // if theres no statement following
        outputPath = args[i + 1];
        if (outputPath[outputPath.Length - 1] != '\\') outputPath += '\\';
        break;
      case "-explicit":
      case "-e": // Explicit download path including file type e.g. -e Users/Download/image.png
        if (i + 1 > args.Length) return; 
        outputPath = args[i + 1]; 
        explicitPath = true;
        break;
      case "-connections":
      case "-c":
        if (i + 1 > args.Length) return; 
        if (int.TryParse(args[i + 1], out int newConnections)) {
          connections = newConnections;
          break;
        }
        Console.WriteLine($"Set Connection Count Error: '{args[i + 1]}' is not a valid integer input.");
        break;
    }
  }

  HttpClient http = HttpClientFactory.Create(connections);
  Downloader downloader = new Downloader(http);

  // Renders progress bar for download
  System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
  DateTime lastUpdate = DateTime.UtcNow;
  var progress = new Progress<(long downloaded, long total)>(p => {
    if ((DateTime.UtcNow - lastUpdate).TotalMilliseconds < 100) return;
    lastUpdate = DateTime.UtcNow;

    var (downloadedBytes, totalBytes) = p;
    var percentage = totalBytes > 0 ? (double)downloadedBytes / totalBytes * 100 : 0;

    double elapsedSeconds = Math.Max(1, stopwatch.Elapsed.TotalSeconds);
    double speed = downloadedBytes / elapsedSeconds;
    double remainingBytes = totalBytes > 0 ? Math.Max(0, totalBytes - downloadedBytes) : 0;
    double etaSeconds = totalBytes > 0 && speed > 0 ? remainingBytes / speed : double.NaN;
    string eta = double.IsNaN(etaSeconds) ? "--:--" : TimeSpan.FromSeconds(etaSeconds).ToString(@"mm\:ss");

    Console.Write(
        $"\r{percentage,6:F2}%  |  {FormatBytes(downloadedBytes)}/{FormatBytes(totalBytes)} " +
        $"@ {FormatBytes((long)speed)}/s  |  ETA: {eta}  |  Elapsed: {stopwatch.Elapsed:mm\\:ss}       ");
  });

  await downloader.DownloadAsync(
    url,
    outputPath!,
    connections, 
    chunkSize, 
    progress,

    explicitPath: explicitPath
  );
  Console.WriteLine("\nDone.");
}




// Formatter for progressbar/eta
static string FormatBytes(long bytes)
{
    string[] units = { "B", "KB", "MB", "GB", "TB" };
    double value = bytes;
    int u = 0;
    while (value >= 1024 && u < units.Length - 1) { value /= 1024; u++; }
    return $"{value:F2} {units[u]}";
}

