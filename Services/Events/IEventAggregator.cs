namespace MeasurementSoftware.Services.Events
{
    /// <summary>
    /// 事件聚合器接口，用于跨ViewModel通信
    /// </summary>
    public interface IEventAggregator
    {
        void Subscribe<TEvent>(Action<TEvent> handler);
        void Unsubscribe<TEvent>(Action<TEvent> handler);
        void Publish<TEvent>(TEvent eventData);
    }
}
