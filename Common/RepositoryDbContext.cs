using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Common
{
    //dotnet ef migrations add MapearCampos --project Common
    public class RepositoryDbContext : DbContext
    {
        public RepositoryDbContext(DbContextOptions<RepositoryDbContext> options)
            : base(options)
        {
        }

        public RepositoryDbContext()
        {
        }

        public DbSet<Commit> Commits { get; set; }
        public DbSet<Build> Builds { get; set; }
        public DbSet<Pipeline> Pipelines { get; set; }
        public DbSet<Repository> Repositories { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Configure the database provider and connection string
                var databasePath = @"C:\repos\Builds.db";
                var connectionString = $"Data Source={databasePath}";
                optionsBuilder.UseSqlite(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new CommitConfiguration());
            modelBuilder.ApplyConfiguration(new BuildConfiguration());
            modelBuilder.ApplyConfiguration(new PipelineConfiguration());
            modelBuilder.ApplyConfiguration(new RepositoryConfiguration());

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
    }

    public class CommitConfiguration : IEntityTypeConfiguration<Commit>
    {
        public void Configure(EntityTypeBuilder<Commit> builder)
        {
            builder.ToTable("Commits");

            builder.HasKey(c => c.Id);

            builder.Property(c => c.Id)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(c => c.Message)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(c => c.Url)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(c => c.AuthorName)
                   .IsRequired()
                   .HasMaxLength(100);

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

            builder.Property(b => b.Status)
                   .IsRequired(false) // Se não tem pipeline cadastrada não tem status
                   .HasMaxLength(50);

            builder.Property(b => b.Result) 
                   .IsRequired(false) // Se não tem pipeline cadastrada não tem result
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

            builder.HasOne(b => b.Commit)
                   .WithMany()
                   .HasForeignKey("CommitId")
                   .IsRequired(false) // Make this relationship optional
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
    {
        public void Configure(EntityTypeBuilder<Pipeline> builder)
        {
            builder.ToTable("Pipelines");

            builder.HasKey(p => p.Id);

            builder.HasOne(p => p.Last)
                   .WithMany()
                   .HasForeignKey("LastBuildId")
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.LastSuccessful)
                   .WithMany()
                   .HasForeignKey("LastSuccessfulBuildId")
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
    {
        public void Configure(EntityTypeBuilder<Repository> builder)
        {
            builder.ToTable("Repositories");

            builder.HasKey(r => r.Id);

            builder.Property(r => r.Project)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(r => r.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(r => r.Url)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(r => r.CloneUrl)
                   .HasMaxLength(500);

            builder.Property(r => r.MasterClonned)
                   .IsRequired();

            builder.Ignore(r => r.Path);

            builder.HasOne(r => r.Pipeline)
                   .WithOne()
                   .HasForeignKey<Pipeline>("RepositoryId")
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

}
