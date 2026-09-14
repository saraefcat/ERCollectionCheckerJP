using ERCollectionCheckerJP.Application;

namespace ERCollectionCheckerJP.Application.Tests;

public sealed class ApplicationMarkerTests
{
    [Fact]
    public void ApplicationAssembly_IsLoadable()
    {
        Assert.NotNull(typeof(ApplicationMarker).Assembly);
    }
}
