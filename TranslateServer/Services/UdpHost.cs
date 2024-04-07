using Microsoft.Extensions.Hosting;
using System.Net.Sockets;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TranslateServer.Services
{
    public class UdpHost : IHostedService
    {
        private bool _isRun = false;
        private readonly UdpClient _listener;
        private readonly IServiceProvider _serviceProvider;
        private ExternalApprover _externalApprover;

        public UdpHost(IServiceProvider serviceProvider, IConfiguration config)
        {
            if (config["Approve"] == null) return;

            _listener = new UdpClient(5555);
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_serviceProvider != null)
            {
                _isRun = true;
                _ = Task.Run(ReceiveLoop, cancellationToken);
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _isRun = false;
            _listener?.Close();
            return Task.CompletedTask;
        }

        private async Task ReceiveLoop()
        {
            while (_isRun)
                try
                {
                    while (true)
                    {
                        var result = await _listener.ReceiveAsync();
                        _ = Task.Run(async () => await ProcessData(result));
                    }
                }
                catch (SocketException e)
                {
                    Console.WriteLine(e);
                }
        }

        private async Task ProcessData(UdpReceiveResult result)
        {
            try
            {
                await OnMessage(result.Buffer);
            }
            catch
            {
            }
        }

        private async Task OnMessage(byte[] buffer)
        {
            _externalApprover ??= _serviceProvider.GetService<ExternalApprover>();

            if (buffer[0] == 0)
            {
                var res = BitConverter.ToUInt16(buffer, 1);
                byte noun = buffer[3];
                byte verb = buffer[4];
                await _externalApprover.ApproveMessage(res, noun, verb);
            }
        }
    }
}
