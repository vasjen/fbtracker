﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using fbtracker;

#nullable disable

namespace fbtracker.Migrations
{
    [DbContext(typeof(FbDbContext))]
    partial class FbDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("fbtracker.Card", b =>
                {
                    b.Property<int>("CardId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("CardId"));

                    b.Property<int>("FbDataId")
                        .HasColumnType("integer");

                    b.Property<int>("FbId")
                        .HasColumnType("integer");

                    b.Property<string>("Position")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Price")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Raiting")
                        .HasColumnType("integer");

                    b.Property<string>("ShortName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("Tradable")
                        .HasColumnType("boolean");

                    b.Property<string>("Version")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("CardId");

                    b.ToTable("Cards");
                });

            modelBuilder.Entity("fbtracker.Profit", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("CardId")
                        .HasColumnType("integer");

                    b.Property<DateTime>("Date")
                        .HasColumnType("timestamp with time zone");

                    b.Property<decimal>("Percentage")
                        .HasColumnType("numeric");

                    b.Property<int>("Price")
                        .HasColumnType("integer");

                    b.Property<int>("ProfitValue")
                        .HasColumnType("integer");

                    b.Property<int>("SellPrice")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("CardId");

                    b.ToTable("Profits");
                });

            modelBuilder.Entity("fbtracker.Profit", b =>
                {
                    b.HasOne("fbtracker.Card", "Card")
                        .WithMany("Profits")
                        .HasForeignKey("CardId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Card");
                });

            modelBuilder.Entity("fbtracker.Card", b =>
                {
                    b.Navigation("Profits");
                });
#pragma warning restore 612, 618
        }
    }
}
