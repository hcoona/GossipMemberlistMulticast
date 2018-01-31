using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GossipMemberlistMulticast.ConsoleApp
{
    public interface ISeedDiscoverPlugin
    {
        Task<IEnumerable<string>> GetSeedsEndpointAsync(CancellationToken cancellationToken = default);
    }
}
