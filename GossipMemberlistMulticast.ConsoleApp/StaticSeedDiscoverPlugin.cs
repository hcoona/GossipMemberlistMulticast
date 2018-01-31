using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GossipMemberlistMulticast.ConsoleApp
{
    public class StaticSeedDiscoverPlugin : ISeedDiscoverPlugin
    {
        private readonly IEnumerable<string> seedsEndpoint;

        public StaticSeedDiscoverPlugin(IEnumerable<string> seedsEndpoint)
        {
            this.seedsEndpoint = seedsEndpoint;
        }

        public Task<IEnumerable<string>> GetSeedsEndpointAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(seedsEndpoint);
    }
}
