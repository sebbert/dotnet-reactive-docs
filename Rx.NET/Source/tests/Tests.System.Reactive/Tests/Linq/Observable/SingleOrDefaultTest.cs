﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT License.
// See the LICENSE file in the project root for more information. 

using System;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.Reactive.Testing;
using ReactiveTests.Dummies;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Assert = Xunit.Assert;

namespace ReactiveTests.Tests
{
    [TestClass]
    public class SingleOrDefaultTest : ReactiveTest
    {

        [TestMethod]
        public void SingleOrDefault_ArgumentChecking()
        {
            ReactiveAssert.Throws<ArgumentNullException>(() => Observable.SingleOrDefault(default(IObservable<int>)));
            ReactiveAssert.Throws<ArgumentNullException>(() => Observable.SingleOrDefault(default(IObservable<int>), _ => true));
            ReactiveAssert.Throws<ArgumentNullException>(() => Observable.SingleOrDefault(DummyObservable<int>.Instance, default));
        }

        [TestMethod]
        public void SingleOrDefault_Empty()
        {
            Assert.Equal(default, Observable.Empty<int>().SingleOrDefault());
        }

        [TestMethod]
        public void SingleOrDefaultPredicate_Empty()
        {
            Assert.Equal(default, Observable.Empty<int>().SingleOrDefault(_ => true));
        }

        [TestMethod]
        public void SingleOrDefault_Return()
        {
            var value = 42;
            Assert.Equal(value, Observable.Return(value).SingleOrDefault());
        }

        [TestMethod]
        public void SingleOrDefault_Throw()
        {
            var ex = new Exception();

            var xs = Observable.Throw<int>(ex);

            ReactiveAssert.Throws(ex, () => xs.SingleOrDefault());
        }

        [TestMethod]
        public void SingleOrDefault_Range()
        {
            var value = 42;
            ReactiveAssert.Throws<InvalidOperationException>(() => Observable.Range(value, 10).SingleOrDefault());
        }

        [TestMethod]
        public void SingleOrDefaultPredicate_Range()
        {
            var value = 42;
            ReactiveAssert.Throws<InvalidOperationException>(() => Observable.Range(value, 10).SingleOrDefault(i => i % 2 == 0));
        }

        [TestMethod]
        public void SingleOrDefault_Range_ReducesToSingle()
        {
            var value = 42;
            Assert.Equal(45, Observable.Range(value, 10).SingleOrDefault(i => i == 45));
        }

        [TestMethod]
        public void SingleOrDefault_Range_ReducesToNone()
        {
            var value = 42;
            Assert.Equal(0, Observable.Range(value, 10).SingleOrDefault(i => i > 100));
        }

    }
}
