
public static class HttpClientFactory {

  public static HttpClient Create(int maxConnectionsPerServer = 10) {
    var handler = new SocketsHttpHandler {
      AllowAutoRedirect = true,
      AutomaticDecompression = System.Net.DecompressionMethods.None,
      ConnectTimeout = TimeSpan.FromSeconds(15),
      EnableMultipleHttp2Connections = true, 
      MaxConnectionsPerServer = maxConnectionsPerServer,
      PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
    };

    return new HttpClient(handler) {
      Timeout = Timeout.InfiniteTimeSpan
    };
  }
}
