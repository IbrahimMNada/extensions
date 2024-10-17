﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Gen.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Gen.MetadataExtractor.Unit.Tests;

public class GeneratorTests(ITestOutputHelper output)
{
    private const string ReportFilename = "MetadataReport.json";
    private const string TestTaxonomy = @"
        using Microsoft.Extensions.Compliance.Classification;

        public sealed class C1Attribute : DataClassificationAttribute
        {
            public C1Attribute(new DataClassification(""TAX"", 1)) { }
        }

        public sealed class C2Attribute : DataClassificationAttribute
        {
            public C2Attribute(new DataClassification(""TAX"", 2)) { }
        }

        public sealed class C3Attribute : DataClassificationAttribute
        {
            public C3Attribute(new DataClassification(""TAX"", 4)) { }
        }

        public sealed class C4Attribute : DataClassificationAttribute
        {
            public C4Attribute(new DataClassification(""TAX"", 8)) { }
        }
    ";

    [Fact]
    public void GeneratorShouldNotDoAnythingIfGeneralExecutionContextDoesNotHaveClassDeclarationSyntaxReceiver()
    {
        GeneratorExecutionContext defaultGeneralExecutionContext = default;
        new MetadataReportsGenerator().Execute(defaultGeneralExecutionContext);

        Assert.Null(defaultGeneralExecutionContext.SyntaxReceiver);
    }

    [Theory]
    [CombinatorialData]
    public async Task TestAll(bool useExplicitReportPath)
    {
        Dictionary<string, string>? options = useExplicitReportPath
            ? new() { ["build_property.MetadataReportOutputPath"] = Directory.GetCurrentDirectory() }
            : null;

        foreach (var inputFile in Directory.GetFiles("TestClasses"))
        {
            var stem = Path.GetFileNameWithoutExtension(inputFile);
            var goldenFileName = Path.ChangeExtension(stem, ".json");
            var goldenReportPath = Path.Combine("GoldenReports", goldenFileName);

            var generatedReportPath = Path.Combine(Directory.GetCurrentDirectory(), ReportFilename);

            if (File.Exists(goldenReportPath))
            {
                var d = await RunGenerator(await File.ReadAllTextAsync(inputFile), options);
                Assert.Empty(d);

                var golden = await File.ReadAllTextAsync(goldenReportPath);
                var generated = await File.ReadAllTextAsync(generatedReportPath);

                if (golden != generated)
                {
                    output.WriteLine($"MISMATCH: golden report {goldenReportPath}, generated {generatedReportPath}");
                    output.WriteLine("----");
                    output.WriteLine("golden:");
                    output.WriteLine(golden);
                    output.WriteLine("----");
                    output.WriteLine("generated:");
                    output.WriteLine(generated);
                    output.WriteLine("----");
                }

                File.Delete(generatedReportPath);

                golden = Regex.Replace(golden, @"\s+", "");
                generated = Regex.Replace(generated, @"\s+", "");

                golden = golden.Replace("\r\n", "\n");
                generated = generated.Replace("\r\n", "\n");

                Assert.Equal(golden, generated);
            }
            else
            {
                // generate the golden file if it doesn't already exist
                output.WriteLine($"Generating golden report: {goldenReportPath}");
                _ = await RunGenerator(await File.ReadAllTextAsync(inputFile), options, reportFileName: goldenFileName);
            }
        }
    }

    [Fact]
    public async Task ShouldNot_Generate_WhenDisabledViaConfig()
    {
        var inputFile = Directory.GetFiles("TestClasses").First();
        var options = new Dictionary<string, string>
        {
            ["build_property.GenerateMetadataReport"] = bool.FalseString,
            ["build_property.MetadataReportOutputPath"] = Path.GetTempPath(),
            ["build_property.rootnamespace"] = "TestClasses"
        };

        var d = await RunGenerator(await File.ReadAllTextAsync(inputFile), options);
        Assert.Empty(d);
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), ReportFilename)));
    }

    [Theory]
    [CombinatorialData]
    public async Task Should_EmitWarning_WhenPathUnavailable(bool isReportPathProvided)
    {
        var inputFile = Directory.GetFiles("TestClasses").First();
        var options = new Dictionary<string, string>
        {
            ["build_property.MetadataReportOutputPath"] = string.Empty
        };

        if (isReportPathProvided)
        {
            options.Add("build_property.MetadataReportOutputPath", string.Empty);
        }

        var diags = await RunGenerator(await File.ReadAllTextAsync(inputFile), options);
        var diag = Assert.Single(diags);
        Assert.Equal("AUDREPGEN000", diag.Id);
        Assert.Equal(DiagnosticSeverity.Info, diag.Severity);
    }

    [Fact]
    public async Task Should_UseProjectDir_WhenOutputPathIsRelative()
    {
        var projectDir = Path.GetTempPath();
        var outputPath = Guid.NewGuid().ToString();
        var fullReportPath = Path.Combine(projectDir, outputPath);
        Directory.CreateDirectory(fullReportPath);

        try
        {
            var inputFile = Directory.GetFiles("TestClasses").First();
            var options = new Dictionary<string, string>
            {
                ["build_property.projectdir"] = projectDir,
                ["build_property.MetadataReportOutputPath"] = outputPath
            };

            var diags = await RunGenerator(await File.ReadAllTextAsync(inputFile), options);
            Assert.Empty(diags);
            Assert.True(File.Exists(Path.Combine(fullReportPath, ReportFilename)));
        }
        finally
        {
            Directory.Delete(fullReportPath, recursive: true);
        }
    }

    private static async Task<IReadOnlyList<Diagnostic>> RunGenerator(
        string code,
        Dictionary<string, string>? analyzerOptions = null,
        CancellationToken cancellationToken = default,
        string? reportFileName = null)
    {
        Assembly[] refs =
        [
            Assembly.GetAssembly(typeof(Meter))!,
            Assembly.GetAssembly(typeof(CounterAttribute))!,
            Assembly.GetAssembly(typeof(HistogramAttribute))!,
            Assembly.GetAssembly(typeof(GaugeAttribute))!,
            Assembly.GetAssembly(typeof(ILogger))!,
            Assembly.GetAssembly(typeof(LoggerMessageAttribute))!,
            Assembly.GetAssembly(typeof(Extensions.Compliance.Classification.DataClassification))!,
       ];

        var generator = reportFileName is null
            ? new MetadataReportsGenerator()
            : new MetadataReportsGenerator(reportFileName);

        var (d, _) = await RoslynTestUtils.RunGenerator(
            generator,
            refs,
            new[] { code, TestTaxonomy },
            new OptionsProvider(analyzerOptions),
            includeBaseReferences: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return d;
    }

    private sealed class Options : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _options;

        public Options(Dictionary<string, string>? analyzerOptions)
        {
            _options = analyzerOptions ?? [];
            _options.TryAdd("build_property.GenerateMetadataReport", bool.TrueString);
            _options.TryAdd("build_property.MetadataReportOutputPath", Directory.GetCurrentDirectory());
        }

        public override bool TryGetValue(string key, out string value)
            => _options.TryGetValue(key, out value!);
    }

    private sealed class OptionsProvider(Dictionary<string, string>? analyzerOptions) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => new Options(analyzerOptions);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => throw new NotSupportedException();
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => throw new NotSupportedException();
    }
}
