using System;
using System.Collections.Generic;
using System.Reflection;
using Serilog;
using Serilog.Sinks.GoogleCloudLogging;

namespace Yuu.Serilog.GoogleCloudLogging.Extensions;

public static class YuuSerilogGoogleCloudLoggingExtensions
{
    public static LoggerConfiguration UseGoogleCloudLoggingAsync(
        this LoggerConfiguration c,
        string projectId,
        Dictionary<string, string>? labels = null,
        string? serviceName = null,
        string? serviceVersion = null,
        string? googleCredentialJson = null
    )
    {
        serviceName ??= Assembly.GetEntryAssembly()!.GetName().Name!;
        serviceVersion ??= Environment.GetEnvironmentVariable("SERVICE_VERSION");

        c.WriteTo.Async(action => action.GoogleCloudLogging(
            projectId: projectId,
            labels: labels,
            serviceName: serviceName,
            serviceVersion: serviceVersion,
            googleCredentialJson: googleCredentialJson
        ));

        return c;
    }

    public static LoggerConfiguration UseGoogleCloudLogging(
        this LoggerConfiguration c,
        string projectId,
        Dictionary<string, string>? labels = null,
        string? serviceName = null,
        string? serviceVersion = null,
        string? googleCredentialJson = null
    )
    {
        serviceName ??= Assembly.GetEntryAssembly()!.GetName().Name!;
        serviceVersion ??= Environment.GetEnvironmentVariable("SERVICE_VERSION");

        c.WriteTo.GoogleCloudLogging(
            projectId: projectId,
            labels: labels,
            serviceName: serviceName,
            serviceVersion: serviceVersion,
            googleCredentialJson: googleCredentialJson
        );

        return c;
    }
}
