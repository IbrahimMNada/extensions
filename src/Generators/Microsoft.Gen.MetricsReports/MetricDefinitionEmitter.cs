// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Gen.Metrics.Model;
using Microsoft.Gen.Shared;

namespace Microsoft.Gen.MetricsReports;

// Stryker disable all

internal sealed class MetricDefinitionEmitter : EmitterBase
{
    private const int RootIndentLevel = 2;
    private const int DimensionsIndentLevel = 3;

    internal MetricDefinitionEmitter()
        : base(false)
    {
    }

    /// <summary>
    /// Generates JSON object containing the <see cref="ReportedMetricClass"/> for metrics report.
    /// </summary>
    /// <param name="metricClasses">The reported metric classes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="indentationLevel">The number of indentations in case its nested in other reports like <see cref="MetadataReportsGenerator"/>.Defaulted to zero.</param>
    /// <returns>string report as json or String.Empty.</returns>
    public string GenerateReport(IReadOnlyList<ReportedMetricClass> metricClasses, CancellationToken cancellationToken = default, int indentationLevel = 0)
    {
        if (metricClasses == null || metricClasses.Count == 0)
        {
            return string.Empty;
        }

        if (indentationLevel > 0)
        {
            OutLn();
        }

        Indent(indentationLevel);
        OutLn("[");

        for (int i = 0; i < metricClasses.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metricClass = metricClasses[i];

            GenMetricClassDefinition(metricClass, cancellationToken);

            if (i < metricClasses.Count - 1)
            {
                OutLn(",");
            }
        }

        OutLn("]");
        Unindent(indentationLevel);

        return Capture();
    }

    private void GenMetricClassDefinition(ReportedMetricClass metricClass, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Indent();
        OutLn("{");

        OutLn($" \"{metricClass.RootNamespace}\":");

        if (metricClass.Methods.Length > 0)
        {
            Indent(RootIndentLevel);
            OutLn("[");

            for (int j = 0; j < metricClass.Methods.Length; j++)
            {
                Indent();
                var metricMethod = metricClass.Methods[j];

                GenMetricMethodDefinition(metricMethod, cancellationToken);

                if (j < metricClass.Methods.Length - 1)
                {
                    OutLn(",");
                }

                Unindent();
            }

            OutLn("]");
            Unindent(RootIndentLevel);
        }

        OutLn("}");
        Unindent();
    }

    private void GenMetricMethodDefinition(ReportedMetricMethod metricMethod, CancellationToken cancellationToken)
    {
        switch (metricMethod.Kind)
        {
            case InstrumentKind.Counter:
            case InstrumentKind.Histogram:
            case InstrumentKind.Gauge:
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    OutLn("{");

                    OutLn($"     \"MetricName\": \"{metricMethod.MetricName.Replace("\\", "\\\\").Replace("\"", "\\\"")}\",");

                    if (!string.IsNullOrEmpty(metricMethod.Summary))
                    {
                        OutLn($"     \"MetricDescription\": \"{metricMethod.Summary.Replace("\\", "\\\\").Replace("\"", "\\\"")}\",");
                    }

                    if (metricMethod.Dimensions.Count == 0)
                    {
                        OutLn($"     \"InstrumentName\": \"{metricMethod.Kind}\"");
                    }

                    if (metricMethod.Dimensions.Count > 0)
                    {
                        OutLn($"     \"InstrumentName\": \"{metricMethod.Kind}\",");
                        OutLn("     \"Dimensions\":");
                        Indent(DimensionsIndentLevel);
                        OutLn("{");
                        Indent();
                        int k = 0;
                        foreach (var dimension in metricMethod.Dimensions)
                        {
                            bool commaCondition = k < metricMethod.Dimensions.Count - 1;
                            if (metricMethod.DimensionsDescriptions.TryGetValue(dimension, out var description))
                            {
                                OutLn($"\"{dimension}\": \"{description.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"{(commaCondition ? "," : string.Empty)}");
                            }
                            else
                            {
                                OutLn($"\"{dimension}\": \"\"{(commaCondition ? "," : string.Empty)}");
                            }

                            k++;
                        }

                        Unindent();
                        OutLn("}");
                        Unindent(DimensionsIndentLevel);
                    }
                    else
                    {
                        OutLn();
                    }

                    OutLn("}");
                }
                catch (Exception e)
                {
                    // This should report diagnostic.
                    throw new InvalidOperationException($"An exception occurred during metric report generation {e.GetType()}:{e.Message}.");
                }

                break;
            case InstrumentKind.None:
            case InstrumentKind.CounterT:
            case InstrumentKind.HistogramT:
            default:
                // This should report diagnostic.
                throw new NotSupportedException($"Report for metric kind: '{metricMethod.Kind}' is not supported.");
        }
    }
}
