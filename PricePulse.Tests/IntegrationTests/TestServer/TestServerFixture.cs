using System;
using System.Threading.Tasks;

namespace PricePulse.Tests.IntegrationTests.TestServer
{
    public class TestServerFixture : IAsyncLifetime
    {
        private TestServer? _testServer;
        private int _port;

        public TestServerFixture()
        {
            // Find an available port
            _port = FindAvailablePort();
        }

        public string BaseAddress { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            _testServer = new TestServer($"http://localhost:{_port}/");
            BaseAddress = _testServer.Url.TrimEnd('/');
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            if (_testServer != null)
            {
                await _testServer.DisposeAsync();
            }
        }

        private static int FindAvailablePort()
        {
            using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}