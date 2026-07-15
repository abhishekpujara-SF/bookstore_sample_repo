using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;
using Bookstore.Domain.Books;
using Bookstore.Domain.Tests.Builders;

namespace Bookstore.Domain.Tests
{
    public class BookTests
    {
        [Fact]
        public void ReduceStockLevel_QuantityGreaterThanStock_SetsStockToZero()
        {
            // Arrange
            var book = new BookBuilder()
                .Quantity(5)
                .Build();

            // Act
            book.ReduceStockLevel(10);

            // Assert
            Assert.Equal(0, book.Quantity);
        }

        [Fact]
        public void ReduceStockLevel_QuantityLessThanStock_ReducesStockCorrectly()
        {
            // Arrange
            var book = new BookBuilder()
                .Quantity(10)
                .Build();

            // Act
            book.ReduceStockLevel(3);

            // Assert
            Assert.Equal(7, book.Quantity);
        }

        [Fact]
        public void ReduceStockLevel_QuantityEqualToStock_SetsStockToZero()
        {
            // Arrange
            var book = new BookBuilder()
                .Quantity(8)
                .Build();
            // Act
            book.ReduceStockLevel(8);

            // Assert
            Assert.Equal(0, book.Quantity);
        }


    }
}
