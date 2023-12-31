﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT License.
// See the LICENSE file in the project root for more information. 

#if HAS_REMOTING
using System.Reactive.Disposables;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Security;
using System.Threading;

//
// DESIGN: The MarshalByRefObject (MBRO) implementations for RemotableObserver and RemotableSubscription act as
//         self-sponsoring objects controlling their lease times in order to tie those to the lifetime of the
//         underlying observable sequence (ended by OnError or OnCompleted) or the user-controlled subscription
//         lifetime. If we were to implement InitializeLifetimeService to return null, we'd end up with leases
//         that are infinite, so we need a more fine-grained lease scheme. The default configuration would time
//         out after 5 minutes, causing clients to fail while they're still observing the sequence. To solve
//         this, those MBROs also implement ISponsor with a Renewal method that continues to renew the lease
//         upon every call. When the sequence comes to an end or the subscription is disposed, the sponsor gets
//         unregistered, allowing the objects to be reclaimed eventually by the Remoting infrastructure.
//
// SECURITY: Registration and unregistration of sponsors is protected by SecurityCritical annotations. The
//           implementation of ISponsor is known (i.e. no foreign implementation can be passed in) at the call
//           sites of the Register and Unregister methods. The call to Register happens in the SecurityCritical
//           InitializeLifetimeService method and is called by trusted Remoting infrastructure. The Renewal
//           method is also marked as SecurityCritical and called by Remoting. The Unregister method is wrapped
//           in a ***SecurityTreatAsSafe*** private method which only gets called by the observer's OnError and
//           OnCompleted notifications, or the subscription's Dispose method. In the former case, the sequence
//           indicates it has reached the end, and hence resources can be reclaimed. Clients will no longer be
//           connected to the source due to auto-detach behavior enforced in the SerializableObservable client-
//           side implementation. In the latter case of disposing the subscription, the client is in control
//           and will cause the underlying remote subscription to be disposed as well, allowing resources to be
//           reclaimed. Rogue messages on either the data or the subscription channel can cause a DoS of the
//           client-server communication but this is subject to the security of the Remoting channels used. In
//           no case an untrusted party can cause _extension_ of the lease time.
//
//
// Notice this assembly is marked as APTCA in official builds, causing methods to be treated as transparent,
// thus requiring the ***SecurityTreatAsSafe*** annotation on the security boundaries described above. When not
// applied, the following exception would occur at runtime:
//
//    System.MethodAccessException:
//
//    Attempt by security transparent method 'System.Reactive.Linq.QueryLanguage+RemotableObservable`1+
//    RemotableSubscription<T>.Unregister()' to access security critical method 'System.Runtime.Remoting.Lifetime.
//    ILease.Unregister(System.Runtime.Remoting.Lifetime.ISponsor)' failed.
//
//    Assembly 'System.Reactive.Linq, Version=2.0.ymmdd.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35'
//    is marked with the AllowPartiallyTrustedCallersAttribute, and uses the level 2 security transparency model.
//    Level 2 transparency causes all methods in AllowPartiallyTrustedCallers assemblies to become security
//    transparent by default, which may be the cause of this exception.
//
//
// The two CodeAnalysis suppressions below are explained by the Justification property (scroll to the right):
//
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2136:TransparencyAnnotationsShouldNotConflictFxCopRule", Scope = "member", Target = "~M:System.Reactive.Linq.RemotingObservable.RemotableObserver`1.Unregister", Justification = "This error only occurs while running FxCop on local builds that don't have NO_CODECOVERAGE set, causing the assembly not to be marked with APTCA (see AssemblyInfo.cs). When APTCA is enabled in official builds, this SecurityTreatAsSafe annotation is required.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2136:TransparencyAnnotationsShouldNotConflictFxCopRule", Scope = "member", Target = "~M:System.Reactive.Linq.RemotingObservable.RemotableObservable`1.RemotableSubscription.Unregister", Justification = "This error only occurs while running FxCop on local builds that don't have NO_CODECOVERAGE set, causing the assembly not to be marked with APTCA (see AssemblyInfo.cs). When APTCA is enabled in official builds, this SecurityTreatAsSafe annotation is required.")]

namespace System.Reactive.Linq
{
    public static partial class RemotingObservable
    {
        #region Remotable

