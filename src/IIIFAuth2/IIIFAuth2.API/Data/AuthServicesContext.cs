#nullable disable

using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Converters;
using IIIFAuth2.API.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

// ReSharper disable ClassNeverInstantiated.Global

namespace IIIFAuth2.API.Data;

public class AuthServicesContext : DbContext
{
    public DbSet<RoleProvider> RoleProviders { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<AccessService> AccessServices { get; set; }
    public DbSet<SessionUser> SessionUsers { get; set; }
    
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None,
        Converters = new List<JsonConverter> { new RoleProviderConverter() }
    };
    
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
        modelBuilder.Entity<AccessService>().Navigation(s => s.RoleProvider).AutoInclude();

        modelBuilder
            .Entity<RoleProvider>()
            .Property(p => p.Configuration)
            .HasColumnType("jsonb")
            .HasConversion(
                modelValue => JsonConvert.SerializeObject(modelValue, JsonSettings),
                dbValue => JsonConvert.DeserializeObject<RoleProviderConfiguration>(dbValue, JsonSettings),
                new ValueComparer<RoleProviderConfiguration>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c));
        
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

/// <summary>
/// Comparison logic for LanguageMap values. Used by EF internals for determining when a field has changed 
/// </summary>
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