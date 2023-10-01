using System.Net.WebSockets;
using System.Text;
using CPCTranslator.Server.Models;
using CPCTranslator.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPCTranslator.Server.Controllers
{
    public class TranslationController : ControllerBase
    {
        private readonly IBackgroundSocketProcessor socketProcessor;

        public TranslationController(IEnumerable<IHostedService> socketProcessors)
        {
            if (socketProcessors == null)
            {
                throw new ArgumentNullException(nameof(socketProcessor));
            }

            socketProcessor = (BackgroundSocketProcessor)(socketProcessors.Where(o => o.GetType() == typeof(BackgroundSocketProcessor)).FirstOrDefault() ?? throw new ArgumentNullException(nameof(BackgroundSocketProcessor)));
        }

        [Route("/translation")]
        public async Task Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

                var socketFinishedTCS = new TaskCompletionSource<object>();
                socketProcessor.AddSocket(webSocket, socketFinishedTCS);

                await socketFinishedTCS.Task;

                ;
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}