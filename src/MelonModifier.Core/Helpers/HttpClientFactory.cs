using System.Net.Http;

namespace MelonModifier.Core.Helpers;

/// <summary>共享的 HttpClient 工厂：统一 UserAgent 与超时，供各下载服务复用。</summary>
public static class HttpClientFactory
{
    public static HttpClient Create()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MelonModifier/0.1");
        client.Timeout = TimeSpan.FromMinutes(10);
        return client;
    }
}
