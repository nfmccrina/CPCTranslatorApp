using System.Net.WebSockets;
using System.Text;
using CPCTranslator.Server.Models;
using CPCTranslator.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CPCTranslator.Server.Controllers
{
    public class TranslationController : ControllerBase
    {
        private readonly IMessagingService messagingService;

        public TranslationController(IMessagingService messagingService)
        {
            this.messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
        }

        [Route("/translation")]
        public async Task Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await SendData(webSocket);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        [HttpPost("/translation")]
        public async Task<IActionResult> Post([FromBody] TranslationDto model)
        {
            if (string.IsNullOrEmpty(model.Data))
            {
                return BadRequest();
            }

            await messagingService.EnqueueMessage(model.Data);

            return Ok();
        }

        private async Task SendData(WebSocket webSocket)
        {
            using var subscription = await messagingService.Subscribe();
            while (true)
            {
                var message = await messagingService.DequeueMessage(subscription.SubscriptionId);

                if (string.IsNullOrEmpty(message))
                {
                    continue;
                }

                await webSocket.SendAsync(
                    Encoding.UTF8.GetBytes(message),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
        }
    }
}