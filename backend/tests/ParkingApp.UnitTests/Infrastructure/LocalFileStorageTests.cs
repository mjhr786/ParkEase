using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using ParkingApp.Infrastructure.Services;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure;

public class LocalFileStorageTests : IDisposable
{
    private readonly string _tempPath;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);
        _storage = new LocalFileStorage(_tempPath, "http://localhost:5000");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }
    }

    [Fact]
    public async Task UploadFileAsync_ShouldSaveFile_AndReturnRelativePath()
    {
        // Arrange
        var fileName = "test.txt";
        var content = "Hello World";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var relativePath = await _storage.UploadFileAsync(stream, fileName, "text/plain");

        // Assert
        relativePath.Should().StartWith("/uploads/");
        
        var fullPath = Path.Combine(_tempPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).Should().BeTrue();
        
        var savedContent = await File.ReadAllTextAsync(fullPath);
        savedContent.Should().Be(content);
    }

    [Fact]
    public async Task DeleteFileAsync_ShouldRemoveFile_WhenExists()
    {
        // Arrange
        var fileName = "to-delete.txt";
        var uploadsDir = Path.Combine(_tempPath, "uploads");
        Directory.CreateDirectory(uploadsDir);
        var filePath = Path.Combine(uploadsDir, fileName);
        await File.WriteAllTextAsync(filePath, "delete me");

        // Act
        await _storage.DeleteFileAsync($"/uploads/{fileName}");

        // Assert
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public void GetPresignedUploadUrl_ShouldThrowNotSupported()
    {
        // Act
        Action act = () => _storage.GetPresignedUploadUrl("file.jpg", "image/jpeg", TimeSpan.FromMinutes(10));

        // Assert
        act.Should().Throw<NotSupportedException>();
    }
}
