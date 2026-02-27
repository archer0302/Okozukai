using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Okozukai.Domain.Transactions;

namespace Okozukai.Infrastructure.Persistence;

public sealed class OkozukaiDbContext : DbContext
{
    public OkozukaiDbContext(DbContextOptions<OkozukaiDbContext> options)
        : base(options)
    {
    }

    public DbSet<Journal> Journals => Set<Journal>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
