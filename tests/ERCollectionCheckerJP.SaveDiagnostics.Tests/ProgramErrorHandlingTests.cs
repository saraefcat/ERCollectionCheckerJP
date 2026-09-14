using System.Text;

namespace ERCollectionCheckerJP.SaveDiagnostics.Tests;

public sealed class ProgramErrorHandlingTests
{
    [Fact]
    public async Task ExecuteSafelyAsync_UsesUsageExitCodeForArgumentErrors()
    {
        using var error = new StringWriter();

        var exitCode = await Program.ExecuteSafelyAsync(
            () => Task.FromException<int>(new ArgumentException("bad option")),
            error);

        Assert.Equal(2, exitCode);
        Assert.Contains("bad option", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteSafelyAsync_ConvertsUnexpectedExceptionsToFailureExitCode()
    {
        using var error = new StringWriter();

        var exitCode = await Program.ExecuteSafelyAsync(
            () => Task.FromException<int>(new InvalidOperationException("unexpected failure")),
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains(
            "Unexpected error (InvalidOperationException): unexpected failure",
            error.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteSafelyAsync_DoesNotRethrowWhenErrorConsoleIsUnavailable()
    {
        var exitCode = await Program.ExecuteSafelyAsync(
            () => Task.FromException<int>(new IOException("input failure")),
            new ThrowingTextWriter());

        Assert.Equal(1, exitCode);
    }

    private sealed class ThrowingTextWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value) =>
            throw new IOException("console unavailable");
    }
}
