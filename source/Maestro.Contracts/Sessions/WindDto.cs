using MessagePack;

namespace Maestro.Contracts.Sessions;

[MessagePackObject]
public record WindDto(
    [property: Key(0)] int Direction,
    [property: Key(1)] int Speed);
