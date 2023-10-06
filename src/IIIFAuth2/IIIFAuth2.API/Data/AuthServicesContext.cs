#nullable disable

using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Data.Converters;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Converters;
using IIIFAuth2.API.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
    public DbSet<CustomerCookieDomain> CustomerCookieDomains { get; set; }
    
    public DbSet<RoleProvisionToken> RoleProvisionTokens { get; set; }
    
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
        
        configurationBuilder
            .Properties<List<string>>()
            .HaveConversion<StringListConverter, StringListComparer>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
        });
        
        modelBuilder.Entity<RoleProvisionToken>()
            .Property(b => b.Version)
            .IsRowVersion();

        modelBuilder.Entity<CustomerCookieDomain>()
            .Property(p => p.Customer)
            .ValueGeneratedNever();
            
        modelBuilder.Entity<CustomerCookieDomain>()
            .HasKey(p => p.Customer);
    }
}