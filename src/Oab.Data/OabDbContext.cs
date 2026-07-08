using Microsoft.EntityFrameworkCore;
using Oab.Core.Domain;

namespace Oab.Data;

public class OabDbContext(DbContextOptions<OabDbContext> options) : DbContext(options)
{
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentLine> DocumentLines => Set<DocumentLine>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Party>(party =>
        {
            party.HasKey(p => p.Id);
            party.Property(p => p.Name).IsRequired();
            party.HasIndex(p => p.Name);
        });

        modelBuilder.Entity<Document>(doc =>
        {
            doc.HasKey(d => d.Id);
            doc.HasIndex(d => d.PartyId);
            doc.HasIndex(d => d.OccurredAt);
            doc.HasMany(d => d.Lines)
               .WithOne()
               .HasForeignKey(l => l.DocumentId)
               .OnDelete(DeleteBehavior.Cascade);
            doc.HasOne<Party>()
               .WithMany()
               .HasForeignKey(d => d.PartyId)
               .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentLine>(line =>
        {
            line.HasKey(l => l.Id);
            line.Ignore(l => l.Total); // computed
        });

        modelBuilder.Entity<LedgerEntry>(entry =>
        {
            entry.HasKey(e => e.Id);
            entry.HasIndex(e => e.PartyId);
            entry.HasIndex(e => e.DocumentId);
            entry.HasIndex(e => e.CreatedAtUtc);
            entry.HasOne<Party>()
                 .WithMany()
                 .HasForeignKey(e => e.PartyId)
                 .OnDelete(DeleteBehavior.Restrict);
            entry.HasOne<Document>()
                 .WithMany()
                 .HasForeignKey(e => e.DocumentId)
                 .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
