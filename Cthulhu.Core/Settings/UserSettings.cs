using System.Text.Json.Serialization;


public sealed class UserSettings {
  public string? DownloadDirectory { get; set; } = null;

  public int MaxConnections { get; set; } = 10;
  public long ChunkSizeBytes { get; set; } = 32L << 20; // 16 MiB
  public bool EnableHttp2 { get; set; } = true;

  [JsonIgnore] public const int CurrentVersion = 1;
  public int Version { get; set; } = CurrentVersion;
}
