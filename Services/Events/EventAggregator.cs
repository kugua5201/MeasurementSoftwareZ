using System.Collections.Concurrent;

namespace MeasurementSoftware.Services.Events
{
    /// <summary>
    /// 事件聚合器实现
    /// </summary>
    public class EventAggregator : IEventAggregator
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _subscribers = new();

        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            var eventType = typeof(TEvent);
            _subscribers.AddOrUpdate(
                eventType,
                _ => new List<Delegate> { handler },
                (_, handlers) =>
                {
                    handlers.Add(handler);
                    return handlers;
                });
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            var eventType = typeof(TEvent);
            if (_subscribers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
            }
        }

        public void Publish<TEvent>(TEvent eventData)
        {
            var eventType = typeof(TEvent);
            if (_subscribers.TryGetValue(eventType, out var handlers))
            {
                foreach (var handler in handlers.ToList())
                {
                    ((Action<TEvent>)handler)?.Invoke(eventData);
                }
            }
        }
    }
}
