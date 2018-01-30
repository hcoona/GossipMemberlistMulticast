using System;
using Microsoft.Extensions.Logging;
using IGrpcLogger = Grpc.Core.Logging.ILogger;

namespace GossipMemberlistMulticast.ConsoleApp
{
    public class GrpcLoggerAdapter : IGrpcLogger
    {
        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;

        public GrpcLoggerAdapter(ILogger logger, ILoggerFactory loggerFactory)
        {
            this.logger = logger;
            this.loggerFactory = loggerFactory;
        }

        public void Debug(string message) => logger.LogDebug(message);

        public void Debug(string format, params object[] formatArgs) => logger.LogDebug(format, formatArgs);

        public void Error(string message) => logger.LogError(message);

        public void Error(string format, params object[] formatArgs) => logger.LogError(format, formatArgs);

        public void Error(Exception exception, string message) => logger.LogError(default, exception, message);

        public IGrpcLogger ForType<T>() => new GrpcLoggerAdapter(loggerFactory.CreateLogger<T>(), loggerFactory);

        public void Info(string message) => logger.LogInformation(message);

        public void Info(string format, params object[] formatArgs) => logger.LogInformation(format, formatArgs);

        public void Warning(string message) => logger.LogWarning(message);

        public void Warning(string format, params object[] formatArgs) => logger.LogWarning(format, formatArgs);

        public void Warning(Exception exception, string message) => logger.LogWarning(default, exception, message);
    }
}
