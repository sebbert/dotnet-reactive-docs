﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT License.
// See the LICENSE file in the project root for more information. 

using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using Microsoft.Reactive.Testing;
using ReactiveTests.Dummies;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Assert = Xunit.Assert;

namespace ReactiveTests.Tests
{
    [TestClass]
    public class EmptyTest : ReactiveTest
    {

        [TestMethod]
        public void Empty_ArgumentChecking()
        {
            ReactiveAssert.Throws<ArgumentNullException>(() => Observable.Empty<int>(null));
            ReactiveAssert.Throws<ArgumentNullException>(() => Observable.Empty(null, 42));
            ReactiveAssert.Throws<ArgumentNullException>(() => Observable.Empty<int>(DummyScheduler.Instance).Subscribe(null));
        }

        [TestMethod]
        public void Empty_Basic()
        {
            var scheduler = new TestScheduler();

            var res = scheduler.Start(() =>
                Observable.Empty<int>(scheduler)
            );

            res.Messages.AssertEqual(
                OnCompleted<int>(201)
            );
        }

        [TestMethod]
        public void Empty_Disposed()
        {
            var scheduler = new TestScheduler();

            var res = scheduler.Start(() =>
                Observable.Empty<int>(scheduler),
                200
            );

            res.Messages.AssertEqual(
            );
        }

        [TestMethod]
        public void Empty_ObserverThrows()
        {
            var scheduler1 = new TestScheduler();

            var xs = Observable.Empty<int>(scheduler1);

            xs.Subscribe(x => { }, exception => { }, () => { throw new InvalidOperationException(); });

            ReactiveAssert.Throws<InvalidOperationException>(() => scheduler1.Start());
        }

        [TestMethod]
        public void Empty_DefaultScheduler()
        {
            Observable.Empty<int>().AssertEqual(Observable.Empty<int>(DefaultScheduler.Instance));
        }

        [TestMethod]
        public void Empty_Basic_Witness1()
        {
            var scheduler = new TestScheduler();

            var res = scheduler.Start(() =>
                Observable.Empty(scheduler, 42)
            );

            res.Messages.AssertEqual(
                OnCompleted<int>(201)
            );
        }

        [TestMethod]
        public void Empty_Basic_Witness2()
        {
            var e = new ManualResetEvent(false);

            Observable.Empty(42).Subscribe(
                _ => { Assert.True(false); },
                _ => { Assert.True(false); },
                () => e.Set()
            );

            e.WaitOne();
        }

    }
}
