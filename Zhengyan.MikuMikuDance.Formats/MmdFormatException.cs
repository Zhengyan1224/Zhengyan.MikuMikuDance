namespace Zhengyan.MikuMikuDance.Formats;

public sealed class MmdFormatException : Exception
{
    public MmdFormatException(string message)
        : base(message)
    {
    }

    public MmdFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
