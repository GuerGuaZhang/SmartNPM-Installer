using FluentAssertions;
using SmartNPM_Installer.Services;
using SmartNPM_Installer.Utils;
using Xunit;

namespace SmartNPM_Installer.Tests
{
    public class ConfigManagerTests
    {
        private readonly ConfigManager _configManager;

        public ConfigManagerTests()
        {
            _configManager = new ConfigManager(new Logger());
        }

        [Fact]
        public void CurrentConfig_ShouldNotBeNull()
        {
            _configManager.CurrentConfig.Should().NotBeNull();
        }

        [Fact]
        public void CurrentConfig_ShouldHaveDefaultRegistry()
        {
            _configManager.CurrentConfig.Registry.Should().Be("https://registry.npmmirror.com");
        }

        [Fact]
        public void CurrentConfig_ShouldHaveDefaultWhitelist()
        {
            _configManager.CurrentConfig.AllowScriptsWhitelist.Should().Contain("node-pty");
            _configManager.CurrentConfig.AllowScriptsWhitelist.Should().Contain("sharp");
            _configManager.CurrentConfig.AllowScriptsWhitelist.Should().Contain("bcrypt");
        }

        [Fact]
        public void CurrentConfig_ShouldHaveDefaultMaxRetryCount()
        {
            _configManager.CurrentConfig.MaxRetryCount.Should().Be(3);
        }

        [Fact]
        public void CurrentConfig_ShouldHaveDefaultAutoInstallBuildTools()
        {
            _configManager.CurrentConfig.AutoInstallBuildTools.Should().BeTrue();
        }
    }
}
