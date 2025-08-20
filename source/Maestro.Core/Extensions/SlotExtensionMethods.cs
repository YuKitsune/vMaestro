using Maestro.Core.Messages;
using Maestro.Core.Model;

namespace Maestro.Core.Extensions;

public static class SlotExtensionMethods
{
    public static SlotMessage ToMessage(this Slot slot)
    {
        return new SlotMessage(slot.Id, slot.StartTime, slot.EndTime, slot.RunwayIdentifiers);
    }
}
