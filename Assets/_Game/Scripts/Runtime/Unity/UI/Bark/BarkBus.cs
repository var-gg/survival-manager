using System;
using System.Collections.Generic;

namespace SM.Unity.UI.Bark;

/// <summary>
/// 잿골 hub V3 face cluster용 minimal signal bus.
/// `pindoc://analysis-hero-face-card-bark-emotion-system` baseline의 첫 단계.
/// god bus는 만들지 않는다 — bark 한 가지 event만 다루고 subscriber count에 무관하게 동작.
/// scene reload 시 _subscribers는 caller가 직접 unsubscribe해야 함 (event leak 방지).
/// </summary>
public sealed class BarkBus
{
    private readonly List<Action<BarkEvent>> _subscribers = new();

    public IDisposable Subscribe(Action<BarkEvent> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        _subscribers.Add(handler);
        return new Subscription(this, handler);
    }

    public void Publish(BarkEvent evt)
    {
        if (evt == null) return;
        // snapshot 후 invoke — handler 안에서 unsubscribe해도 collection mutation 안전.
        var snapshot = _subscribers.ToArray();
        foreach (var handler in snapshot)
        {
            handler.Invoke(evt);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly BarkBus _bus;
        private Action<BarkEvent>? _handler;

        public Subscription(BarkBus bus, Action<BarkEvent> handler)
        {
            _bus = bus;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_handler == null) return;
            _bus._subscribers.Remove(_handler);
            _handler = null;
        }
    }
}
