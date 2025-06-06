// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.Gen.Shared;
using Microsoft.Shared.DiagnosticIds;

namespace Microsoft.Gen.MetricsReports;

[Generator]
public class MetricsReportsGenerator : ISourceGenerator
{
    private const string GenerateMetricDefinitionReport = "build_property.GenerateMetricsReport";
    private const string RootNamespace = "build_property.rootnamespace";
    private const string ReportOutputPath = "build_property.MetricsReportOutputPath";
    private const string FallbackFileName = "MetricsReport.json";
    private readonly string _fileName;
    private string? _directory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsReportsGenerator"/> class.
    /// </summary>
    public MetricsReportsGenerator()
        : this(filePath: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsReportsGenerator"/> class.
    /// </summary>
    /// <param name="filePath">The report path and name.</param>
    internal MetricsReportsGenerator(string? filePath)
    {
        if (filePath is not null)
        {
            _directory = Path.GetDirectoryName(filePath);
            _fileName = Path.GetFileName(filePath);
        }
        else
        {
            _fileName = FallbackFileName;
        }
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(ClassDeclarationSyntaxReceiver.Create);
    }

    public void Execute(GeneratorExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (context.SyntaxReceiver is not ClassDeclarationSyntaxReceiver receiver ||
            receiver.ClassDeclarations.Count == 0 ||
            !GeneratorUtilities.ShouldGenerateReport(context, GenerateMetricDefinitionReport))
        {
            return;
        }

        var options = context.AnalyzerConfigOptions.GlobalOptions;
        _directory ??= GeneratorUtilities.TryRetrieveOptionsValue(options, ReportOutputPath, out var reportOutputPath)
            ? reportOutputPath!
            : GeneratorUtilities.GetDefaultReportOutputPath(options);

        if (string.IsNullOrWhiteSpace(_directory))
        {
            // Report diagnostic:
            var diagnostic = new DiagnosticDescriptor(
                DiagnosticIds.AuditReports.AUDREPGEN000,
                "MetricsReports generator couldn't resolve output path for the report. It won't be generated.",
                "Both <MetricsReportOutputPath> and <OutputPath> MSBuild properties are not set. The report won't be generated.",
                nameof(DiagnosticIds.AuditReports),
                DiagnosticSeverity.Info,
                isEnabledByDefault: true,
                helpLinkUri: string.Format(CultureInfo.InvariantCulture, DiagnosticIds.UrlFormat, DiagnosticIds.AuditReports.AUDREPGEN000));
            context.ReportDiagnostic(Diagnostic.Create(diagnostic, location: null));
            return;
        }

        var meteringParser = new Metrics.Parser(context.Compilation, context.ReportDiagnostic, context.CancellationToken);
        var meteringClasses = meteringParser.GetMetricClasses(receiver.ClassDeclarations);
        if (meteringClasses.Count == 0)
        {
            return;
        }

        _ = options.TryGetValue(RootNamespace, out var rootNamespace);
        var reportedMetrics = MetricsReportsHelpers.MapToCommonModel(meteringClasses, rootNamespace);
        var emitter = new MetricDefinitionEmitter();
        var report = emitter.GenerateReport(reportedMetrics, context.CancellationToken);

        // File IO has been marked as banned for use in analyzers, and an alternate should be used instead
        // Suppressing until this issue is addressed in https://github.com/dotnet/extensions/issues/5390

#pragma warning disable RS1035 // Do not use APIs banned for analyzers
        _ = Directory.CreateDirectory(_directory);

        File.WriteAllText(Path.Combine(_directory, _fileName), report, Encoding.UTF8);
#pragma warning restore RS1035 // Do not use APIs banned for analyzers
    }
}
