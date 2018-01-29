using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace GossipMemberlistMulticast.Tests
{
    public class UnitTest1
    {
        private readonly ITestOutputHelper testOutputHelper;

        public UnitTest1(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task Test1()
        {
            var client = new GossiperImpl(
                new Logger<GossiperImpl>(new LoggerFactory(new[] { new XUnitOutputLoggerProvider(testOutputHelper) })),
                endpoint => new Gossiper.GossiperClient(new Channel(endpoint, ChannelCredentials.Insecure)),
                null);
            var response = await client.Forward(new ForwardRequest
            {
                TargetEndpoint = "test",
                TargetMethod = "Ping1",
                Ping1Request = new Ping1Request()
            }, default);

            switch (response.ResponseCase)
            {
                case ForwardResponse.ResponseOneofCase.ErrorMessage:
                    Assert.True(false, response.ErrorMessage);
                    break;
                case ForwardResponse.ResponseOneofCase.None:
                    Assert.True(false, "Response type is none");
                    break;
                case ForwardResponse.ResponseOneofCase.Ping1Response:
                    break;
                default:
                    Assert.True(false, "Mismatch response type");
                    break;
            }
        }
    }
}
