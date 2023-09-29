using System.Collections.Concurrent;

namespace CPCTranslator.Server.Services
{
    public class MessagingServiceSubscription : IDisposable
    {
        public MessagingServiceSubscription(Action<Guid> disposalCallback)
        {
            SubscriptionId = Guid.NewGuid();
            this.disposalCallback = disposalCallback;
        }
        public Guid SubscriptionId { get; }
        public void Dispose()
        {
            disposalCallback(SubscriptionId);
        }

        private Action<Guid> disposalCallback;
    }
    public interface IMessagingService
    {
        Task EnqueueMessage(string message);
        Task<string?> DequeueMessage(Guid subscriberId);
        Task<MessagingServiceSubscription> Subscribe();
    }

    public class MessagingService : IMessagingService
    {
        private Dictionary<Guid, BlockingCollection<string>> queue;

        public MessagingService()
        {
            queue = new Dictionary<Guid, BlockingCollection<string>>();
        }

        public Task<string?> DequeueMessage(Guid subscriberId)
        {
            if (!queue.ContainsKey(subscriberId))
            {
                return Task.FromResult((string?)null);
            }
            
            var q = queue[subscriberId];
            if (q.Count < 1)
            {
                return Task.FromResult((string?)null);
            }

            return Task.FromResult((string?)q.Take());
        }

        public Task EnqueueMessage(string message)
        {
            foreach (var q in queue.Values)
            {
                q.Add(message);
            }

            return Task.CompletedTask;
        }

        public Task<MessagingServiceSubscription> Subscribe()
        {
            var subscription = new MessagingServiceSubscription(id => {
                if (this.queue.ContainsKey(id))
                {
                    this.queue.Remove(id);
                }
            });

            queue.Add(subscription.SubscriptionId, new BlockingCollection<string>(256));

            return Task.FromResult(subscription);
        }
    }
}