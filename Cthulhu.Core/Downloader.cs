namespace Cthulhu.Core;

public class Downloader
{
  HttpClient httpClient = new HttpClient();
  string TEMP_downloadRoute = @"C:Users\Matthew\Downloads\";
  
  public async Task Download(Uri uri) {
    try {
      Console.WriteLine("Validating Hosted File..");
      HttpResponseMessage response = await httpClient.GetAsync(
        uri,
        HttpCompletionOption.ResponseHeadersRead
      );
      response.EnsureSuccessStatusCode();
      string fileName = Path.GetFileName(uri.LocalPath);
      
      Console.WriteLine("File Located Online");
      Stream stream = await response.Content.ReadAsStreamAsync();
      FileStream fileStream = new FileStream(
        path: TEMP_downloadRoute + fileName, 
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
  }
}
