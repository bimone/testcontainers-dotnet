namespace DotNet.Testcontainers.Containers
{
  using System.Collections.Generic;
  using System.Globalization;
  using System.Linq;
  using System.Net;
  using System.Threading.Tasks;
  using Docker.DotNet.Models;
  using DotNet.Testcontainers.Builders;
  using DotNet.Testcontainers.Configurations;
  using JetBrains.Annotations;
  using Microsoft.Extensions.Logging;
  using Renci.SshNet;

  /// <inheritdoc cref="DockerContainer" />
  [PublicAPI]
  internal sealed class PortForwardingContainer : DockerContainer
  {
    private readonly PortForwardingConfiguration _configuration;

    static PortForwardingContainer()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PortForwardingContainer" /> class.
    /// </summary>
    /// <param name="configuration">The container configuration.</param>
    /// <param name="logger">The logger.</param>
    private PortForwardingContainer(PortForwardingConfiguration configuration, ILogger logger)
      : base(configuration, logger)
    {
      _configuration = configuration;
    }

    /// <summary>
    /// Gets the <see cref="PortForwardingContainer" /> instance.
    /// </summary>
    public static PortForwardingContainer Instance { get; }
      = new PortForwardingBuilder().Build();

    /// <summary>
    /// Exposes the host ports using SSH port forwarding.
    /// </summary>
    /// <param name="ports">The host ports to forward.</param>
    /// <returns>A task that completes when the host ports are forwarded.</returns>
    public Task ExposeHostPortsAsync(params ushort[] ports)
    {
      var sshClient = new SshClient(Hostname, GetMappedPublicPort(PortForwardingBuilder.SshdPort), _configuration.Username, _configuration.Password);
      sshClient.Connect();

      foreach (var forwardedPort in ports.Select(port => new ForwardedPortRemote(IPAddress.Loopback, port, IPAddress.Loopback, port)))
      {
        sshClient.AddForwardedPort(forwardedPort);
        forwardedPort.Start();
      }

      return Task.CompletedTask;
    }

    /// <inheritdoc cref="ContainerBuilder{TBuilderEntity, TContainerEntity, TConfigurationEntity}" />
    [PublicAPI]
    private sealed class PortForwardingBuilder : ContainerBuilder<PortForwardingBuilder, PortForwardingContainer, PortForwardingConfiguration>
    {
      public const string SshdImage = "testcontainers/sshd:1.1.0";

      public const ushort SshdPort = 22;

      private const string Command = "echo \"$USERNAME:$PASSWORD\" | chpasswd && /usr/sbin/sshd -D"
                                     + " -o AddressFamily=inet"
                                     + " -o AllowAgentForwarding=yes"
                                     + " -o AllowTcpForwarding=yes"
                                     + " -o GatewayPorts=yes"
                                     + " -o HostkeyAlgorithms=+ssh-rsa"
                                     + " -o KexAlgorithms=+diffie-hellman-group1-sha1"
                                     + " -o PermitRootLogin=yes";

      /// <summary>
      /// Initializes a new instance of the <see cref="PortForwardingConfiguration" /> class.
      /// </summary>
      public PortForwardingBuilder()
        : this(new PortForwardingConfiguration())
      {
        DockerResourceConfiguration = Init().DockerResourceConfiguration;
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="PortForwardingBuilder" /> class.
      /// </summary>
      /// <param name="resourceConfiguration">The Docker resource configuration.</param>
      private PortForwardingBuilder(PortForwardingConfiguration resourceConfiguration)
        : base(resourceConfiguration)
      {
        DockerResourceConfiguration = resourceConfiguration;
      }

      /// <inheritdoc />
      protected override PortForwardingConfiguration DockerResourceConfiguration { get; }

      /// <summary>
      /// Exposes the host ports using SSH port forwarding.
      /// </summary>
      /// <param name="ports">The host ports to forward.</param>
      /// <returns>A configured instance of <see cref="PortForwardingBuilder" />.</returns>
      public PortForwardingBuilder WithExposedHostPort(params int[] ports)
      {
        return WithExposedHostPort(ports.Select(port => port.ToString(CultureInfo.InvariantCulture)).ToArray());
      }

      /// <summary>
      /// Exposes the host ports to containers in the same network.
      /// </summary>
      /// <param name="exposedHostPorts">The host ports.</param>
      /// <returns>A configured instance of <see cref="PortForwardingBuilder" />.</returns>
      public PortForwardingBuilder WithExposedHostPort(params string[] exposedHostPorts)
      {
        return Merge(DockerResourceConfiguration, new PortForwardingConfiguration(exposedHostPorts: exposedHostPorts));
      }

      /// <inheritdoc />
      public override PortForwardingContainer Build()
      {
        Validate();
        return new PortForwardingContainer(DockerResourceConfiguration, TestcontainersSettings.Logger);
      }

      /// <inheritdoc />
      protected override PortForwardingBuilder Init()
      {
        return base.Init()
          .WithImage(SshdImage)
          .WithPortBinding(SshdPort, true)
          .WithEntrypoint("/bin/sh", "-c")
          .WithCommand(Command)
          .WithUsername("root")
          .WithPassword("root")
          .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(SshdPort));
      }

      /// <inheritdoc />
      protected override PortForwardingBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
      {
        return Merge(DockerResourceConfiguration, new PortForwardingConfiguration(resourceConfiguration));
      }

      /// <inheritdoc />
      protected override PortForwardingBuilder Clone(IContainerConfiguration resourceConfiguration)
      {
        return Merge(DockerResourceConfiguration, new PortForwardingConfiguration(resourceConfiguration));
      }

      /// <inheritdoc />
      protected override PortForwardingBuilder Merge(PortForwardingConfiguration oldValue, PortForwardingConfiguration newValue)
      {
        return new PortForwardingBuilder(new PortForwardingConfiguration(oldValue, newValue));
      }

      /// <summary>
      /// Sets the OpenSSH daemon username.
      /// </summary>
      /// <param name="username">The OpenSSH daemon username.</param>
      /// <returns>A configured instance of <see cref="PortForwardingBuilder" />.</returns>
      private PortForwardingBuilder WithUsername(string username)
      {
        return Merge(DockerResourceConfiguration, new PortForwardingConfiguration(username: username))
          .WithEnvironment("USERNAME", username);
      }

      /// <summary>
      /// Sets the OpenSSH daemon password.
      /// </summary>
      /// <param name="password">The OpenSSH daemon password.</param>
      /// <returns>A configured instance of <see cref="PortForwardingBuilder" />.</returns>
      private PortForwardingBuilder WithPassword(string password)
      {
        return Merge(DockerResourceConfiguration, new PortForwardingConfiguration(password: password))
          .WithEnvironment("PASSWORD", password);
      }
    }

    /// <inheritdoc cref="ContainerConfiguration" />
    [PublicAPI]
    public sealed class PortForwardingConfiguration : ContainerConfiguration
    {
      /// <summary>
      /// Initializes a new instance of the <see cref="PortForwardingConfiguration" /> class.
      /// </summary>
      /// <param name="username">The OpenSSH daemon username.</param>
      /// <param name="password">The OpenSSH daemon password.</param>
      /// <param name="exposedHostPorts">A list of exposed host ports.</param>
      public PortForwardingConfiguration(
        string username = null,
        string password = null,
        IEnumerable<string> exposedHostPorts = null)
      {
        Username = username;
        Password = password;
        ExposedHostPorts = exposedHostPorts;
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="PortForwardingConfiguration" /> class.
      /// </summary>
      /// <param name="resourceConfiguration">The Docker resource configuration.</param>
      public PortForwardingConfiguration(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
        : base(resourceConfiguration)
      {
        // Passes the configuration upwards to the base implementations to create an updated immutable copy.
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="PortForwardingConfiguration" /> class.
      /// </summary>
      /// <param name="resourceConfiguration">The Docker resource configuration.</param>
      public PortForwardingConfiguration(IContainerConfiguration resourceConfiguration)
        : base(resourceConfiguration)
      {
        // Passes the configuration upwards to the base implementations to create an updated immutable copy.
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="PortForwardingConfiguration" /> class.
      /// </summary>
      /// <param name="resourceConfiguration">The Docker resource configuration.</param>
      public PortForwardingConfiguration(PortForwardingConfiguration resourceConfiguration)
        : this(new PortForwardingConfiguration(), resourceConfiguration)
      {
        // Passes the configuration upwards to the base implementations to create an updated immutable copy.
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="PortForwardingConfiguration" /> class.
      /// </summary>
      /// <param name="oldValue">The old Docker resource configuration.</param>
      /// <param name="newValue">The new Docker resource configuration.</param>
      public PortForwardingConfiguration(PortForwardingConfiguration oldValue, PortForwardingConfiguration newValue)
        : base(oldValue, newValue)
      {
        Username = BuildConfiguration.Combine(oldValue.Username, newValue.Username);
        Password = BuildConfiguration.Combine(oldValue.Password, newValue.Password);
        ExposedHostPorts = BuildConfiguration.Combine(oldValue.ExposedHostPorts, newValue.ExposedHostPorts);
      }

      /// <summary>
      /// Gets the OpenSSH daemon username.
      /// </summary>
      public string Username { get; }

      /// <summary>
      /// Gets the OpenSSH daemon password.
      /// </summary>
      public string Password { get; }

      /// <summary>
      /// Gets a list of exposed host ports.
      /// </summary>
      public IEnumerable<string> ExposedHostPorts { get; }
    }
  }
}
