using AspNetCoreRateLimit;
using Microsoft.Extensions.Options;

namespace Appointment_SaaS.API.Security;

/// <summary>
/// Rate-limit istemci IP'sini YALNIZCA gerçek bağlantı IP'sinden alır
/// (UseForwardedHeaders sonrası güvenilen loopback nginx zinciriyle çözülen
/// <c>Connection.RemoteIpAddress</c>). İstemcinin gönderdiği <c>X-Real-IP</c> /
/// <c>X-Forwarded-For</c> başlıklarına GÜVENMEZ; böylece başlık değiştirerek
/// (ör. OTP / verify / register) rate-limit bypass edilemez.
///
/// AspNetCoreRateLimit 5.0.0 varsayılan <see cref="RateLimitConfiguration"/> davranışı
/// önce <c>RealIpHeader</c> (varsayılan X-Real-IP) başlığını ekler ve istemci bu başlığı
/// gönderirse limit kovasının anahtarı olarak kullanır — bu güvenlik açığını kapatıyoruz.
/// </summary>
public class ConnectionIpRateLimitConfiguration : RateLimitConfiguration
{
    public ConnectionIpRateLimitConfiguration(
        IOptions<IpRateLimitOptions> ipOptions,
        IOptions<ClientRateLimitOptions> clientOptions)
        : base(ipOptions, clientOptions)
    {
    }

    public override void RegisterResolvers()
    {
        // Header tabanlı IP resolver EKLENMEZ — yalnızca gerçek bağlantı IP'si.
        IpResolvers.Add(new IpConnectionResolveContributor());

        if (!string.IsNullOrEmpty(ClientRateLimitOptions.ClientIdHeader))
        {
            ClientResolvers.Add(new ClientHeaderResolveContributor(ClientRateLimitOptions.ClientIdHeader));
        }
    }
}
