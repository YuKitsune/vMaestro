namespace Maestro.Core.Configuration;

public class LabelsConfiguration
{
    public required GlobalColourConfiguration GlobalColours { get; init; }
    public required LabelLayoutConfiguration[] Layouts { get; init; }
}
