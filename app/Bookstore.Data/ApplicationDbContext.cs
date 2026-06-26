using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using System;

namespace Bookstore.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Address> Address { get; set; }
        public DbSet<Book> Book { get; set; }
        public DbSet<Customer> Customer { get; set; }
        public DbSet<Order> Order { get; set; }
        public DbSet<ShoppingCart> ShoppingCart { get; set; }
        public DbSet<OrderItem> OrderItem { get; set; }
        public DbSet<Offer> Offer { get; set; }
        public DbSet<ReferenceDataItem> ReferenceData { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>()
                .Property(x => x.Sub)
                .HasColumnType("nvarchar")
                .HasMaxLength(450);

            modelBuilder.Entity<Customer>()
                .HasIndex(x => x.Sub)
                .IsUnique();

            modelBuilder.Entity<Book>()
                .HasOne(x => x.Publisher).WithMany().HasForeignKey(x => x.PublisherId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Book>()
                .HasOne(x => x.BookType).WithMany().HasForeignKey(x => x.BookTypeId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Book>()
                .HasOne(x => x.Genre).WithMany().HasForeignKey(x => x.GenreId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Book>()
                .HasOne(x => x.Condition).WithMany().HasForeignKey(x => x.ConditionId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Offer>()
                .HasOne(x => x.Publisher).WithMany().HasForeignKey(x => x.PublisherId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Offer>()
                .HasOne(x => x.BookType).WithMany().HasForeignKey(x => x.BookTypeId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Offer>()
                .HasOne(x => x.Genre).WithMany().HasForeignKey(x => x.GenreId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Offer>()
                .HasOne(x => x.Condition).WithMany().HasForeignKey(x => x.ConditionId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(x => x.Customer).WithMany().OnDelete(DeleteBehavior.Restrict);

            // Order uses a private readonly backing field for its OrderItems collection
            modelBuilder.Entity<Order>()
                .Navigation(o => o.OrderItems)
                .HasField("orderItems");

            modelBuilder.Entity<ReferenceDataItem>().ToTable("ReferenceData");

            modelBuilder.Entity<ShoppingCartItem>()
                .HasKey(x => new { x.Id, x.ShoppingCartId });
            modelBuilder.Entity<ShoppingCartItem>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();

            SeedData(modelBuilder);
        }

        private static void SeedData(ModelBuilder modelBuilder)
        {
            var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<ReferenceDataItem>().HasData(
                new ReferenceDataItem(ReferenceDataType.BookType, "Hardcover") { Id = 1, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.BookType, "Trade Paperback") { Id = 2, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.BookType, "Mass Market Paperback") { Id = 3, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Condition, "New") { Id = 4, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Condition, "Like New") { Id = 5, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Condition, "Good") { Id = 6, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Condition, "Acceptable") { Id = 7, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Genre, "Biographies") { Id = 8, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Genre, "Children's Books") { Id = 9, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Genre, "History") { Id = 10, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Genre, "Literature & Fiction") { Id = 11, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Genre, "Mystery, Thriller & Suspense") { Id = 12, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Genre, "Science Fiction & Fantasy") { Id = 13, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Genre, "Travel") { Id = 14, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Arcadia Books") { Id = 15, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Astral Publishing") { Id = 16, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Moonlight Publishing") { Id = 17, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Dreamscape Press") { Id = 18, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Enchanted Library") { Id = 19, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Fantasia House") { Id = 20, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Horizon Books") { Id = 21, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Infinity Press") { Id = 22, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Paradigm Publishing") { Id = 23, CreatedOn = now, UpdatedOn = now },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Aurora Publishing") { Id = 24, CreatedOn = now, UpdatedOn = now }
            );

            modelBuilder.Entity<Book>().HasData(
                new Book("2020: The Apocalypse", "Li Juan", "6556784356", 15, 1, 13, 5, 10.95M, 25, null, null, "/Content/Images/coverimages/apocalypse.png") { Id = 1, CreatedOn = now, UpdatedOn = now },
                new Book("Children Of Iron", "Nikki Wolf", "7665438976", 16, 1, 11, 6, 13.95M, 3, null, null, "/Content/Images/coverimages/childrenofiron.png") { Id = 2, CreatedOn = now, UpdatedOn = now },
                new Book("Gold In The Dark", "Richard Roe", "5442280765", 17, 1, 13, 5, 6.50M, 10, null, null, "/Content/Images/coverimages/goldinthedark.png") { Id = 3, CreatedOn = now, UpdatedOn = now },
                new Book("Leagues Of Smoke", "Pat Candella", "4556789542", 18, 2, 11, 7, 3M, 1, null, null, "/Content/Images/coverimages/leaguesofsmoke.png") { Id = 4, CreatedOn = now, UpdatedOn = now },
                new Book("Alone With The Stars", "Carlos Salazar", "4563358087", 19, 2, 12, 5, 15.95M, 5, null, null, "/Content/Images/coverimages/alonewiththestars.png") { Id = 5, CreatedOn = now, UpdatedOn = now },
                new Book("The Girl In The Polaroid", "Terri Whitlock", "2354435678", 20, 1, 12, 6, 8.25M, 2, null, null, "/Content/Images/coverimages/girlinthepolaroid.png") { Id = 6, CreatedOn = now, UpdatedOn = now },
                new Book("1001 Jokes", "Mary Major", "6554789632", 21, 2, 11, 5, 13.95M, 7, null, null, "/Content/Images/coverimages/1001jokes.png") { Id = 7, CreatedOn = now, UpdatedOn = now },
                new Book("My Search For Meaning", "Mateo Jackson", "4558786554", 22, 3, 8, 7, 5M, 15, null, null, "/Content/Images/coverimages/mysearchformeaning.png") { Id = 8, CreatedOn = now, UpdatedOn = now }
            );
        }
    }
}
