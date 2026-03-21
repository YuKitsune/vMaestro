namespace Maestro.Core.Sessions;

public class Wind
{
    public Wind(int direction, int speed)
    {
        if (Direction is < 0 or > 360)
            throw new ArgumentOutOfRangeException(nameof(direction), "Wind direction must be between 0 and 360 degrees.");

        if (Speed is < 0 or > 200)
            throw new ArgumentOutOfRangeException(nameof(speed), "Wind speed must be between 0 and 200 knots.");

        Direction = direction;
        Speed = speed;
    }

    public int Direction { get; }
    public int Speed { get; }
}
