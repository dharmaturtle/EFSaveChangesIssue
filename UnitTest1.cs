using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Xunit;

namespace XUnitTestProject2 {

  public partial class UserEntity {
    public UserEntity() {
      CardOptions = new HashSet<CardOptionEntity>();
    }

    [Key]
    public int Id { get; set; }
    [InverseProperty("User")]
    public virtual ICollection<CardOptionEntity> CardOptions { get; set; }
  }

  public partial class CardOptionEntity {
    [Key]
    public int Id { get; set; }
    public int UserId { get; set; }
    public bool IsDefault { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("CardOptions")]
    public virtual UserEntity User { get; set; }
  }

  public partial class CardOverflowDb : DbContext {
    public virtual DbSet<CardOptionEntity> CardOption { get; set; }
    public virtual DbSet<UserEntity> User { get; set; }

    public CardOverflowDb(DbContextOptions<CardOverflowDb> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
      base.OnModelCreating(modelBuilder);

      modelBuilder.Entity<CardOptionEntity>(entity => {
        entity.HasIndex(e => e.UserId)
            .IsUnique()
            .HasFilter("([IsDefault]=(1))");

        entity.HasOne(d => d.User)
            .WithMany(p => p.CardOptions)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull);
      });

      modelBuilder.Entity<UserEntity>(entity => {
        entity.ToTable("User");
      });

    }
  }

  public class UnitTest1 {

    private void _NewContext(Action<CardOverflowDb> action, ILoggerFactory loggerFactory = null) {
      var dbContextOptions =
        new DbContextOptionsBuilder<CardOverflowDb>()
          .UseSqlServer("Server=localhost;Database=CardOverflowSaveChanges;User Id=localsa;");
      if (loggerFactory != null) {
        dbContextOptions = dbContextOptions.UseLoggerFactory(loggerFactory).EnableSensitiveDataLogging();
      }
      using var context = new CardOverflowDb(dbContextOptions.Options);
      action(context);
    }

    private void _SwitchIsDefault(ILoggerFactory loggerFactory = null) {
      _NewContext(db => {
        var options = db.CardOption.ToList();
        var a = options.Single(x => x.IsDefault);
        var b = options.Single(x => !x.IsDefault);
        a.IsDefault = false;
        b.IsDefault = true;
        Assert.Equal(2, db.ChangeTracker.Entries().Count(x => x.State == EntityState.Modified));
        db.SaveChanges();
      }, loggerFactory);
    }

    [Fact]
    public void Test1() {
      _NewContext(db => {
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var user = new UserEntity();
        db.CardOption.Add(new CardOptionEntity() { User = user, IsDefault = true });
        db.CardOption.Add(new CardOptionEntity() { User = user, IsDefault = false });
        db.SaveChanges();
      });

      var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
      _SwitchIsDefault(loggerFactory);
      _SwitchIsDefault(loggerFactory);

    }

  }
}
