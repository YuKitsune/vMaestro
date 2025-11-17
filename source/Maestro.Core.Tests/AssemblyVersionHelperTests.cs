using Shouldly;

namespace Maestro.Core.Tests;

public class AssemblyVersionHelperTests
{
    [Fact]
    public void GetVersion_ShouldReturnVersionString()
    {
        var assembly = typeof(AssemblyVersionHelper).Assembly;
        var version = AssemblyVersionHelper.GetVersion(assembly);

        version.ShouldNotBeNullOrEmpty();
        version.ShouldMatch(@"^\d+\.\d+\.\d+$");
    }
}
