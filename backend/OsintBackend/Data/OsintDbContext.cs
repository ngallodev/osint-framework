using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OsintBackend.Models;

namespace OsintBackend.Data
{
    public class OsintDbContext : DbContext
    {
        public OsintDbContext(DbContextOptions<OsintDbContext> options) : base(options) { }

        public DbSet<OsintInvestigation> Investigations { get; set; }
        public DbSet<OsintResult> Results { get; set; }
        public DbSet<ToolExecution> ToolExecutions { get; set; }
        public DbSet<ToolFinding> ToolFindings { get; set; }
        public DbSet<AiJob> AiJobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OsintInvestigation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Target).IsRequired().HasMaxLength(500);
                entity.Property(e => e.InvestigationType).IsRequired().HasMaxLength(100);
                entity.HasMany(e => e.Results)
                      .WithOne(r => r.Investigation)
                      .HasForeignKey(r => r.OsintInvestigationId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.ToolExecutions)
                      .WithOne(te => te.Investigation)
                      .HasForeignKey(te => te.OsintInvestigationId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.AiJobs)
                      .WithOne(j => j.Investigation)
                      .HasForeignKey(j => j.OsintInvestigationId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<OsintResult>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ToolName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.DataType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Summary).HasMaxLength(1000);
                entity.Property(e => e.RawData).HasColumnType("json");
            });

            modelBuilder.Entity<ToolExecution>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ToolName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.Configuration).HasColumnType("json");
                entity.Property(e => e.ExecutionMetadata).HasColumnType("json");
                entity.HasMany(e => e.Findings)
                      .WithOne(f => f.ToolExecution)
                      .HasForeignKey(f => f.ToolExecutionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ToolFinding>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FindingType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Value).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.Severity).HasMaxLength(50);
                entity.Property(e => e.Source).HasMaxLength(200);
                entity.Property(e => e.RelatedEntities).HasColumnType("json");
                entity.Property(e => e.RawData).HasColumnType("json");
                entity.Property(e => e.ReferenceUrl).HasMaxLength(500);
                entity.Property(e => e.AnalystNotes).HasMaxLength(1000);
            });

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var structuredResultConverter = new ValueConverter<AiJobStructuredResult?, string?>(
                v => v == null ? null : JsonSerializer.Serialize(v, jsonOptions),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<AiJobStructuredResult>(v, jsonOptions)!);

            var errorInfoConverter = new ValueConverter<AiJobErrorInfo?, string?>(
                v => v == null ? null : JsonSerializer.Serialize(v, jsonOptions),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<AiJobErrorInfo>(v, jsonOptions)!);

            var debugInfoConverter = new ValueConverter<AiJobDebugInfo?, string?>(
                v => v == null ? null : JsonSerializer.Serialize(v, jsonOptions),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<AiJobDebugInfo>(v, jsonOptions)!);

            modelBuilder.Entity<AiJob>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.JobType).IsRequired().HasMaxLength(32);
                entity.Property(e => e.Model).HasMaxLength(100);
                entity.Property(e => e.Status).HasConversion<int>();
                entity.Property(e => e.Prompt).HasColumnType("longtext");
                entity.Property(e => e.Result).HasColumnType("longtext");
                entity.Property(e => e.ResultFormat).HasMaxLength(64).HasDefaultValue(AiJobResultFormats.MarkdownSectionsV1);
                entity.Property(e => e.StructuredResult).HasConversion(structuredResultConverter).HasColumnType("json");
                entity.Property(e => e.Error).HasColumnType("longtext");
                entity.Property(e => e.ErrorInfo).HasConversion(errorInfoConverter).HasColumnType("json");
                entity.Property(e => e.Debug).HasDefaultValue(false);
                entity.Property(e => e.DebugInfo).HasConversion(debugInfoConverter).HasColumnType("json");
                entity.Property(e => e.LastError).HasColumnType("longtext");
                entity.Property(e => e.WorkerHost).HasMaxLength(100);
                entity.HasIndex(e => new { e.OsintInvestigationId, e.CreatedAt });
                entity.HasIndex(e => e.Status);
            });
        }
    }
}
