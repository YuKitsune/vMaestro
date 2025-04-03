using Maestro.Core.Dtos;

namespace Maestro.Core.Model;

public static class StateExtensionMethods
{
    public static StateDto ToDto(this State state)
    {
        return state switch
        {
            State.Unstable => StateDto.Unstable,
            State.Stable => StateDto.Stable,
            State.SuperStable => StateDto.SuperStable,
            State.Frozen => StateDto.Frozen,
            State.Landed => StateDto.Landed,
            _ => throw new ArgumentOutOfRangeException($"Unexpected state: {state}")
        };
    }
}
