using System.Text;

namespace Zhengyan.MikuMikuDance.Formats.Binary;

internal static class EncodingProvider
{
    public static Encoding ShiftJis { get; } = CreateShiftJisEncoding();

    private static Encoding CreateShiftJisEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932);
    }
}
