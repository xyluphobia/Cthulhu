
public sealed class SessionMeta {
  public required Uri Url { get; init; }
  public required string OutputPath { get; init; }
  public string? ETag { get; init; }
  public DateTimeOffset? LastModified { get; init; }
  public required long ContentLength { get; init; }
  public required List<Chunk> Chunks { get; init; } = new();
}

public sealed class Chunk {
  public long Start { get; init; }
  public long End { get; init; }
  public long BytesWritten { get; set; } // for resuming/progress
  public bool Done => BytesWritten >= (End - Start + 1);
}
