using FluentAssertions;
using SmartNPM_Installer.Utils;
using Xunit;

namespace SmartNPM_Installer.Tests
{
    public class LoggerTests
    {
        [Fact]
        public void Logger_ShouldInitializeWithDefaultDirectory()
        {
            var logger = new Logger();
            logger.Should().NotBeNull();
        }

        [Fact]
        public void Logger_ShouldInitializeWithCustomDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "sni-test-logs-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                var logger = new Logger(tempDir);
                logger.Should().NotBeNull();
                Directory.Exists(tempDir).Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetCurrentLogFile_ShouldReturnValidPath()
        {
            var logger = new Logger();
            var logFile = logger.GetCurrentLogFile();

            logFile.Should().NotBeNullOrEmpty();
            logFile.Should().EndWith(".log");
        }

        [Fact]
        public void LogDebug_ShouldNotThrow()
        {
            var logger = new Logger();
            var act = () => logger.LogDebug("Test debug message");
            act.Should().NotThrow();
        }

        [Fact]
        public void LogInfo_ShouldNotThrow()
        {
            var logger = new Logger();
            var act = () => logger.LogInfo("Test info message");
            act.Should().NotThrow();
        }

        [Fact]
        public void LogWarning_ShouldNotThrow()
        {
            var logger = new Logger();
            var act = () => logger.LogWarning("Test warning message");
            act.Should().NotThrow();
        }

        [Fact]
        public void LogError_ShouldNotThrow()
        {
            var logger = new Logger();
            var act = () => logger.LogError("Test error message");
            act.Should().NotThrow();
        }

        [Fact]
        public void LogFatal_ShouldNotThrow()
        {
            var logger = new Logger();
            var act = () => logger.LogFatal("Test fatal message");
            act.Should().NotThrow();
        }
    }
}
