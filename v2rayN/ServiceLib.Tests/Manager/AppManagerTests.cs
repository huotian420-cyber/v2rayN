using AwesomeAssertions;
using ServiceLib.Enums;
using ServiceLib.Manager;
using Xunit;

namespace ServiceLib.Tests.CoreConfig;

public class AppManagerTests
{
    [Fact]
    public void SetSystemProxyPort_ShouldUseHttpInboundForXray()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.Xray);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        AppManager.Instance.SetSystemProxyPort(ECoreType.Xray);

        AppManager.Instance.GetSystemProxyPort()
            .Should().Be(config.Inbound.First().LocalPort + (int)EInboundProtocol.http);
    }

    [Fact]
    public void SetSystemProxyPort_ShouldUseMixedInboundForSingbox()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        AppManager.Instance.SetSystemProxyPort(ECoreType.sing_box);

        AppManager.Instance.GetSystemProxyPort()
            .Should().Be(config.Inbound.First().LocalPort + (int)EInboundProtocol.socks);
    }
}
