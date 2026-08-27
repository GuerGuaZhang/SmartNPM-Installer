using FluentAssertions;
using SmartNPM_Installer.Models;
using SmartNPM_Installer.Services;
using SmartNPM_Installer.Utils;
using Xunit;

namespace SmartNPM_Installer.Tests
{
    public class ErrorHealerTests
    {
        private readonly ErrorHealer _errorHealer;

        public ErrorHealerTests()
        {
            _errorHealer = new ErrorHealer(new ConfigManager(new Logger()), new Logger());
        }

        [Fact]
        public void Analyze_WithAllowScriptsBlocked_ShouldMatchAllowScriptsError()
        {
            var stderr = "npm warn allowScripts 2 packages have install scripts not yet covered by allowScripts:";
            stderr += "\nnpm warn allowScripts Run `npm install -g --allow-scripts=node-pty,koffi` to run install scripts";

            var result = _errorHealer.Analyze(stderr);

            result.Should().NotBeNull();
            result!.Matched.Should().BeTrue();
            result.ErrorType.Should().Be(ErrorType.AllowScriptsBlocked);
        }

        [Fact]
        public void Analyze_WithNetworkError_ShouldMatchNetworkError()
        {
            var stderr = "npm ERR! code ENETUNREACH\nnpm ERR! network request to https://registry.npmjs.org/ failed";

            var result = _errorHealer.Analyze(stderr);

            result.Should().NotBeNull();
            result!.Matched.Should().BeTrue();
            result.ErrorType.Should().Be(ErrorType.NetworkError);
        }

        [Fact]
        public void Analyze_WithPackageNotFound_ShouldMatchNotFoundError()
        {
            var stderr = "npm ERR! code E404\nnpm ERR! 404 Not Found";

            var result = _errorHealer.Analyze(stderr);

            result.Should().NotBeNull();
            result!.Matched.Should().BeTrue();
            result.ErrorType.Should().Be(ErrorType.PackageNotFound);
        }

        [Fact]
        public void Analyze_WithEmptyStderr_ShouldReturnNull()
        {
            var result = _errorHealer.Analyze("");
            result.Should().BeNull();
        }

        [Fact]
        public void Analyze_WithNullStderr_ShouldReturnNull()
        {
            var result = _errorHealer.Analyze(null!);
            result.Should().BeNull();
        }

        [Fact]
        public void Analyze_WithUnknownError_ShouldReturnUnknownError()
        {
            var stderr = "npm ERR! some completely unknown error";

            var result = _errorHealer.Analyze(stderr);

            result.Should().NotBeNull();
            result!.Matched.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.Unknown);
        }
    }
}
