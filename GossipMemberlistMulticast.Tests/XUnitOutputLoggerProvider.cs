using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace GossipMemberlistMulticast.Tests
{
    public class XUnitOutputLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper testOutputHelper;

        public XUnitOutputLoggerProvider(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XUnitOutputLogger(testOutputHelper);
        }

        public void Dispose()
        {
        }
    }
}
