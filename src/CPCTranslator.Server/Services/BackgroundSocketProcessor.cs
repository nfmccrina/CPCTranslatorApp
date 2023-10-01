using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Azure.Messaging.ServiceBus;

namespace CPCTranslator.Server.Services
{
    public interface IBackgroundSocketProcessor : IHostedService
    {
        void AddSocket(WebSocket socket, TaskCompletionSource<object> socketFinishedTCS);
    }

    class SocketInfo
    {
        public SocketInfo(
            WebSocket socket,
            TaskCompletionSource<object> socketFinishedTCS,
            Task heartbeatTask
        )
        {
            Socket = socket;
            SocketFinishedTCS = socketFinishedTCS;
            HeartbeatTask = heartbeatTask;
            LastPing = DateTimeOffset.Now;
        }
        public WebSocket Socket { get; }
        public TaskCompletionSource<object> SocketFinishedTCS { get; }
        public Task HeartbeatTask { get; }
        public DateTimeOffset LastPing { get; set; }
    }

    public class BackgroundSocketProcessor : BackgroundService, IBackgroundSocketProcessor
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<BackgroundSocketProcessor> logger;
        private ServiceBusClient? serviceBusClient;
        private ServiceBusProcessor? serviceBusProcessor;
        private IDictionary<Guid, SocketInfo> sockets;
        private CancellationToken? stoppingToken;

        public BackgroundSocketProcessor(IConfiguration configuration, ILogger<BackgroundSocketProcessor> logger)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            sockets = new Dictionary<Guid, SocketInfo>();
            serviceBusClient = null;
            serviceBusProcessor = null;
            stoppingToken = null;
        }

        public void AddSocket(WebSocket socket, TaskCompletionSource<object> socketFinishedTCS)
        {
            var newId = Guid.NewGuid();

            var heartbeatTask = Task.Run(async () => {
                var buffer = new byte[1024];
                while (!stoppingToken?.IsCancellationRequested ?? false) {
                    var heartbeatResponse = await socket.ReceiveAsync(buffer, stoppingToken!.Value);

                    if (heartbeatResponse.CloseStatus.HasValue)
                    {
                        CloseSocket(newId);
                        break;
                    }

                    if (sockets.ContainsKey(newId))
                    {
                        sockets[newId].LastPing = DateTimeOffset.Now;
                    }
                }
            });

            sockets.Add(newId, new SocketInfo(socket, socketFinishedTCS, heartbeatTask));
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            serviceBusClient = new ServiceBusClient(configuration["ServiceBusListen"]);
            serviceBusProcessor = serviceBusClient.CreateProcessor(configuration["QueueName"]);

            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var socket in sockets.Values)
            {
                socket.SocketFinishedTCS.TrySetResult(new());
            }

            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (serviceBusProcessor == null)
            {
                throw new Exception("serviceBusProcessor is null");
            }

            this.stoppingToken = stoppingToken;

            serviceBusProcessor.ProcessMessageAsync += HandleTranslationMessage;
            serviceBusProcessor.ProcessErrorAsync += ErrorHandler;

            await serviceBusProcessor.StartProcessingAsync(stoppingToken);
        }

        private async Task HandleTranslationMessage(ProcessMessageEventArgs args)
        {
            var body = args.Message.Body.ToString();

            foreach (var key in sockets.Keys)
            {
                SocketInfo? currentSocket;
                var socketExists = sockets.TryGetValue(key, out currentSocket);

                if (!socketExists || currentSocket == null)
                {
                    continue;
                }

                if (currentSocket.LastPing < DateTimeOffset.Now.AddMinutes(-2) || currentSocket.HeartbeatTask.IsCompleted || currentSocket.HeartbeatTask.IsCompletedSuccessfully)
                {
                    CloseSocket(key);
                    continue;
                }

                try
                {
                    await currentSocket.Socket.SendAsync(Encoding.UTF8.GetBytes(body), WebSocketMessageType.Text, true, args.CancellationToken);
                }
                catch (WebSocketException ex)
                {
                    logger.LogError($"Error sending to socket (it has probably been closed normally): {ex.Message}");
                }
            }

            await args.CompleteMessageAsync(args.Message);
        }

        private void CloseSocket(Guid key)
        {
            if (sockets.ContainsKey(key))
            {
                var socket = sockets[key];
                socket.SocketFinishedTCS.TrySetResult(new());
                sockets.Remove(key);
            }
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            logger.LogError(args.Exception.ToString());
            return Task.CompletedTask;
        }
    }
}