using FluentAssertions;
using OxyKit.Flow.Outputs;

namespace OxyKit.Flow.Tests;

public sealed class FileOutputProviderTests
{
    [Fact]
    public async Task SendAsync_WritesViaPartialThenFinalPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"oxykit-flow-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var destination = Path.Combine(dir, "report.html");
        var partial = destination + ".partial";

        try
        {
            var provider = new FileOutputProvider();
            var artifact = new Artifact
            {
                Content = "<html/>"u8.ToArray(),
                ContentType = "text/html",
                FileName = "report.html",
            };

            await provider.SendAsync(
                artifact,
                new DeliveryRequest { Provider = "file", Destination = destination },
                new DeliveryContext { JobId = Guid.NewGuid(), Ordinal = 0 },
                TestContext.Current.CancellationToken
            );

            File.Exists(destination).Should().BeTrue();
            File.Exists(partial).Should().BeFalse();
            (await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken))
                .Should()
                .Equal(artifact.Content);
        }
        finally
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }

    [Fact]
    public async Task SendAsync_OverwriteFinal_DoesNotLeaveSiblingFinal()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"oxykit-flow-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var destination = Path.Combine(dir, "report.html");

        try
        {
            await File.WriteAllTextAsync(
                destination,
                "old",
                TestContext.Current.CancellationToken
            );

            var provider = new FileOutputProvider();
            await provider.SendAsync(
                new Artifact
                {
                    Content = "new"u8.ToArray(),
                    ContentType = "text/plain",
                    FileName = "report.html",
                },
                new DeliveryRequest { Provider = "file", Destination = destination },
                new DeliveryContext { JobId = Guid.NewGuid(), Ordinal = 0 },
                TestContext.Current.CancellationToken
            );

            Directory.GetFiles(dir).Should().ContainSingle().Which.Should().Be(destination);
            (await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken))
                .Should()
                .Be("new");
        }
        finally
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }
}
