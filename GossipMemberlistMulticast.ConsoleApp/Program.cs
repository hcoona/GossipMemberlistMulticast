using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog.Config;
using NLog.Extensions.Logging;
using LogManager = NLog.LogManager;

namespace GossipMemberlistMulticast.ConsoleApp
{
    internal class Program
    {
        internal static async Task Main(string[] args)
        {
            LogManager.Configuration = new XmlLoggingConfiguration("nlog.config");

            var configuration = BuildConfiguration(args);
            var services = new ServiceCollection();
            services.AddSingleton(configuration);
            services.AddLogging(b => b.AddNLog().SetMinimumLevel(LogLevel.Trace));
            services.Configure<ClusterOptions>(configuration.GetSection("Cluster"));

            var selfNodeEndpoint = configuration.GetValue<string>("Root:EndPoint");
            var selfBindingPort = int.Parse(selfNodeEndpoint.Split(new[] { ':' }, 2)[1]);
            var clientFactory = new Func<string, Gossiper.GossiperClient>(target =>
            {
                var channel = new Channel(target, ChannelCredentials.Insecure);
                return new Gossiper.GossiperClient(channel);
            });

            services.AddScoped(serviceProvider =>
            {
                var seedsEndpoint = serviceProvider.GetRequiredService<IConfiguration>()
                    .GetValue<string>("Root:Seeds")
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                return Node.Create(
                  selfNodeEndpoint,
                  seedsEndpoint,
                  () => serviceProvider.GetRequiredService<ILogger<Node>>());
            });
            services.AddScoped(serviceProvider => new GossiperImpl(
                serviceProvider.GetRequiredService<ILogger<GossiperImpl>>(),
                clientFactory,
                serviceProvider.GetRequiredService<Node>()));
            services.AddScoped(serviceProvider => new Cluster(
                serviceProvider.GetRequiredService<ILogger<Cluster>>(),
                serviceProvider.GetRequiredService<IOptionsMonitor<ClusterOptions>>(),
                serviceProvider.GetRequiredService<Node>(),
                clientFactory));

            var container = services.BuildServiceProvider();

            var logger = container.GetRequiredService<ILogger<Program>>();
            var node = container.GetRequiredService<Node>();
            node.NodeStateChanged += (_, e) =>
            {
                logger.LogWarning(
                    "Node {0} state changed from {1} to {2}",
                    e.EndPoint, e.PreviousNodeState, e.CurrentNodeState);
            };

            GrpcEnvironment.SetLogger(new GrpcLoggerAdapter(
                container.GetRequiredService<ILogger<GrpcEnvironment>>(),
                container.GetRequiredService<ILoggerFactory>()));

            var server = new Server
            {
                Services = { Gossiper.BindService(container.GetRequiredService<GossiperImpl>()) },
                Ports = { new ServerPort(selfNodeEndpoint.Split(':')[0], selfBindingPort, ServerCredentials.Insecure) }
            };
            server.Start();

            var cluster = container.GetRequiredService<Cluster>();
            await cluster.StartAsync();

            logger.LogInformation("Server is running");
            Console.ReadKey();

            Task.WaitAll(
                server.ShutdownAsync(),
                cluster.StopAsync());
        }

        private static IConfiguration BuildConfiguration(string[] args)
        {
            return new ConfigurationBuilder()
                .AddIniFile("config.ini")
                .AddCommandLine(args)
                .Build();
        }
    }
}
