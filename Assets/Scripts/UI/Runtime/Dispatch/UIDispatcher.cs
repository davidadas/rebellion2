using System;
using System.Collections.Generic;

public sealed class UIDispatcher
{
    private readonly Dictionary<Type, Action<IUIDispatchRequest>> handlers =
        new Dictionary<Type, Action<IUIDispatchRequest>>();

    public void Register<TRequest>(Action<TRequest> handler)
        where TRequest : IUIDispatchRequest
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        handlers[typeof(TRequest)] = request => handler((TRequest)request);
    }

    public void Send<TRequest>(TRequest request)
        where TRequest : IUIDispatchRequest
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        Type requestType = request.GetType();
        if (handlers.TryGetValue(requestType, out Action<IUIDispatchRequest> handler))
        {
            handler(request);
            return;
        }

        if (handlers.TryGetValue(typeof(TRequest), out handler))
        {
            handler(request);
            return;
        }

        throw new InvalidOperationException(
            $"No UI dispatch handler registered for {requestType.Name}."
        );
    }
}
