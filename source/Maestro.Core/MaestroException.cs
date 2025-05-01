namespace Maestro.Core;

public class MaestroException : Exception
{
    public MaestroException(string message) : base(message) { }
    public MaestroException(string message, Exception innerException) : base(message, innerException) { }
}