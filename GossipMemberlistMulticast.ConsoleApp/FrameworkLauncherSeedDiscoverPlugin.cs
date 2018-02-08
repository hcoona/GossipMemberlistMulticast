using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GossipMemberlistMulticast.ConsoleApp
{
    public class FrameworkLauncherSeedDiscoverPlugin : ISeedDiscoverPlugin
    {
        private readonly ILogger logger;

        public FrameworkLauncherSeedDiscoverPlugin(
            IConfiguration configuration,
            ILogger<FrameworkLauncherSeedDiscoverPlugin> logger)
        {
            this.logger = logger;

            BindingPort = int.Parse(configuration.GetValue<string>("Root:EndPoint").Split(new[] { ':' }, 2)[1]);
            LauncherTrackingUri = string.Format(
                "{0}/v1/Frameworks/{1}",
                Environment.GetEnvironmentVariable("LAUNCHER_ADDRESS"),
                Environment.GetEnvironmentVariable("FRAMEWORK_NAME"));

            logger.LogInformation("BindingPort = {0}", BindingPort);
            logger.LogInformation("LauncherTrackingUri = {0}", LauncherTrackingUri);
        }

        private int BindingPort { get; }

        private string LauncherTrackingUri { get; }

        public async Task<IEnumerable<string>> GetSeedsEndpointAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string frameworkStatusResponseContent;
                using (var client = new HttpClient())
                {
                    var responseMsg = await client.GetAsync(LauncherTrackingUri, cancellationToken);
                    responseMsg.EnsureSuccessStatusCode();
                    frameworkStatusResponseContent = await responseMsg.Content.ReadAsStringAsync();
                }

                logger.LogTrace("Parsing IP addresses from framework status");
                var ipAddresses = ParseIpAddressesFromFrameworkStatus(frameworkStatusResponseContent);

                return ipAddresses.Select(ip => $"{ip}:{BindingPort}").ToArray();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get seeds endpoint: {0}", ex.Message);
                return Enumerable.Empty<string>();
            }
        }

        private static IList<string> ParseIpAddressesFromFrameworkStatus(string content)
        {
            dynamic contentObj = JsonConvert.DeserializeObject(content);
            JArray taskStatusArray = contentObj.AggregatedTaskRoleStatuses.SaaS.TaskStatuses.TaskStatusArray;
            return taskStatusArray
                .Select(item => item["ContainerIPAddress"].ToString())
                .Where(ipString => IPAddress.TryParse(ipString, out var ignored))
                .ToArray();
        }
    }
}
