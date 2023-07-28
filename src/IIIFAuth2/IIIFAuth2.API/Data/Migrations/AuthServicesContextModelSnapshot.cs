﻿// <auto-generated />
using System;
using IIIFAuth2.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IIIFAuth2.API.Data.Migrations
{
    [DbContext(typeof(AuthServicesContext))]
    partial class AuthServicesContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.9")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("IIIFAuth2.API.Data.Entities.AccessService", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("AccessTokenErrorHeading")
                        .HasColumnType("text")
                        .HasColumnName("access_token_error_heading");

                    b.Property<string>("AccessTokenErrorNote")
                        .HasColumnType("text")
                        .HasColumnName("access_token_error_note");

                    b.Property<string>("ConfirmLabel")
                        .HasColumnType("text")
                        .HasColumnName("confirm_label");

                    b.Property<int>("Customer")
                        .HasColumnType("integer")
                        .HasColumnName("customer");

                    b.Property<string>("Heading")
                        .HasColumnType("text")
                        .HasColumnName("heading");

                    b.Property<string>("Label")
                        .HasColumnType("text")
                        .HasColumnName("label");

                    b.Property<string>("LogoutLabel")
                        .HasColumnType("text")
                        .HasColumnName("logout_label");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<string>("Note")
                        .HasColumnType("text")
                        .HasColumnName("note");

                    b.Property<string>("Profile")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("profile");

                    b.Property<Guid?>("RoleProviderId")
                        .HasColumnType("uuid")
                        .HasColumnName("role_provider_id");

                    b.HasKey("Id")
                        .HasName("pk_access_services");

                    b.HasIndex("RoleProviderId")
                        .HasDatabaseName("ix_access_services_role_provider_id");

                    b.HasIndex("Customer", "Name")
                        .IsUnique()
                        .HasDatabaseName("ix_access_services_customer_name");

                    b.ToTable("access_services", (string)null);
                });

            modelBuilder.Entity("IIIFAuth2.API.Data.Entities.Role", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<int>("Customer")
                        .HasColumnType("integer")
                        .HasColumnName("customer");

                    b.Property<Guid>("AccessServiceId")
                        .HasColumnType("uuid")
                        .HasColumnName("access_service_id");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.HasKey("Id", "Customer")
                        .HasName("pk_roles");

                    b.ToTable("roles", (string)null);
                });

            modelBuilder.Entity("IIIFAuth2.API.Data.Entities.RoleProvider", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Configuration")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("configuration");

                    b.HasKey("Id")
                        .HasName("pk_role_providers");

                    b.ToTable("role_providers", (string)null);
                });

            modelBuilder.Entity("IIIFAuth2.API.Data.Entities.RoleProvisionToken", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<DateTime>("Created")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created");

                    b.Property<int>("Customer")
                        .HasColumnType("integer")
                        .HasColumnName("customer");

                    b.Property<string>("Roles")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("roles");

                    b.Property<bool>("Used")
                        .HasColumnType("boolean")
                        .HasColumnName("used");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("Id")
                        .HasName("pk_role_provision_tokens");

                    b.ToTable("role_provision_tokens", (string)null);
                });

            modelBuilder.Entity("IIIFAuth2.API.Data.Entities.SessionUser", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("AccessToken")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("access_token");

                    b.Property<string>("CookieId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("cookie_id");

                    b.Property<DateTime>("Created")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created")
                        .HasDefaultValueSql("now()");

                    b.Property<int>("Customer")
                        .HasColumnType("integer")
                        .HasColumnName("customer");

                    b.Property<DateTime>("Expires")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("expires");

                    b.Property<DateTime?>("LastChecked")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_checked");

                    b.Property<string>("Origin")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("origin");

                    b.Property<string>("Roles")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("roles");

                    b.HasKey("Id")
                        .HasName("pk_session_users");

                    b.ToTable("session_users", (string)null);
                });

            modelBuilder.Entity("IIIFAuth2.API.Data.Entities.AccessService", b =>
                {
                    b.HasOne("IIIFAuth2.API.Data.Entities.RoleProvider", "RoleProvider")
                        .WithMany("AccessServices")
                        .HasForeignKey("RoleProviderId")
                        .HasConstraintName("fk_access_services_role_providers_role_provider_id");

                    b.Navigation("RoleProvider");
                });

            modelBuilder.Entity("IIIFAuth2.API.Data.Entities.RoleProvider", b =>
                {
                    b.Navigation("AccessServices");
                });
#pragma warning restore 612, 618
        }
    }
}
