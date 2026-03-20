using Maestro.Contracts.Slots;
using Maestro.Core.Model;

namespace Maestro.Core.Extensions;

public static class SlotExtensionMethods
{
    public static SlotDto ToDto(this Slot slot)
    {
        return new SlotDto(slot.Id, slot.StartTime, slot.EndTime, slot.RunwayIdentifiers);
    }
}
