namespace ServiceLib.Handler.SysProxy;

public static class SysProxyHandler
{
    private static readonly string _tag = "SysProxyHandler";

    public static async Task<bool> UpdateSysProxy(Config config, bool forceDisable)
    {
        var type = config.SystemProxyItem.SysProxyType;

        if (forceDisable && type != ESysProxyType.Unchanged)
        {
            type = ESysProxyType.ForcedClear;
        }

        try
        {
            var port = AppManager.Instance.GetSystemProxyPort();
            var exceptions = config.SystemProxyItem.SystemProxyExceptions.Replace(" ", "");
            if (port <= 0)
            {
                return false;
            }
            switch (type)
            {
                case ESysProxyType.ForcedChange when Utils.IsWindows():
                    {
                        GetWindowsProxyString(config, out var strProxy, out var strExceptions);
                        ProxySettingWindows.SetProxy(strProxy, strExceptions, 2);
                        break;
                    }
                case ESysProxyType.ForcedChange when Utils.IsLinux():
                    await ProxySettingLinux.SetProxy(Global.Loopback, port, exceptions);
                    break;

                case ESysProxyType.ForcedChange when Utils.IsMacOS():
                    await ProxySettingOSX.SetProxy(Global.Loopback, port, exceptions);
                    break;

                case ESysProxyType.ForcedClear when Utils.IsWindows():
                    ProxySettingWindows.UnsetProxy();
                    break;

                case ESysProxyType.ForcedClear when Utils.IsLinux():
                    await ProxySettingLinux.UnsetProxy();
                    break;

                case ESysProxyType.ForcedClear when Utils.IsMacOS():
                    await ProxySettingOSX.UnsetProxy();
                    break;

                case ESysProxyType.Pac when Utils.IsWindows():
                    await SetWindowsProxyPac();
                    break;
            }

            if (type != ESysProxyType.Pac && Utils.IsWindows())
            {
                PacManager.Instance.Stop();
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return true;
    }

    private static void GetWindowsProxyString(Config config, out string strProxy, out string strExceptions)
    {
        strExceptions = config.SystemProxyItem.SystemProxyExceptions.Replace(" ", "");
        if (config.SystemProxyItem.NotProxyLocalAddress)
        {
            strExceptions = $"<local>;{strExceptions}";
        }

        var httpPort = AppManager.Instance.GetSystemProxyPort();
        var socksPort = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        strProxy = string.Empty;
        if (config.SystemProxyItem.SystemProxyAdvancedProtocol.IsNullOrEmpty())
        {
            strProxy = $"http={Global.Loopback}:{httpPort};https={Global.Loopback}:{httpPort};socks={Global.Loopback}:{socksPort}";
        }
        else
        {
            strProxy = config.SystemProxyItem.SystemProxyAdvancedProtocol
                .Replace("{ip}", Global.Loopback)
                .Replace("{http_port}", httpPort.ToString())
                .Replace("{socks_port}", socksPort.ToString());
        }
    }

    private static async Task SetWindowsProxyPac()
    {
        var portPac = AppManager.Instance.GetLocalPort(EInboundProtocol.pac);
        await PacManager.Instance.StartAsync(AppManager.Instance.GetSystemProxyPort(), portPac);
        var strProxy = $"{Global.HttpProtocol}{Global.Loopback}:{portPac}/pac?t={DateTime.Now.Ticks}";
        ProxySettingWindows.SetProxy(strProxy, "", 4);
    }
}
