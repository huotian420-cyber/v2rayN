namespace ServiceLib.Services.CoreConfig;

public partial class CoreConfigV2rayService
{
    private void GenInbounds()
    {
        try
        {
            var listen = "0.0.0.0";
            var listenPort = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
            _coreConfig.inbounds = [];
            var inbound = BuildInbound(_config.Inbound.First(), EInboundProtocol.socks);
            var httpInbound = BuildInbound(_config.Inbound.First(), EInboundProtocol.http);

            if (!context.IsTunEnabled
                || (context.IsTunEnabled && _node.Address != Global.Loopback && _node.Port != listenPort))
            {
                _coreConfig.inbounds.Add(inbound);
                _coreConfig.inbounds.Add(httpInbound);

                if (_config.Inbound.First().SecondLocalPortEnabled)
                {
                    var inbound2 = BuildInbound(_config.Inbound.First(), EInboundProtocol.socks2);
                    var httpInbound2 = BuildInbound(_config.Inbound.First(), EInboundProtocol.http2);
                    _coreConfig.inbounds.Add(inbound2);
                    _coreConfig.inbounds.Add(httpInbound2);
                }

                if (_config.Inbound.First().AllowLANConn)
                {
                    if (_config.Inbound.First().NewPort4LAN)
                    {
                        var inbound3 = BuildInbound(_config.Inbound.First(), EInboundProtocol.socks3);
                        var httpInbound3 = BuildInbound(_config.Inbound.First(), EInboundProtocol.http3);
                        inbound3.listen = listen;
                        httpInbound3.listen = listen;
                        _coreConfig.inbounds.Add(inbound3);
                        _coreConfig.inbounds.Add(httpInbound3);

                        //auth
                        if (_config.Inbound.First().User.IsNotEmpty() && _config.Inbound.First().Pass.IsNotEmpty())
                        {
                            inbound3.settings.auth = "password";
                            inbound3.settings.accounts = new List<AccountsItem4Ray>
                            {
                                new() { user = _config.Inbound.First().User, pass = _config.Inbound.First().Pass }
                            };
                            httpInbound3.settings.accounts = inbound3.settings.accounts;
                        }
                    }
                    else
                    {
                        inbound.listen = listen;
                        httpInbound.listen = listen;
                    }
                }
            }

            if (context.IsTunEnabled)
            {
                if (_config.TunModeItem.Mtu <= 0)
                {
                    _config.TunModeItem.Mtu = Global.TunMtus.First();
                }
                var tunInbound = JsonUtils.Deserialize<Inbounds4Ray>(EmbedUtils.GetEmbedText(Global.V2raySampleTunInbound)) ?? new Inbounds4Ray { };
                tunInbound.settings.name = Utils.IsMacOS() ? $"utun{new Random().Next(99)}" : "xray_tun";
                tunInbound.settings.MTU = _config.TunModeItem.Mtu;
                if (_config.TunModeItem.EnableIPv6Address == false)
                {
                    tunInbound.settings.gateway = ["172.18.0.1/30"];
                }
                tunInbound.sniffing = inbound.sniffing;
                _coreConfig.inbounds.Add(tunInbound);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private Inbounds4Ray BuildInbound(InItem inItem, EInboundProtocol protocol)
    {
        var result = EmbedUtils.GetEmbedText(IsHttpInbound(protocol)
            ? Global.V2raySampleHttpInbound
            : Global.V2raySampleInbound);
        if (result.IsNullOrEmpty())
        {
            return new();
        }

        var inbound = JsonUtils.Deserialize<Inbounds4Ray>(result);
        if (inbound == null)
        {
            return new();
        }
        inbound.tag = protocol.ToString();
        inbound.port = inItem.LocalPort + (int)protocol;
        inbound.settings.udp = IsHttpInbound(protocol) ? null : inItem.UdpEnabled;
        inbound.sniffing.enabled = inItem.SniffingEnabled;
        inbound.sniffing.destOverride = inItem.DestOverride;
        inbound.sniffing.routeOnly = inItem.RouteOnly;

        return inbound;
    }

    private static bool IsHttpInbound(EInboundProtocol protocol)
    {
        return protocol is EInboundProtocol.http or EInboundProtocol.http2 or EInboundProtocol.http3;
    }
}
