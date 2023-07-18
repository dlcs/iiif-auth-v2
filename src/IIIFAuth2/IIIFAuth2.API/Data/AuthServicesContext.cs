#nullable disable

using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
// ReSharper disable ClassNeverInstantiated.Global

namespace IIIFAuth2.API.Data;

public class AuthServicesContext : DbContext
{
    public DbSet<RoleProvider> RoleProviders { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<AccessService> AccessServices { get; set; }
    public DbSet<SessionUser> SessionUsers { get; set; }
    
    public AuthServicesContext(DbContextOptions<AuthServicesContext> options) : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<LanguageMap>()
            .HaveConversion<LanguageMapConverter, LanguageMapComparer>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringArrayComparer = new ValueComparer<string[]>(
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToArray());

        modelBuilder
            .Entity<Role>()
            .HasKey(r => new { r.Id, r.Customer });

        modelBuilder.Entity<AccessService>(builder =>
        {
            builder
                .HasOne(s => s.RoleProvider)
                .WithMany(rp => rp.AccessServices)
                .OnDelete(DeleteBehavior.ClientSetNull);

            builder
                .HasIndex(s => new { s.Customer, s.Name })
                .IsUnique();
        });

        modelBuilder
            .Entity<RoleProvider>()
            .Property(p => p.Configuration)
            .HasColumnType("jsonb");
        
        modelBuilder.Entity<SessionUser>(builder =>
        {
            builder
                .Property(su => su.Created)
                .HasDefaultValueSql("now()");
            
            builder
                .Property(su => su.Roles)
                .HasConversion(
                    v => string.Join(",", v),
                    v => v.Split(",", StringSplitOptions.RemoveEmptyEntries).ToArray(),
                    stringArrayComparer);
        });
    }
}

/// <summary>
/// Conversion logic for LanguageMap (on model) -> string (in db)
/// </summary>
public class LanguageMapConverter : ValueConverter<LanguageMap, string>
{
    public LanguageMapConverter()
        : base(
            v => JsonConvert.SerializeObject(v),
            v => JsonConvert.DeserializeObject<LanguageMap>(v))
    {
    }
}

public class LanguageMapComparer : ValueComparer<LanguageMap>
{
    public LanguageMapComparer()
        : base(
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c
        )
    {
    }
}