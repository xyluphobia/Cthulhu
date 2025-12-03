using Cthulhu.Core;

string? urlInput = null;
do {
  string message = urlInput is null ? "Download Url: " : "Please enter a valid URL: ";
  Console.WriteLine(message);
  urlInput = Console.ReadLine();
} while (!Uri.IsWellFormedUriString(urlInput, UriKind.Absolute));

Downloader downloader = new Downloader();
await downloader.Download(new Uri(urlInput)); 
