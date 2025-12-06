namespace Cthulhu.Core;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

public sealed class Downloader
{
  private readonly HttpClient _http;
  public Downloader(HttpClient http) => _http = http;

  UserSettings settings = UserSettingsStore.Load();

  public async Task DownloadAsync(
    Uri url,
    string outputPath,
    int connections,
    long chunkSize,
    IProgress<(long downloaded, long total)>? progress = null,
    CancellationToken cancellationToken = default,

    bool? explicitPath = false
  ){
    // Format output path to include file name and type
    if (!explicitPath.HasValue || explicitPath == false) { // Explicit is null or false
      outputPath = outputPath + Path.GetFileName(url.LocalPath);
    }

    // If filepath already exists, append version number
    int fileVer = 1;
    while (File.Exists(outputPath)) {

      int indexOfFinalDot = outputPath.LastIndexOf('.');
      if (fileVer > 1) {
        indexOfFinalDot = outputPath.LastIndexOf($" ({fileVer - 1}).");
        outputPath = outputPath.Remove(
            indexOfFinalDot,
            4 + ((fileVer - 1).ToString().Length) // Adds length based on how much space ver takes up e.g. 10 = 2, 100 = 3
        ).Insert(indexOfFinalDot, $" ({fileVer}).");
      }
      else {
        outputPath = outputPath.Remove(indexOfFinalDot, 1).Insert(indexOfFinalDot, $" ({fileVer}).");
      }

      fileVer++;
    }

    // Probe
    var (supportsRanges, length, etag, lastModified) = await ProbeAsync(url, cancellationToken);
    if (!supportsRanges || length is null) { // Single thread download
      Console.WriteLine("Downloading with single stream.");
      await SingleStreamAsync(url, outputPath, cancellationToken, progress);
      return;
    }
    Console.WriteLine($"Downloading with {connections} tentacles.");

    long total = length.Value;

    // Load or init session meta
    string metaPath = outputPath + ".cthulhu.json";
    SessionMeta meta = await LoadOrCreateMetaAsync(metaPath, url, outputPath, total, etag, lastModified, chunkSize, cancellationToken);

    // Writer
    using OffsetWriter writer = new OffsetWriter(outputPath, total);

    // Scheduler (queue all incomplete chunks)
    List<Chunk> pending = meta.Chunks.Where(chunk => !chunk.Done).ToList();
    SemaphoreSlim throttler = new SemaphoreSlim(connections);
    long globalDownloaded = meta.Chunks.Sum(chunk => chunk.BytesWritten);

    var tasks = pending.Select(async ch => {
      await throttler.WaitAsync(cancellationToken);
      try {
        await DownloadChunkAsync(url, ch, writer, cancellationToken, bytes => {
          Interlocked.Add(ref globalDownloaded, bytes);
          progress?.Report((globalDownloaded, total));
        });
        await SaveMetaAsync(metaPath, meta, cancellationToken);
      }
      finally {
        throttler.Release();
      }
    });

    await Task.WhenAll(tasks); // Wait for all tasks of scheduler (async downloads)
    progress?.Report((total, total)); // Finish download progress
    File.Delete(metaPath); // Delete the download's metadata
  }

  // Probe the file before beginning download
  private async 
    Task<(bool supportsRanges, long? length, string? etag, DateTimeOffset? lastModified)>
    ProbeAsync(Uri url, CancellationToken cancellationToken) {
      
    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url);
    using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    response.EnsureSuccessStatusCode();

    // If the headers contain the Accept-Ranges header & the value is bytes (on)
    bool supportsRanges = response.Headers.TryGetValues("Accept-Ranges", out var vals) &&
      vals.Any(val => val.Contains("bytes", StringComparison.OrdinalIgnoreCase));

    long? length = response.Content.Headers.ContentLength;
    string? etag = response.Headers.ETag?.Tag;
    DateTimeOffset? lastModified = response.Content.Headers.LastModified;

