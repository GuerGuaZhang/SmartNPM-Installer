using FluentAssertions;
using SmartNPM_Installer.Models;
using SmartNPM_Installer.Services;
using Xunit;

namespace SmartNPM_Installer.Tests
{
    public class CommandParserTests
    {
        [Fact]
        public void Parse_WithNpxCommand_ShouldReturnNpxSource()
        {
            var result = CommandParser.Parse("npx @deepseek-ai/dsh web");

            result.Should().NotBeNull();
            result!.Source.Should().Be(InstallSource.Npx);
            result.PackageName.Should().Be("@deepseek-ai/dsh");
            result.SubCommand.Should().Be("web");
        }

        [Fact]
        public void Parse_WithNpmInstallGlobalCommand_ShouldReturnNpmInstallSource()
        {
            var result = CommandParser.Parse("npm install -g typescript");

            result.Should().NotBeNull();
            result!.Source.Should().Be(InstallSource.NpmInstall);
            result.PackageName.Should().Be("typescript");
        }

        [Fact]
        public void Parse_WithPackageNameOnly_ShouldReturnRawPackageNameSource()
        {
            var result = CommandParser.Parse("typescript");

            result.Should().NotBeNull();
            result!.Source.Should().Be(InstallSource.RawPackageName);
            result.PackageName.Should().Be("typescript");
        }

        [Fact]
        public void Parse_WithScopedPackage_ShouldParseCorrectly()
        {
            var result = CommandParser.Parse("npx @angular/cli new my-app");

            result.Should().NotBeNull();
            result!.PackageName.Should().Be("@angular/cli");
            result.IsScoped.Should().BeTrue();
            result.Scope.Should().Be("angular");
            result.SubCommand.Should().Be("new my-app");
            result.BinaryName.Should().Be("cli");
        }

        [Fact]
        public void Parse_WithVersion_ShouldParseCorrectly()
        {
            var result = CommandParser.Parse("npm install -g typescript@5.0.0");

            result.Should().NotBeNull();
            result!.PackageName.Should().Be("typescript");
            result.Version.Should().Be("5.0.0");
        }

        [Fact]
        public void Parse_WithEmptyInput_ShouldReturnNull()
        {
            var result = CommandParser.Parse("");
            result.Should().BeNull();
        }

        [Fact]
        public void Parse_WithWhitespaceInput_ShouldReturnNull()
        {
            var result = CommandParser.Parse("   ");
            result.Should().BeNull();
        }

        [Fact]
        public void Parse_WithExitCommand_ShouldReturnNull()
        {
            var result = CommandParser.Parse("exit");
            result.Should().BeNull();
        }

        [Fact]
        public void Parse_WithInternalCommand_ShouldReturnNull()
        {
            var result = CommandParser.Parse("/help");
            result.Should().BeNull();
        }

        [Fact]
        public void IsValidPackageName_WithValidName_ShouldReturnTrue()
        {
            CommandParser.IsValidPackageName("typescript").Should().BeTrue();
            CommandParser.IsValidPackageName("@scope/package").Should().BeTrue();
            CommandParser.IsValidPackageName("node-pty").Should().BeTrue();
        }

        [Fact]
        public void IsValidPackageName_WithInvalidName_ShouldReturnFalse()
        {
            CommandParser.IsValidPackageName("").Should().BeFalse();
            CommandParser.IsValidPackageName(" ").Should().BeFalse();
        }

        [Fact]
        public void BuildInstallCommand_ShouldBuildCorrectCommand()
        {
            var command = new ParsedCommand
            {
                PackageName = "typescript",
                Version = "5.0.0"
            };

            var result = CommandParser.BuildInstallCommand(command);
            result.Should().Be("npm install -g typescript@5.0.0");
        }

        [Fact]
        public void BuildInstallCommand_WithScopedPackage_ShouldBuildCorrectCommand()
        {
            var command = new ParsedCommand
            {
                PackageName = "@scope/package",
                IsScoped = true,
                Scope = "scope"
            };

            var result = CommandParser.BuildInstallCommand(command);
            result.Should().Be("npm install -g @scope/package");
        }
    }
}
