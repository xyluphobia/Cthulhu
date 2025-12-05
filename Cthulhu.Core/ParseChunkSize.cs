using System.Globalization;

public class ParseChunkSize {

  public static long ParseSize(string sizeString) { // Expects "16MB", "4MiB"
    sizeString = sizeString.Trim().ToUpperInvariant();
    if (long.TryParse(
          sizeString, 
          NumberStyles.Integer, 
          CultureInfo.InvariantCulture,
          out var bytes
        )) return bytes;
    long factor = sizeString.EndsWith("MIB") ? 1 << 20 :
                  sizeString.EndsWith("MB")  ? 1_000_000 :
                  sizeString.EndsWith("KIB") ? 1 << 10 :
                  sizeString.EndsWith("KB")  ? 1_000 :
                  sizeString.EndsWith("GB")  ? 1_000_000_000 :
                  sizeString.EndsWith("GIB") ? 1L << 30 : 1;

    var number = new string(sizeString.TakeWhile(char.IsDigit).ToArray());
    return (long)(double.Parse(number, CultureInfo.InvariantCulture) * factor);
  }
}
