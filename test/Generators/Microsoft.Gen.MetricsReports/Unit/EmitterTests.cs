﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Gen.Metrics.Model;
using Microsoft.Gen.MetricsReports;
using Xunit;

namespace Microsoft.Gen.MetricsReports.Test;

public class EmitterTests
{
    private static readonly ReportedMetricClass[] _metricClasses = new[]
        {
            new ReportedMetricClass
            {
                Name = "MetricClass1",
                RootNamespace = "MetricContainingAssembly1",
                Methods = new []
                {
                    new ReportedMetricMethod
                    {
                        MetricName = "Requests",
                        Summary = "Requests summary.",
                        Kind = InstrumentKind.Counter,
                        Dimensions = ["StatusCode", "ErrorCode"],
                        DimensionsDescriptions = new Dictionary<string, string>
                        {
                            { "StatusCode", "Status code for request." },
                            { "ErrorCode", "Error code for request." }
                        }
                    },
                    new ReportedMetricMethod
                    {
                        MetricName = "Latency",
                        Summary = "Latency summary.",
                        Kind = InstrumentKind.Histogram,
                        Dimensions = ["Dim1"],
                        DimensionsDescriptions = []
                    },
                    new ReportedMetricMethod
                    {
                        MetricName = "MemoryUsage",
                        Kind = InstrumentKind.Gauge,
                        Dimensions = [],
                        DimensionsDescriptions = []
                    }
                }
            },
            new ReportedMetricClass
            {
                Name = "MetricClass2",
                RootNamespace = "MetricContainingAssembly2",
                Methods = new[]
                {
                    new ReportedMetricMethod
                    {
                        MetricName = "Counter",
                        Summary = "Counter summary.",
                        Kind = InstrumentKind.Counter,
                        Dimensions = [],
                        DimensionsDescriptions = []
                    },
                    new ReportedMetricMethod
                    {
                        MetricName = "Test\\MemoryUsage",
                        Summary = "MemoryUsage summary.",
                        Kind = InstrumentKind.Gauge,
                        Dimensions = ["Path"],
                        DimensionsDescriptions = new Dictionary<string, string>
                        {
                            { "Path", "Test\\Description\\Path" }
                        },
                    }
                }
            }
    };

    [Fact]
    public void EmitterShouldThrowExceptionUponCancellation()
    {
        Assert.Throws<OperationCanceledException>(() =>
        {
            var emitter = new MetricDefinitionEmitter();
            emitter.GenerateReport(_metricClasses, new CancellationToken(true));
        });
    }

    [Fact]
    public void EmitterShouldOutputEmptyForNullInput()
    {
        var emitter = new MetricDefinitionEmitter();
        Assert.Equal(string.Empty, emitter.GenerateReport(null!, CancellationToken.None));
    }

    [Fact]
    public void EmitterShouldOutputEmptyForEmptyInputForMetricClass()
    {
        var emitter = new MetricDefinitionEmitter();
        Assert.Equal(string.Empty, emitter.GenerateReport(Array.Empty<ReportedMetricClass>(), CancellationToken.None));
    }

    [Fact]
    public void GetMetricClassDefinition_GivenMetricTypeIsUnknown_ThrowsNotSupportedException()
    {
        const int UnknownMetricType = 10;
        var emitter = new MetricDefinitionEmitter();
        var metricClass = new ReportedMetricClass
        {
            Name = "Test",
            RootNamespace = "MetricContainingAssembly3",
            Methods = new[]
            {
                new ReportedMetricMethod
                {
                    MetricName = "UnknownMetric",
                    Kind = (InstrumentKind)UnknownMetricType,
                    Dimensions = ["Dim1"]
                }
            }
        };

        Assert.Throws<NotSupportedException>(() => emitter.GenerateReport(new[] { metricClass }, CancellationToken.None));
    }

    [Fact]
    public void EmitterShouldOutputInJSONFormat()
    {
        string expected = @"
[
    {
        ""MetricContainingAssembly1"":
        [
            {
                ""MetricName"": ""Requests"",
                ""MetricDescription"": ""Requests summary."",
                ""Dimensions"":
                {
                    ""StatusCode"": ""Status code for request."",
                    ""ErrorCode"": ""Error code for request.""
                }
            },
            {
                ""MetricName"": ""Latency"",
                ""MetricDescription"": ""Latency summary."",
                ""Dimensions"":
                {
                }
            },
            {
                ""MetricName"": ""MemoryUsage"",
                ""InstrumentName"": ""Gauge""
            }
        ]
    }    ,
,
    {
        ""MetricContainingAssembly2"":
        [
            {
                ""MetricName"": ""Counter"",
                ""MetricDescription"": ""Counter summary."",
                ""InstrumentName"": ""Counter""
            },
            {
                ""MetricName"": ""Test\\\\MemoryUsage"",
                ""MetricDescription"": ""MemoryUsage summary."",
                ""Dimensions"":
                {
                    ""Path"": ""Test\\\\Description\\\\Path""
                }
            }
        ]
    }
]";

        var emitter = new MetricDefinitionEmitter();
        string json = emitter.GenerateReport(_metricClasses, CancellationToken.None);

        Assert.Equal(expected, json);
    }
}