        private static IObservable<TSource> Remotable_<TSource>(IObservable<TSource> source)
        {
            return new SerializableObservable<TSource>(new RemotableObservable<TSource>(source, null));
        }

        private static IObservable<TSource> Remotable_<TSource>(IObservable<TSource> source, ILease? lease)
        {
            return new SerializableObservable<TSource>(new RemotableObservable<TSource>(source, lease));
        }

        [Serializable]
        private class SerializableObservable<T> : IObservable<T>
        {
            private readonly RemotableObservable<T> _remotableObservable;

            public SerializableObservable(RemotableObservable<T> remotableObservable)
            {
                _remotableObservable = remotableObservable;
            }

            public IDisposable Subscribe(IObserver<T> observer)
            {
                var consumer = SafeObserver<T>.Wrap(observer);

                //
                // [OK] Use of unsafe Subscribe: non-pretentious transparent wrapping through remoting; exception coming from the remote object is not re-routed.
                //
                var d = _remotableObservable.Subscribe/*Unsafe*/(new RemotableObserver<T>(consumer));

                consumer.SetResource(d);

                return d;
            }
        }

        private class RemotableObserver<T> : MarshalByRefObject, IObserver<T>, ISponsor
        {
            private readonly IObserver<T> _underlyingObserver;

            public RemotableObserver(IObserver<T> underlyingObserver)
            {
                _underlyingObserver = underlyingObserver;
            }

            public void OnNext(T value)
            {
                _underlyingObserver.OnNext(value);
            }

            public void OnError(Exception exception)
            {
                try
                {
                    _underlyingObserver.OnError(exception);
                }
                finally
                {
                    Unregister();
                }
            }

            public void OnCompleted()
            {
                try
                {
                    _underlyingObserver.OnCompleted();
                }
                finally
                {
                    Unregister();
                }
            }

            [SecuritySafeCritical] // See remarks at the top of the file.
            private void Unregister()
            {
                var lease = (ILease)RemotingServices.GetLifetimeService(this);
                lease?.Unregister(this);
            }

            [SecurityCritical]
            public override object InitializeLifetimeService()
            {
                var lease = (ILease)base.InitializeLifetimeService();
                lease.Register(this);
                return lease;
            }

            [SecurityCritical]
            TimeSpan ISponsor.Renewal(ILease lease)
            {
                return lease.InitialLeaseTime;
            }
        }

        [Serializable]
        private sealed class RemotableObservable<T> : MarshalByRefObject, IObservable<T>
        {
            private readonly IObservable<T> _underlyingObservable;
            private readonly ILease? _lease;

            public RemotableObservable(IObservable<T> underlyingObservable, ILease? lease)
            {
                _underlyingObservable = underlyingObservable;
                _lease = lease;
            }

            public IDisposable Subscribe(IObserver<T> observer)
            {
                //
                // [OK] Use of unsafe Subscribe: non-pretentious transparent wrapping through remoting; throwing across remoting boundaries is fine.
                //
                return new RemotableSubscription(_underlyingObservable.Subscribe/*Unsafe*/(observer));
            }

            [SecurityCritical]
            public override object? InitializeLifetimeService()
            {
                return _lease;
            }

            private sealed class RemotableSubscription : MarshalByRefObject, IDisposable, ISponsor
            {
                private IDisposable _underlyingSubscription;

                public RemotableSubscription(IDisposable underlyingSubscription)
                {
                    _underlyingSubscription = underlyingSubscription;
                }

                public void Dispose()
                {
                    //
                    // Avoiding double-dispose and dropping the reference upon disposal.
                    //
                    using (Interlocked.Exchange(ref _underlyingSubscription, Disposable.Empty))
                    {
                        Unregister();
                    }
                }

                [SecuritySafeCritical] // See remarks at the top of the file.
                private void Unregister()
                {
                    var lease = (ILease)RemotingServices.GetLifetimeService(this);
                    lease?.Unregister(this);
                }

                [SecurityCritical]
                public override object InitializeLifetimeService()
                {
                    var lease = (ILease)base.InitializeLifetimeService();
                    lease.Register(this);
                    return lease;
                }

                [SecurityCritical]
                TimeSpan ISponsor.Renewal(ILease lease)
                {
                    return lease.InitialLeaseTime;
                }
            }
        }

        #endregion
    }
}
#endif
