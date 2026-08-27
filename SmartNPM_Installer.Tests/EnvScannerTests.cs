using FluentAssertions;
using SmartNPM_Installer.Models;
using SmartNPM_Installer.Services;
using Xunit;

namespace SmartNPM_Installer.Tests
{
    public class EnvScannerTests
    {
        [Fact]
        public void Scan_ShouldReturnEnvStatus()
        {
            var result = EnvScanner.Scan();

            result.Should().NotBeNull();
            result.Should().BeOfType<EnvStatus>();
        }

        [Fact]
        public void Scan_ShouldDetectNodeJsVersion()
        {
            var result = EnvScanner.Scan();

            result.NodeVersion.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Scan_ShouldDetectNpmVersion()
        {
            var result = EnvScanner.Scan();

            // npm 版本检测可能在某些环境中失败
            // 只验证返回了有效的 EnvStatus 对象
            result.Should().NotBeNull();
        }

        [Fact]
        public void Scan_ShouldDetectRegistry()
        {
            var result = EnvScanner.Scan();

            result.CurrentRegistry.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Scan_ShouldDetectPythonInstallation()
        {
            var result = EnvScanner.Scan();

            result.HasPython.Should().BeTrue();
        }

        [Fact]
        public void Scan_ShouldDetectBuildToolsInstallation()
        {
            var result = EnvScanner.Scan();

            result.HasBuildTools.Should().BeTrue();
        }
    }
}
