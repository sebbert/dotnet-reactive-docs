﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT License.
// See the LICENSE file in the project root for more information.

#if (CURRENT)
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace Benchmarks.System.Reactive
{
    [MemoryDiagnoser]
    public class PrependVsStartWtihBenchmark
    {
        private int _store;
#pragma warning disable IDE0052 // (Remove unread private members.) We want to store results to prevent the benchmarked code from being optimized out of existence.
        private IObservable<int> _obsStore;
#pragma warning restore IDE0052

        [Benchmark(Baseline = true)]
        public void Prepend()
        {
            Observable
                .Empty<int>()
                .Prepend(0)
                .Subscribe(v => Volatile.Write(ref _store, v));
        }

        [Benchmark]
        public void Prepend_Create()
        {
            _obsStore = Observable
                .Empty<int>()
                .Prepend(0);
        }


        private static readonly IObservable<int> _prependObservable = Observable.Empty<int>().Prepend(0);
        [Benchmark]
        public void Prepend_Subscribe()
        {
            _prependObservable
                .Subscribe(v => Volatile.Write(ref _store, v));
        }

        [Benchmark]
        public void StartWith()
        {
            Observable
                .Empty<int>()
                .StartWith(0)
                .Subscribe(v => Volatile.Write(ref _store, v));
        }

        [Benchmark]
        public void StartWith_Create()
        {
            _obsStore = Observable
                .Empty<int>()
                .StartWith(0);
        }

        private static readonly IObservable<int> _startWithObservable = Observable.Empty<int>().StartWith(0);
        [Benchmark]
        public void StartWith_Subscribe()
        {
            _startWithObservable
                .Subscribe(v => Volatile.Write(ref _store, v));
        }
    }
}
#endif
