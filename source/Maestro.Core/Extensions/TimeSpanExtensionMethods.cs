using System.Text;

namespace Maestro.Core.Extensions;

public static class TimeSpanExtensionMethods
{
    public static string ToHoursAndMinutesString(this TimeSpan timeSpan)
    {
        var sb = new StringBuilder();

        if (timeSpan.TotalSeconds > 0)
        {
            sb.Append("-");
        }
        
        sb.Append(timeSpan.TotalMinutes.ToString("00"));
        sb.Append(":");
        sb.Append(timeSpan.Seconds.ToString("00"));

        return sb.ToString();
    }
}