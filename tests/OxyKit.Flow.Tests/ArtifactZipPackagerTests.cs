using System.IO.Compression;
using System.Text;
using FluentAssertions;
using OxyKit.Flow.Abstractions;
using OxyKit.Flow.Outputs;

namespace OxyKit.Flow.Tests;

public sealed class ArtifactZipPackagerTests
{
    [Fact]
    public void PackIfRequested_WhenZipFalse_ReturnsSameArtifact()
    {
        var artifact = SampleArtifact();
        var request = new DeliveryRequest
        {
            Provider = "file",
            Destination = "output/out.html",
            Zip = false,
        };

        var result = ArtifactZipPackager.PackIfRequested(artifact, request);

        result.Should().BeSameAs(artifact);
    }

    [Fact]
    public void PackIfRequested_WhenZipTrue_WrapsSingleEntry()
    {
        var artifact = SampleArtifact();
        var request = new DeliveryRequest
        {
            Provider = "file",
            Destination = "output/out.html",
            Zip = true,
        };

        var result = ArtifactZipPackager.PackIfRequested(artifact, request);

        result.Should().NotBeSameAs(artifact);
        result.ContentType.Should().Be(ArtifactZipPackager.ZipContentType);
        result.FileName.Should().Be("out.zip");

        using var zip = new ZipArchive(new MemoryStream(result.Content), ZipArchiveMode.Read);
        zip.Entries.Should().ContainSingle();
        var entry = zip.Entries[0];
        entry.FullName.Should().Be("out.html");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        reader.ReadToEnd().Should().Be("<html/>");
    }

    [Fact]
    public void PackIfRequested_WhenNoFileName_GuessesEntryFromContentType()
    {
        var artifact = new Artifact
        {
            Content = [1, 2, 3],
            ContentType = "application/pdf",
            FileName = null,
        };
        var request = new DeliveryRequest
        {
            Provider = "mail",
            Destination = "a@b.c",
            Zip = true,
        };

        var result = ArtifactZipPackager.PackIfRequested(artifact, request);

        result.FileName.Should().Be("report.zip");
        using var zip = new ZipArchive(new MemoryStream(result.Content), ZipArchiveMode.Read);
        zip.Entries.Should().ContainSingle().Which.FullName.Should().Be("report.pdf");
    }

    [Fact]
    public void AdjustDestination_FilePath_RewritesExtensionToZip()
    {
        var request = new DeliveryRequest
        {
            Provider = "file",
            Destination = "output/report.pdf",
            Zip = true,
        };

        var adjusted = ArtifactZipPackager.AdjustDestination(request);

        adjusted.Destination.Should().Be("output/report.zip");
        adjusted.Zip.Should().BeTrue();
    }

    [Fact]
    public void AdjustDestination_AlreadyZip_Unchanged()
    {
        var request = new DeliveryRequest
        {
            Provider = "file",
            Destination = "output/report.zip",
            Zip = true,
        };

        ArtifactZipPackager.AdjustDestination(request).Destination.Should().Be("output/report.zip");
    }

    [Fact]
    public void AdjustDestination_Directory_Unchanged()
    {
        var request = new DeliveryRequest
        {
            Provider = "ftp",
            Destination = "inbox/",
            Profile = "main",
            Zip = true,
        };

        ArtifactZipPackager.AdjustDestination(request).Destination.Should().Be("inbox/");
    }

    [Fact]
    public void AdjustDestination_Mail_LeavesToAddress()
    {
        var request = new DeliveryRequest
        {
            Provider = "mail",
            Destination = "ops@example.com",
            Zip = true,
        };

        ArtifactZipPackager.AdjustDestination(request).Destination.Should().Be("ops@example.com");
    }

    private static Artifact SampleArtifact() =>
        new()
        {
            Content = "<html/>"u8.ToArray(),
            ContentType = "text/html",
            FileName = "out.html",
        };
}
