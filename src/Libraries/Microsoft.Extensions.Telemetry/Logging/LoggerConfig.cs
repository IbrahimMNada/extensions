﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Diagnostics.Enrichment;

namespace Microsoft.Extensions.Logging;

internal sealed class LoggerConfig
{
#pragma warning disable S107 // Methods should not have too many parameters
    public LoggerConfig(
        KeyValuePair<string, object?>[] staticTags,
        Action<IEnrichmentTagCollector>[] enrichers,
        LoggingSampler? sampler,
        bool captureStackTraces,
        bool useFileInfoForStackTraces,
        bool includeExceptionMessage,
        int maxStackTraceLength,
        Func<DataClassificationSet, Redactor> getRedactor,
        bool addRedactionDiscriminator)
    {
#pragma warning restore S107 // Methods should not have too many parameters
        StaticTags = staticTags;
        Enrichers = enrichers;
        Sampler = sampler;
        CaptureStackTraces = captureStackTraces;
        UseFileInfoForStackTraces = useFileInfoForStackTraces;
        MaxStackTraceLength = maxStackTraceLength;
        IncludeExceptionMessage = includeExceptionMessage;
        GetRedactor = getRedactor;
        AddRedactionDiscriminator = addRedactionDiscriminator;
    }

    public KeyValuePair<string, object?>[] StaticTags { get; }
    public Action<IEnrichmentTagCollector>[] Enrichers { get; }
    public LoggingSampler? Sampler { get; }
    public bool CaptureStackTraces { get; }
    public bool UseFileInfoForStackTraces { get; }
    public bool IncludeExceptionMessage { get; }
    public int MaxStackTraceLength { get; }
    public Func<DataClassificationSet, Redactor> GetRedactor { get; }
    public bool AddRedactionDiscriminator { get; }
}
