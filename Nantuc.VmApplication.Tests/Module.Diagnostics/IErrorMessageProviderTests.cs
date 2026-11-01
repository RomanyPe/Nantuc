using Nantuc.Kernel.Diagnostics;

namespace Nantuc.Tests.Module.Diagnostics;

public class IErrorMessageProviderTests
{
    [Fact]
    public void Default_IsSingleton()
    {
        Assert.Same(IErrorMessageProvider.Default, IErrorMessageProvider.Default);
    }

    [Fact]
    public void Default_IsDefaultErrorMessageProvider()
    {
        Assert.IsType<DefaultErrorMessageProvider>(IErrorMessageProvider.Default);
    }

    [Fact]
    public void Default_ProducesMessageForKnownCode()
    {
        Assert.Equal("Invalid assignment target",
            IErrorMessageProvider.Default.GetMessage(ErrorCode.Parser_InvalidAssignment));
    }
}


