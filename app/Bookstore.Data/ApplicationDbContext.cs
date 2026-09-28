using System.ComponentModel.DataAnnotations.Schema;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly string _connectionString;

        public ApplicationDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        // EF Core requires DbContextOptions constructor for DI
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Address> Address { get; set; }

        public DbSet<Book> Book { get; set; }

        public DbSet<Customer> Customer { get; set; }

        public DbSet<Order> Order { get; set; }

        public DbSet<ShoppingCart> ShoppingCart { get; set; }

        public DbSet<OrderItem> OrderItem { get; set; }

        public DbSet<Offer> Offer { get; set; }

        public DbSet<ReferenceDataItem> ReferenceData { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured && _connectionString != null)
            {
                optionsBuilder.UseSqlServer(_connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // EF6: modelBuilder.Conventions.Remove<PluralizingTableNameConvention>()
            // EF Core: no conventions API, table names non-pluralized by default

            // EF6: HasIndex().IsUnique() -> EF Core: HasIndex().IsUnique()
            modelBuilder.Entity<Customer>().Property(x => x.Sub).HasColumnType("nvarchar(450)");
            modelBuilder.Entity<Customer>().HasIndex(x => x.Sub).IsUnique();

            // EF6: HasRequired().WithMany().WillCascadeOnDelete(false)
            // EF Core: HasOne().WithMany().OnDelete(DeleteBehavior.Restrict)
            modelBuilder.Entity<Book>()
                .HasOne(x => x.Publisher).WithMany().HasForeignKey(x => x.PublisherId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Book>()
                .HasOne(x => x.BookType).WithMany().HasForeignKey(x => x.BookTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Book>()
                .HasOne(x => x.Genre).WithMany().HasForeignKey(x => x.GenreId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Book>()
                .HasOne(x => x.Condition).WithMany().HasForeignKey(x => x.ConditionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Offer>()
                .HasOne(x => x.Publisher).WithMany().HasForeignKey(x => x.PublisherId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Offer>()
                .HasOne(x => x.BookType).WithMany().HasForeignKey(x => x.BookTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Offer>()
                .HasOne(x => x.Genre).WithMany().HasForeignKey(x => x.GenreId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Offer>()
                .HasOne(x => x.Condition).WithMany().HasForeignKey(x => x.ConditionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(x => x.Customer).WithMany()
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReferenceDataItem>().ToTable("ReferenceData");

            // EF6: HasKey() + HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity)
            // EF Core: HasKey() + ValueGeneratedOnAdd()
            modelBuilder.Entity<ShoppingCartItem>().HasKey(x => new { x.Id, x.ShoppingCartId });
            modelBuilder.Entity<ShoppingCartItem>().Property(x => x.Id).ValueGeneratedOnAdd();

            // EF6: Database.SetInitializer(new BookstoreDbInitializer())
            // EF Core: No initializers - use migrations or EnsureCreated() in Program.cs
            // TODO: Replace BookstoreDbInitializer with EF Core migration or seed data in Program.cs
        }
    }
}