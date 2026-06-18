using Microsoft.Extensions.Logging;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Aevor.Application.Interfaces;
using Aevor.Application.Models;
using Aevor.Core.Exceptions;
using Aevor.Core.Models;
using Aevor.Infrastructure.Services;

namespace Aevor.Tests.Services;

public class SecurityScannerTests
{
    private static readonly byte[] SqliteHeader = new byte[] { 0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x66, 0x6F, 0x72, 0x6D, 0x61, 0x74, 0x20, 0x33, 0x00 };
    private static readonly byte[] EmptyHeader = new byte[16];

    private SecurityScanner CreateScanner(
        IFileSystem fileSystem,
        SecurityScannerOptions? options = null,
        IBraveInstallationService? installationService = null)
    {
        var safetyEvaluator = new ExportSafetyEvaluator();
        options ??= new SecurityScannerOptions();
        installationService ??= Substitute.For<IBraveInstallationService>();
        var logger = Substitute.For<ILogger<SecurityScanner>>();
        return new SecurityScanner(fileSystem, safetyEvaluator, options, installationService, logger);
    }

    [Fact]
    public async Task ScanAsync_ShouldThrowSecurityScanException_WhenProfileDirectoryDoesNotExist()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(false);

        var scanner = CreateScanner(fileSystem);

        var act = () => scanner.ScanAsync(profile);

        await act.Should().ThrowAsync<SecurityScanException>();
    }

    [Fact]
    public async Task ScanAsync_ShouldReturnEmptyFindingsAndLowRisk_WhenProfileIsEmpty()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(true);

        var scanner = CreateScanner(fileSystem);

        var result = await scanner.ScanAsync(profile);

        result.Should().NotBeNull();
        result.Findings.Should().BeEmpty();
        result.RiskScore.Should().Be(0);
        result.RiskLevel.Should().Be(RiskLevel.Low);
        result.ExportSafe.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAsync_ShouldDetectPasswordsAndSetUnsafe_WhenPasswordsExist()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(true);

        var loginDataPath = @"C:\Brave\Default\Login Data";
        fileSystem.FileExists(loginDataPath).Returns(true);
        fileSystem.ReadBytesAsync(loginDataPath, 16).Returns(SqliteHeader);

        var scanner = CreateScanner(fileSystem);

        var result = await scanner.ScanAsync(profile);

        result.HasPasswords.Should().BeTrue();
        result.ExportSafe.Should().BeFalse();
        result.Findings.Should().ContainSingle(f => f.Category == "Credentials");
    }

    [Fact]
    public async Task ScanAsync_ShouldDetectCookiesAndSetUnsafe_WhenCookiesExist()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(true);

        var cookiesPath = @"C:\Brave\Default\Network\Cookies";
        fileSystem.FileExists(cookiesPath).Returns(true);
        fileSystem.ReadBytesAsync(cookiesPath, 16).Returns(SqliteHeader);

        var scanner = CreateScanner(fileSystem);

        var result = await scanner.ScanAsync(profile);

        result.HasCookies.Should().BeTrue();
        result.ExportSafe.Should().BeFalse();
        result.Findings.Should().ContainSingle(f => f.Category == "Cookies");
    }

    [Fact]
    public async Task ScanAsync_ShouldDetectWalletAndSetUnsafe_WhenWalletExists()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(true);

        var walletPath = @"C:\Brave\Default\BraveWallet";
        fileSystem.DirectoryExists(walletPath).Returns(true);

        var scanner = CreateScanner(fileSystem);

        var result = await scanner.ScanAsync(profile);

        result.HasWalletData.Should().BeTrue();
        result.ExportSafe.Should().BeFalse();
        result.Findings.Should().ContainSingle(f => f.Category == "Cryptocurrency Wallet");
    }

    [Fact]
    public async Task ScanAsync_ShouldDetectSessions_WhenSessionStorageFoldersExist()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(true);

        var sessionPath = @"C:\Brave\Default\Sessions";
        fileSystem.DirectoryExists(sessionPath).Returns(true);

        var scanner = CreateScanner(fileSystem);

        var result = await scanner.ScanAsync(profile);

        result.HasSessions.Should().BeTrue();
        result.ExportSafe.Should().BeTrue();
        result.Findings.Should().ContainSingle(f => f.Category == "Session & Local Storage Data");
    }

    [Fact]
    public async Task ScanAsync_ShouldIgnoreCorruptedSqliteHeader_WhenReadingDummyFiles()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(true);

        var loginDataPath = @"C:\Brave\Default\Login Data";
        fileSystem.FileExists(loginDataPath).Returns(true);
        fileSystem.ReadBytesAsync(loginDataPath, 16).Returns(EmptyHeader);

        var scanner = CreateScanner(fileSystem);

        var result = await scanner.ScanAsync(profile);

        result.HasPasswords.Should().BeFalse();
        result.ExportSafe.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAsync_ShouldCalculateCorrectRiskScoreAndLevel_WhenMultipleFindingsExist()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(true);

        var webDataPath = @"C:\Brave\Default\Web Data";
        fileSystem.FileExists(webDataPath).Returns(true);
        fileSystem.ReadBytesAsync(webDataPath, 16).Returns(SqliteHeader);

        var sessionPath = @"C:\Brave\Default\Sessions";
        fileSystem.DirectoryExists(sessionPath).Returns(true);

        var scanner = CreateScanner(fileSystem);

        var result = await scanner.ScanAsync(profile);

        // Autofill (3) + Sessions (2) = 5
        // Max weight is 60 (with BrowserRunningWeight = 30)
        // 5 / 60 * 100 = 8.33% -> rounds to 8
        result.RiskScore.Should().Be(8);
        result.RiskLevel.Should().Be(RiskLevel.Low);
        result.ExportSafe.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAsync_ShouldDetectBrowserRunningAndIncreaseRisk_WhenBraveIsRunning()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var installationService = Substitute.For<IBraveInstallationService>();
        installationService.IsBraveRunning().Returns(true);

        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(true);

        var scanner = CreateScanner(fileSystem, installationService: installationService);

        var result = await scanner.ScanAsync(profile);

        result.Findings.Should().ContainSingle(f => f.Name == "Brave Browser is Running");
        // Running weight is 30. Max weight is 60.
        // 30 / 60 * 100 = 50%
        result.RiskScore.Should().Be(50);
        result.RiskLevel.Should().Be(RiskLevel.Medium);
    }
}