    return (supportsRanges, length, etag, lastModified);
  }
  
  private static async Task<SessionMeta> LoadOrCreateMetaAsync(
    string metaPath, Uri url, string outputPath, long total,
    string? etag, DateTimeOffset? lastModified, long chunkSize, CancellationToken cancellationToken
  ){
    if (File.Exists(metaPath)) {
      using FileStream fileStream = File.OpenRead(metaPath);
      SessionMeta existing = await JsonSerializer.DeserializeAsync<SessionMeta>(fileStream, cancellationToken: cancellationToken)
        ?? throw new InvalidOperationException("Invalid metadata.");
      return existing;
    }

    List<Chunk> chunks = new List<Chunk>();
    for (long s = 0; s < total; s += chunkSize) {
      long e = Math.Min(s + chunkSize - 1, total - 1);
      chunks.Add(new Chunk { Start = s, End = e, BytesWritten = 0 });
    }

    SessionMeta meta = new SessionMeta {
      Url = url,
      OutputPath = outputPath,
      ETag = etag,
      LastModified = lastModified,
      ContentLength = total,
      Chunks = chunks
    };

    await SaveMetaAsync(metaPath, meta, cancellationToken);
    return meta;
  }

  private static readonly SemaphoreSlim MetaWriteLock = new(1, 1);
  private static async Task SaveMetaAsync(string metaPath, SessionMeta meta, CancellationToken cancellationToken) {
    await MetaWriteLock.WaitAsync(cancellationToken);
    try {
      Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);

      string tempMetaPath = metaPath + ".tmp";
      string json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
      await File.WriteAllTextAsync(tempMetaPath, json, cancellationToken);

      File.Move(tempMetaPath, metaPath, overwrite: true);
    }
    finally {
      MetaWriteLock.Release();
    }
  }

  private async Task DownloadChunkAsync(Uri url, Chunk chunk, OffsetWriter writer, CancellationToken cancellationToken, Action<long> onBytes) {
    // Range is inclusive, if resuming start at Start + BytesWritten
    long start = chunk.Start + chunk.BytesWritten;
    if (start > chunk.End) return;

    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Range = new RangeHeaderValue(start, chunk.End);
    using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    if (response.StatusCode is not HttpStatusCode.PartialContent and not HttpStatusCode.OK) {
      response.EnsureSuccessStatusCode();
    }

    await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

    const int BUF = 1 << 16;
    byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(BUF);
    try {
      long offset = start;
      int read;
      while((read = await stream.ReadAsync(buffer.AsMemory(0, BUF), cancellationToken)) > 0) {
        await writer.WriteAtAsync(buffer.AsMemory(0, read), offset, cancellationToken);
        offset += read;
        chunk.BytesWritten += read;
        onBytes(read);
      }
    }
    finally {
      System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
    }
  }

  private async Task SingleStreamAsync(Uri url, string outputPath, CancellationToken cancellationToken, IProgress<(long, long)>? progress) {
    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
    using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    response.EnsureSuccessStatusCode();
    
    long total = response.Content.Headers.ContentLength ?? -1;
    await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
    await using FileStream output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 20, useAsync: true);

    const int BUF = 1 << 16;
    byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(BUF);
    long written = 0;
    try {
      int read;
      while((read = await input.ReadAsync(buffer.AsMemory(0, BUF), cancellationToken)) > 0) {
        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        written += read;
        if (total > 0) progress?.Report((written, total));
      }
    }
    finally {
      System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
    }
  }
}

  /*
  HttpClient httpClient = new HttpClient();
  
  public async Task Download(
    Uri uri,
    string outputPath,
    int connections = 6,
    long chunkSize = 8L << 20, // 8MB
    IProgress<(long downloaded, long total)>? progress = null,
    CancellationToken cancellationToken = default,

    bool overridePath = false,
    bool explicitPath = false
  ){
    Console.WriteLine("Validating Hosted File..");
    using (HttpResponseMessage response = await httpClient.GetAsync(
      uri,
      HttpCompletionOption.ResponseHeadersRead
    )) {
      try {
        response.EnsureSuccessStatusCode();
        
        Console.WriteLine("File Located Online");
        Stream stream = await response.Content.ReadAsStreamAsync();
        FileStream fileStream = new FileStream(
          path: outputPath + fileName, 
          mode: FileMode.Create, 
          access: FileAccess.Write
        );
        
        Console.WriteLine("Copying File to Device");
        await stream.CopyToAsync(fileStream);
        await fileStream.FlushAsync();
        Console.WriteLine("Download Complete");
      }
      catch (Exception e) {
        throw new Exception("An error occured downloading your file: " + e);
      }
    } // response.Dispose() is automatically called this way.
  }

  */

