using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace GossipMemberlistMulticast.Tests
{
    public class UnitTest1
    {
        private readonly ITestOutputHelper testOutputHelper;
        private readonly IServiceProvider container;

        public UnitTest1(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddProvider(new XUnitOutputLoggerProvider(testOutputHelper)));
            this.container = services.BuildServiceProvider();
        }

        [Fact]
        public async Task Test1()
        {
            var client = container.GetRequiredService<Gossiper.GossiperClient>();
            var response = await client.ForwardAsync(new ForwardRequest
            {
                TargetEndpoint = "test",
                TargetMethod = "Ping1",
                RequestMessage = new RequestMessage
                {
                    NodeId = "test_node1",
                    Ping1Request = new Ping1Request()
                }
            });

            switch (response.ResponseCase)
            {
                case ForwardResponse.ResponseOneofCase.ResponseMessage:
                    Assert.Equal(ResponseMessage.ResponseOneofCase.Ping1Response, response.ResponseMessage.ResponseCase);
                    break;
                case ForwardResponse.ResponseOneofCase.ErrorMessage:
                    Assert.True(false, response.ErrorMessage);
                    break;
                case ForwardResponse.ResponseOneofCase.None:
                    Assert.True(false, "Response type is none");
                    break;
            }
        }
    }
}
