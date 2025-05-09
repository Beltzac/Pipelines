using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.SQLite;
using Common.Models;
using Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartComponents.LocalEmbeddings;

namespace Common.Repositories.Interno
{
    //dotnet ef migrations add MapearCampos --project Common
    //Add-Migration MapearEmbbeding
    //EntityFrameworkCore\Add-Migration Pin -Context RepositoryDbContext
    public class RepositoryDbContext : DbContext
    {
        public RepositoryDbContext(DbContextOptions<RepositoryDbContext> options)
            : base(options)
        {
        }

        public DbSet<Commit> Commits { get; set; }
        public DbSet<Build> Builds { get; set; }
        public DbSet<Pipeline> Pipelines { get; set; }
        public DbSet<Repository> Repositories { get; set; }
        public DbSet<FileChunkRecord> FileChunks { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // if (!optionsBuilder.IsConfigured)
            // {
            //     var databasePath = DBUtils.MainDBPath;
            //     var connectionString = $"Data Source={databasePath}"; //;Journal Mode=WAL
            //     optionsBuilder.UseSqlite(connectionString)
            //         .EnableSensitiveDataLogging();
            // }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.AddQuartz(builder => builder.UseSqlite());

            modelBuilder.ApplyConfiguration(new CommitConfiguration());
            modelBuilder.ApplyConfiguration(new BuildConfiguration());
            modelBuilder.ApplyConfiguration(new PipelineConfiguration());
            modelBuilder.ApplyConfiguration(new RepositoryConfiguration());
            modelBuilder.ApplyConfiguration(new FileChunkRecordConfiguration());

            // Loop through all entity types in the model
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // Get all navigation properties for the entity type
                var navigations = entityType.GetNavigations();

                // Set AutoInclude for each navigation property
                foreach (var navigation in navigations)
                {
                    navigation.SetIsEagerLoaded(true);
                }
            }
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder mcb)
        {
            mcb.Properties<string>().UseCollation("NOCASE");
        }
    }

    public class CommitConfiguration : IEntityTypeConfiguration<Commit>
    {
        public void Configure(EntityTypeBuilder<Commit> builder)
        {
            builder.ToTable("Commits");

            builder.HasKey(c => c.Id);

            builder.Property(r => r.Id)
                .ValueGeneratedNever();

            builder.Property(c => c.Id)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(c => c.CommitMessage)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(c => c.Url)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(c => c.AuthorName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.HasIndex(c => c.AuthorName);

            builder.Property(c => c.AuthorEmail)
                   .IsRequired()
                   .HasMaxLength(100);
        }
    }

    public class BuildConfiguration : IEntityTypeConfiguration<Build>
    {
        public void Configure(EntityTypeBuilder<Build> builder)
        {
            builder.ToTable("Builds");

            builder.HasKey(b => b.Id);

            builder.Property(r => r.Id)
                .ValueGeneratedNever();

            builder.Property(b => b.Status)
                   .IsRequired(false) // Se n�o tem pipeline cadastrada n�o tem status
                   .HasMaxLength(50);

            builder.Property(b => b.Result)
                   .IsRequired(false) // Se n�o tem pipeline cadastrada n�o tem result
                   .HasMaxLength(50);

            builder.Property(b => b.Url)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(b => b.ErrorLogs)
                   .HasMaxLength(2000)
                   .IsRequired(false);

            builder.Property(b => b.Queued)
                   .IsRequired(false);

            builder.Property(b => b.Changed)
                   .IsRequired();

            builder.HasIndex(b => b.Changed);

            builder.HasOne(b => b.Commit)
                   .WithMany()
                   .HasForeignKey("CommitId")
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
    {
        public void Configure(EntityTypeBuilder<Pipeline> builder)
        {
            builder.ToTable("Pipelines");

            builder.HasKey(p => p.Id);

            builder.Property(r => r.Id)
                .ValueGeneratedNever();

            builder.HasOne(p => p.Last)
                   .WithMany()
                   .HasForeignKey("LastBuildId")
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(p => p.LastSuccessful)
                   .WithMany()
                   .HasForeignKey("LastSuccessfulBuildId")
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
    {
        public void Configure(EntityTypeBuilder<Repository> builder)
        {
            builder.ToTable("Repositories");

            builder.HasKey(r => r.Id);

            builder.Property(r => r.Id)
                .ValueGeneratedNever();

            builder.Property(r => r.Project)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.HasIndex(r => r.Project);

            builder.Property(r => r.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.HasIndex(r => r.Name);

            builder.Property(r => r.Url)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.HasIndex(r => r.Url)
                   .IsUnique();

            builder.Property(r => r.CloneUrl)
                   .HasMaxLength(500);

            builder.Property(r => r.MasterClonned)
                   .IsRequired();

            builder.Property(r => r.CurrentBranch)
                   .HasMaxLength(100)
                   .IsRequired(false);

            builder.Ignore(r => r.Path);

            builder.HasOne(r => r.Pipeline)
                   .WithMany()
                   .HasForeignKey("PipelineId")
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Property(r => r.Embedding)
                .HasConversion(x => x.HasValue ? x.Value.Buffer.ToArray() : null, x => x != null ? new EmbeddingF32(x) : null);

            builder.Property(r => r.ProjectNames)
                .HasConversion(
                    x => string.Join(',', x),
                    x => x.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
                .Metadata.SetValueComparer(
                    new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()
                    )
                );

            builder.HasMany(r => r.ActivePullRequests)
                .WithOne();
        }
    }

    public class FileChunkRecordConfiguration : IEntityTypeConfiguration<FileChunkRecord>
    {
        public void Configure(EntityTypeBuilder<FileChunkRecord> builder)
        {
            builder.ToTable("FileChunkRecords");

            builder.HasKey(r => r.Id);

            builder.Property(r => r.Embedding)
                .HasConversion(x => x.HasValue ? x.Value.Buffer.ToArray() : null, x => x != null ? new EmbeddingF32(x) : null);
        }
    }
}
