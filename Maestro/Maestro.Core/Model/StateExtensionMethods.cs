using Maestro.Core.Dtos;

namespace Maestro.Core.Model;

public static class StateExtensionMethods
{
    public static StateDTO ToDTO(this State state)
    {
        return state switch
        {
            State.Unstable => StateDTO.Unstable,
            State.Stable => StateDTO.Stable,
            State.SuperStable => StateDTO.SuperStable,
            State.Frozen => StateDTO.Frozen,
            State.Landed => StateDTO.Landed,
            _ => throw new ArgumentOutOfRangeException($"Unexpected state: {state}")
        };
    }
}
