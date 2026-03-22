using Maestro.Contracts.Sessions;

namespace Maestro.Plugin;

public static class MetarWindParser
{
    /// <summary>
    /// Parses METAR string to extract wind into WindDto.
    /// </summary>
    public static WindDto? Parse(string metarString)
    {
        if (string.IsNullOrWhiteSpace(metarString))
            return null;

        try
        {
            // Extract wind component from METAR
            // Wind is typically the field ending with KT (knots) or MPS (meters per second)
            var parts = metarString.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            var windString = parts.FirstOrDefault(p =>
                p.EndsWith("KT", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith("MPS", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(windString))
                return null;

            windString = windString.Trim();

            // Wind formats:
            // 34016KT (340 degrees, 16 knots)
            // 340V25016KT (Varying between 340 and 250, 16 knots)
            // 340V25016G25KT (Varying with gusts)
            // VRB16KT (Variable direction, 16 knots)
            // VRB16G25KT (Variable with gusts)

            int direction;
            int speed;
            bool isMps = windString.EndsWith("MPS", StringComparison.OrdinalIgnoreCase);

            // Check for variable direction (VRB)
            if (windString.StartsWith("VRB", StringComparison.OrdinalIgnoreCase))
            {
                direction = 0;
                // Extract speed: everything after VRB until G or KT
                var speedPart = windString.Substring(3);
                var gustIndex = speedPart.IndexOf('G');
                if (gustIndex > 0)
                {
                    speedPart = speedPart.Substring(0, gustIndex);
                }
                else
                {
                    var ktIndex = speedPart.IndexOf("KT", StringComparison.OrdinalIgnoreCase);
                    if (ktIndex > 0)
                    {
                        speedPart = speedPart.Substring(0, ktIndex);
                    }
                    else
                    {
                        var mpsIndex = speedPart.IndexOf("MPS", StringComparison.OrdinalIgnoreCase);
                        if (mpsIndex > 0)
                        {
                            speedPart = speedPart.Substring(0, mpsIndex);
                        }
                    }
                }

                if (!int.TryParse(speedPart, out speed))
                    return null;
            }
            else
            {
                // Check for variable wind direction (e.g., 340V250)
                var vIndex = windString.IndexOf('V');
                if (vIndex >= 3 && vIndex < 7) // V should be after first direction (3 digits) and before second
                {
                    // Variable direction: calculate midpoint
                    var dir1Str = windString.Substring(0, 3);
                    var remainingAfterV = windString.Substring(vIndex + 1);
                    var dir2Str = remainingAfterV.Substring(0, 3);

                    if (!int.TryParse(dir1Str, out var dir1) || !int.TryParse(dir2Str, out var dir2))
                        return null;

                    // Validate directions are within valid range (0-360)
                    if (dir1 < 0 || dir1 > 360 || dir2 < 0 || dir2 > 360)
                        return null;

                    // Calculate midpoint handling wraparound (e.g., 350V010 should be 000, not 180)
                    var diff = Math.Abs(dir1 - dir2);
                    if (diff > 180)
                    {
                        // Wraparound case
                        direction = ((dir1 + dir2 + 360) / 2) % 360;
                    }
                    else
                    {
                        direction = (dir1 + dir2) / 2;
                    }

                    // Extract speed after second direction
                    var speedPart = remainingAfterV.Substring(3);
                    var gustIndex = speedPart.IndexOf('G');
                    if (gustIndex > 0)
                    {
                        speedPart = speedPart.Substring(0, gustIndex);
                    }
                    else
                    {
                        var ktIndex = speedPart.IndexOf("KT", StringComparison.OrdinalIgnoreCase);
                        if (ktIndex > 0)
                        {
                            speedPart = speedPart.Substring(0, ktIndex);
                        }
                        else
                        {
                            var mpsIndex = speedPart.IndexOf("MPS", StringComparison.OrdinalIgnoreCase);
                            if (mpsIndex > 0)
                            {
                                speedPart = speedPart.Substring(0, mpsIndex);
                            }
                        }
                    }

                    if (!int.TryParse(speedPart, out speed))
                        return null;
                }
                else
                {
                    // Standard format: 34016KT
                    if (windString.Length < 7)
                        return null;

                    var dirStr = windString.Substring(0, 3);
                    if (!int.TryParse(dirStr, out direction))
                        return null;

                    // Validate direction is within valid range (0-360)
                    if (direction < 0 || direction > 360)
                        return null;

                    // Extract speed: everything after direction until G or KT
                    var speedPart = windString.Substring(3);
                    var gustIndex = speedPart.IndexOf('G');
                    if (gustIndex > 0)
                    {
                        speedPart = speedPart.Substring(0, gustIndex);
                    }
                    else
                    {
                        var ktIndex = speedPart.IndexOf("KT", StringComparison.OrdinalIgnoreCase);
                        if (ktIndex > 0)
                        {
                            speedPart = speedPart.Substring(0, ktIndex);
                        }
                        else
                        {
                            var mpsIndex = speedPart.IndexOf("MPS", StringComparison.OrdinalIgnoreCase);
                            if (mpsIndex > 0)
                            {
                                speedPart = speedPart.Substring(0, mpsIndex);
                            }
                        }
                    }

                    if (!int.TryParse(speedPart, out speed))
                        return null;
                }
            }

            // Convert MPS to knots (1 m/s = 1.94384 knots)
            if (isMps)
            {
                speed = (int)Math.Round(speed * 1.94384);
            }

            return new WindDto(direction, speed);
        }
        catch
        {
            return null;
        }
    }
}
