namespace Testcontainers.Azurite;

/// <inheritdoc cref="DockerContainer" />
[PublicAPI]
public sealed class AzuriteContainer : DockerContainer
{
    public const string AccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    public string ConnectionString => BuildConnectionString();

    /// <summary>
    /// Initializes a new instance of the <see cref="AzuriteContainer" /> class.
    /// </summary>
    /// <param name="configuration">The container configuration.</param>
    /// <param name="logger">The logger.</param>
    public AzuriteContainer(AzuriteConfiguration configuration, ILogger logger)
        : base(configuration, logger)
    {
    }

    public string BuildConnectionString() => BuildConnectionString(GetMappedPublicPort(10000));

    public static string BuildConnectionString(int blobPublicPort)
    {
        return $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey={AccountKey};" +
               $"BlobEndpoint=http://127.0.0.1:{blobPublicPort}/devstoreaccount1";
    }
}