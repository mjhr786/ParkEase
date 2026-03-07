using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ParkingApp.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class R2FileStorageTests
{
    private readonly Mock<IAmazonS3> _s3ClientMock;
    private readonly R2FileStorage _service;

    public R2FileStorageTests()
    {
        _s3ClientMock = new Mock<IAmazonS3>();
        
        var inMemorySettings = new Dictionary<string, string?> {
            {"Storage:R2:BucketName", "test-bucket"},
            {"Storage:R2:PublicUrl", "https://test.r2.cloudflarestorage.com"}
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _service = new R2FileStorage(_s3ClientMock.Object, configuration);
    }

    [Fact]
    public void Constructor_MissingBucketName_ThrowsInvalidOperationException()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?> {
            {"Storage:R2:PublicUrl", "https://test.r2.cloudflarestorage.com"}
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

        // Act
        Action action = () => new R2FileStorage(_s3ClientMock.Object, configuration);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("R2 BucketName is missing");
    }

    [Fact]
    public void Constructor_MissingPublicUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?> {
            {"Storage:R2:BucketName", "test-bucket"}
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

        // Act
        Action action = () => new R2FileStorage(_s3ClientMock.Object, configuration);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("R2 PublicUrl is missing");
    }

    [Fact]
    public async Task UploadFileAsync_UploadsToS3AndReturnsUrl()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        _s3ClientMock.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var resultUrl = await _service.UploadFileAsync(stream, "test.jpg", "image/jpeg", CancellationToken.None);

        // Assert
        resultUrl.Should().StartWith("https://test.r2.cloudflarestorage.com/");
        resultUrl.Should().EndWith(".jpg");
        _s3ClientMock.Verify(x => x.PutObjectAsync(It.Is<PutObjectRequest>(r => r.BucketName == "test-bucket" && r.ContentType == "image/jpeg"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_WithRelativeKey_DeletesFromS3()
    {
        // Arrange
        _s3ClientMock.Setup(x => x.DeleteObjectAsync("test-bucket", "some-key.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse { HttpStatusCode = HttpStatusCode.NoContent });

        // Act
        var exception = await Record.ExceptionAsync(() => _service.DeleteFileAsync("some-key.jpg", CancellationToken.None));

        // Assert
        exception.Should().BeNull();
        _s3ClientMock.Verify(x => x.DeleteObjectAsync("test-bucket", "some-key.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_WithFullUrl_ExtractsKeyAndDeletes()
    {
        // Arrange
        _s3ClientMock.Setup(x => x.DeleteObjectAsync("test-bucket", "some-key.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse { HttpStatusCode = HttpStatusCode.NoContent });

        // Act
        var exception = await Record.ExceptionAsync(() => _service.DeleteFileAsync("https://test.r2.cloudflarestorage.com/some-key.jpg", CancellationToken.None));

        // Assert
        exception.Should().BeNull();
        _s3ClientMock.Verify(x => x.DeleteObjectAsync("test-bucket", "some-key.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_IgnoresNotFoundException()
    {
        // Arrange
        _s3ClientMock.Setup(x => x.DeleteObjectAsync("test-bucket", "missing.jpg", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

        // Act
        var exception = await Record.ExceptionAsync(() => _service.DeleteFileAsync("missing.jpg", CancellationToken.None));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void GetPresignedUploadUrl_ReturnsUrlsAndKey()
    {
        // Arrange
        _s3ClientMock.Setup(x => x.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Returns("https://test.r2.cloudflarestorage.com/presigned-url");

        // Act
        var result = _service.GetPresignedUploadUrl("test.png", "image/png", TimeSpan.FromMinutes(10));

        // Assert
        result.UploadUrl.Should().Be("https://test.r2.cloudflarestorage.com/presigned-url");
        result.PublicUrl.Should().StartWith("https://test.r2.cloudflarestorage.com/");
        result.PublicUrl.Should().EndWith(".png");
        result.Key.Should().EndWith(".png");
    }
}
