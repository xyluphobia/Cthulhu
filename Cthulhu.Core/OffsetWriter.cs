using Microsoft.Win32.SafeHandles;

public sealed class OffsetWriter : IDisposable {
  private readonly FileStream _fileStream;
  public SafeFileHandle Handle => _fileStream.SafeFileHandle;

  public OffsetWriter(string path, long length) {
    _fileStream = new FileStream(
      path, 
      FileMode.OpenOrCreate,
      FileAccess.ReadWrite,
      FileShare.Read,
      bufferSize: 1 << 20,
      useAsync: true
    );
    _fileStream.SetLength(length);
  }

  public ValueTask WriteAtAsync(ReadOnlyMemory<byte> buffer, long offset, CancellationToken cancellationToken)
    => RandomAccess.WriteAsync(_fileStream.SafeFileHandle, buffer, offset, cancellationToken);

  public void Dispose() => _fileStream?.Dispose();
}

