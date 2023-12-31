﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT License.
// See the LICENSE file in the project root for more information. 

using System.Collections.Generic;
using System.Threading.Tasks;

namespace System.Reactive
{
    internal sealed class EventSource<T> : IEventSource<T>
    {
        private readonly IAsyncObservable<T> _source;
        private readonly Dictionary<Delegate, Stack<IAsyncDisposable>> _subscriptions;
        private readonly Action<Action<T>, /*object,*/ T> _invokeHandler;

        public EventSource(IAsyncObservable<T> source, Action<Action<T>, /*object,*/ T> invokeHandler)
        {
            _source = source;
            _invokeHandler = invokeHandler;
            _subscriptions = new Dictionary<Delegate, Stack<IAsyncDisposable>>();
        }

        public event Action<T> OnNext
        {
            add
            {
                var gate = new object();
                var isAdded = false;
                var isDone = false;

                var remove = new Action(() =>
                {
                    lock (gate)
                    {
                        if (isAdded)
                            Remove(value);
                        else
                            isDone = true;
                    }
                });

                //
                // [OK] Use of unsafe SubscribeAsync: non-pretentious wrapper of an observable in an event; exceptions can occur during +=.
                //
                var d = _source.SubscribeAsync(
                    x => { _invokeHandler(value, /*this,*/ x); return default; },
                    ex => { remove(); return new ValueTask(Task.FromException(ex)); },
                    () => { remove(); return default; }
                ).GetAwaiter().GetResult();

                lock (gate)
                {
                    if (!isDone)
                    {
                        Add(value, d);
                        isAdded = true;
                    }
                }
            }

            remove
            {
                Remove(value);
            }
        }

        private void Add(Delegate handler, IAsyncDisposable disposable)
        {
            lock (_subscriptions)
            {
                var l = new Stack<IAsyncDisposable>();
                if (!_subscriptions.TryGetValue(handler, out l))
                    _subscriptions[handler] = l = new Stack<IAsyncDisposable>();

                l.Push(disposable);
            }
        }

        private void Remove(Delegate handler)
        {
            var d = default(IAsyncDisposable);

            lock (_subscriptions)
            {
                var l = new Stack<IAsyncDisposable>();
                if (_subscriptions.TryGetValue(handler, out l))
                {
                    d = l.Pop();
                    if (l.Count == 0)
                        _subscriptions.Remove(handler);
                }
            }

            d?.DisposeAsync().GetAwaiter().GetResult();
        }
    }
}
