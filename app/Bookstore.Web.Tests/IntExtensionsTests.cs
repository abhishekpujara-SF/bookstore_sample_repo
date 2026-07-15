using Xunit;
using Bookstore.Web.Helpers;

namespace Bookstore.Web.Tests
{
    public class IntExtensionsTests
    {
        [Fact]
        public void ToStorageSize_ZeroBytes_ReturnsZeroB()
        {
            // Arrange
            int bytes = 0;

            // Act
            string result = bytes.ToStorageSize();

            // Assert
            Assert.Equal("0 B", result);
        }

        [Fact]
        public void ToStorageSize_LessThan1KB_ReturnsBytesFormat()
        {
            // Arrange
            int bytes = 500;

            // Act
            string result = bytes.ToStorageSize();

            // Assert
            Assert.Equal("500 B", result);
        }

        [Fact]
        public void ToStorageSize_Exactly1KB_Returns1KB()
        {
            // Arrange
            int bytes = 1024;

            // Act
            string result = bytes.ToStorageSize();

            // Assert
            Assert.Equal("1 KB", result);
        }

        [Fact]
        public void ToStorageSize_LessThan1MB_ReturnsKBFormat()
        {
            // Arrange
            int bytes = 1024 * 500; // 500 KB

            // Act
            string result = bytes.ToStorageSize();

            // Assert
            Assert.Equal("500 KB", result);
        }

        [Fact]
        public void ToStorageSize_Exactly1MB_Returns1MB()
        {
            // Arrange
            int bytes = 1024 * 1024; // 1 MB

            // Act
            string result = bytes.ToStorageSize();

            // Assert
            Assert.Equal("1 MB", result);
        }

        [Fact]
        public void ToStorageSize_LessThan1GB_ReturnsMBFormat()
        {
            // Arrange
            int bytes = 1024 * 1024 * 500; // 500 MB

            // Act
            string result = bytes.ToStorageSize();

            // Assert
            Assert.Equal("500 MB", result);
        }

        [Fact]
        public void ToStorageSize_Exactly1GB_Returns1GB()
        {
            // Arrange
            int bytes = 1024 * 1024 * 1024; // 1 GB

            // Act
            string result = bytes.ToStorageSize();

            // Assert
            Assert.Equal("1 GB", result);
        }




    }
}
