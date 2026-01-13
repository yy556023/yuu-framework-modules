using System;
using System.Security.Cryptography.X509Certificates;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Serilog;

namespace Yuu.Serilog.Elasticsearch.Extensions;

public static class YuuElasticsearchSerilogExtensions
{
    public static LoggerConfiguration UseElasticsearchAsync(
        this LoggerConfiguration c,
        string elasticHost,
        string elasticApiKey,
        string elasticCaPath,
        string @type,
        string @dataSet,
        string @namespace
    )
    {
        if (string.IsNullOrWhiteSpace(elasticHost))
        {
            throw new ArgumentNullException(nameof(elasticHost));
        }

        if (string.IsNullOrWhiteSpace(elasticApiKey))
        {
            throw new ArgumentNullException(nameof(elasticApiKey));
        }

        if (string.IsNullOrWhiteSpace(elasticCaPath))
        {
            throw new ArgumentNullException(nameof(elasticCaPath));
        }

        if (string.IsNullOrWhiteSpace(@type))
        {
            throw new ArgumentNullException(nameof(@type));
        }

        if (string.IsNullOrWhiteSpace(@dataSet))
        {
            throw new ArgumentNullException(nameof(@dataSet));
        }

        if (string.IsNullOrWhiteSpace(@namespace))
        {
            throw new ArgumentNullException(nameof(@namespace));
        }

        c.WriteTo.Async(action => action.Elasticsearch([new Uri(elasticHost)], opts =>
        {
            opts.DataStream = new DataStreamName(@type, @dataSet, @namespace);
            opts.BootstrapMethod = BootstrapMethod.Silent;
        }, transport =>
        {
            transport.Authentication(new ApiKey(elasticApiKey));
            transport.ServerCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) =>
            {
                using var caCert = X509CertificateLoader.LoadCertificateFromFile(elasticCaPath);
                chain.ChainPolicy.ExtraStore.Add(caCert);
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                return chain.Build(new X509Certificate2(certificate));
            });
        }));

        return c;
    }

    public static LoggerConfiguration UseElasticsearch(
        this LoggerConfiguration c,
        string elasticHost,
        string elasticApiKey,
        string elasticCaPath,
        string @type,
        string @dataSet,
        string @namespace
    )
    {
        if (string.IsNullOrWhiteSpace(elasticHost))
        {
            throw new ArgumentNullException(nameof(elasticHost));
        }

        if (string.IsNullOrWhiteSpace(elasticApiKey))
        {
            throw new ArgumentNullException(nameof(elasticApiKey));
        }

        if (string.IsNullOrWhiteSpace(elasticCaPath))
        {
            throw new ArgumentNullException(nameof(elasticCaPath));
        }

        if (string.IsNullOrWhiteSpace(@type))
        {
            throw new ArgumentNullException(nameof(@type));
        }

        if (string.IsNullOrWhiteSpace(@dataSet))
        {
            throw new ArgumentNullException(nameof(@dataSet));
        }

        if (string.IsNullOrWhiteSpace(@namespace))
        {
            throw new ArgumentNullException(nameof(@namespace));
        }

        c.WriteTo.Elasticsearch([new Uri(elasticHost)], opts =>
        {
            opts.DataStream = new DataStreamName(@type, @dataSet, @namespace);
            opts.BootstrapMethod = BootstrapMethod.Silent;
        }, transport =>
        {
            transport.Authentication(new ApiKey(elasticApiKey));
            transport.ServerCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) =>
            {
                using var caCert = X509CertificateLoader.LoadCertificateFromFile(elasticCaPath);
                chain.ChainPolicy.ExtraStore.Add(caCert);
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                return chain.Build(new X509Certificate2(certificate));
            });
        });

        return c;
    }
}
