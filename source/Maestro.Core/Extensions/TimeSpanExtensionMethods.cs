using System.Text;

namespace Maestro.Core.Extensions;

public static class TimeSpanExtensionMethods
{
    public static string ToHoursAndMinutesString(this TimeSpan timeSpan)
    {
        var sb = new StringBuilder();

        if (timeSpan < TimeSpan.Zero)
        {
            sb.Append("-");
        }

        var absolute = timeSpan.Duration();
        
        sb.Append(absolute.TotalMinutes.ToString("0"));
        sb.Append("m ");
        sb.Append(absolute.Seconds.ToString("00"));
        sb.Append("s");

        return sb.ToString();
    }
}