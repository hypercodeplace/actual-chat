﻿// <auto-generated />
using System;
using ActualChat.Chat.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ActualChat.Chat.Migrations.Migrations
{
    [DbContext(typeof(ChatDbContext))]
    [Migration("20220204101911_UserAvatars")]
    partial class UserAvatars
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("ActualChat.Chat.Db.DbChat", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("IsPublic")
                        .HasColumnType("boolean");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.ToTable("Chats");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatAuthor", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<string>("ChatId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("IsAnonymous")
                        .HasColumnType("boolean");

                    b.Property<long>("LocalId")
                        .HasColumnType("bigint");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("UserId")
                        .HasColumnType("text");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("ChatId", "LocalId");

                    b.HasIndex("ChatId", "UserId");

                    b.ToTable("ChatAuthors");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatEntry", b =>
                {
                    b.Property<string>("CompositeId")
                        .HasColumnType("text");

                    b.Property<long?>("AudioEntryId")
                        .HasColumnType("bigint");

                    b.Property<string>("AuthorId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("BeginsAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ChatId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("ClientSideBeginsAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("ContentEndsAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<double>("Duration")
                        .HasColumnType("double precision");

                    b.Property<DateTime?>("EndsAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<bool>("IsRemoved")
                        .HasColumnType("boolean");

                    b.Property<string>("StreamId")
                        .HasColumnType("text");

                    b.Property<string>("TextToTimeMap")
                        .HasColumnType("text");

                    b.Property<int>("Type")
                        .HasColumnType("integer");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint");

                    b.Property<long?>("VideoEntryId")
                        .HasColumnType("bigint");

                    b.HasKey("CompositeId");

                    b.HasIndex("ChatId", "Type", "Id");

                    b.HasIndex("ChatId", "Type", "Version");

                    b.HasIndex("ChatId", "Type", "BeginsAt", "EndsAt");

                    b.HasIndex("ChatId", "Type", "EndsAt", "BeginsAt");

                    b.HasIndex("ChatId", "Type", "IsRemoved", "Id");

                    b.ToTable("ChatEntries");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatOwner", b =>
                {
                    b.Property<string>("ChatId")
                        .HasColumnType("text");

                    b.Property<string>("UserId")
                        .HasColumnType("text");

                    b.Property<string>("DbChatId")
                        .HasColumnType("text");

                    b.HasKey("ChatId", "UserId");

                    b.HasIndex("DbChatId");

                    b.ToTable("ChatOwners");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbInviteCode", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<string>("ChatId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("CreatedBy")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("ExpiresOn")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("State")
                        .HasColumnType("integer");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("Value")
                        .IsUnique()
                        .HasFilter("\"State\" = 0");

                    b.ToTable("InviteCodes");
                });

            modelBuilder.Entity("Stl.Fusion.EntityFramework.Operations.DbOperation", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<string>("AgentId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("CommandJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("CommitTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ItemsJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("StartTime")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex(new[] { "CommitTime" }, "IX_CommitTime");

                    b.HasIndex(new[] { "StartTime" }, "IX_StartTime");

                    b.ToTable("_Operations");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatOwner", b =>
                {
                    b.HasOne("ActualChat.Chat.Db.DbChat", null)
                        .WithMany("Owners")
                        .HasForeignKey("DbChatId");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChat", b =>
                {
                    b.Navigation("Owners");
                });
#pragma warning restore 612, 618
        }
    }
}
