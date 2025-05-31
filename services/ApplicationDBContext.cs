using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;

public class ApplicationDBContext : DbContext {
    // Constructor to pass options to the base DbContext class
    public ApplicationDBContext(DbContextOptions<ApplicationDBContext> options)
        : base(options) { 
    }

    public DbSet<Camera> Cameras { get; set; }
    public DbSet<CountData> CountData { get; set; }
    public DbSet<Schedule> Schedules { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        // Configure Camera entity
        modelBuilder.Entity<Camera>(entity =>
        {
            // Set CameraID as the primary key
            entity.HasKey(c => c.CameraID);
            // Set CameraName as required and with a maximum length of 100 characters
            entity.Property(c => c.CameraName)
                .IsRequired()
                .HasMaxLength(100);
        });

        // Configure CountData entity
        modelBuilder.Entity<CountData>(entity =>
        {
            entity.HasKey(c => c.SrNo); // Define primary key explicitly
            entity.Property(c => c.SrNo)
                .ValueGeneratedOnAdd(); // Ensures auto-increment behavior
        });

        // Configure Schedule entity
        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(s => s.ScheduleID);
            entity.Property(s => s.ScheduleID)
                .ValueGeneratedOnAdd(); // Auto-increment
            entity.Property(s => s.ScheduleName)
                .HasMaxLength(200);
            
            // Add foreign key relationship to Camera if needed
            entity.HasOne<Camera>()
                .WithMany()
                .HasForeignKey(s => s.CameraID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed initial values for the Camera table
        modelBuilder.Entity<Camera>().HasData(
            new Camera { CameraID = 1, CameraName = "Kitchen Main Camera", RefreshRateInSeconds = 0, LastRefreshTimestamp = DateTime.MinValue },
            new Camera { CameraID = 2, CameraName = "Swadishta Camera", RefreshRateInSeconds = 0, LastRefreshTimestamp = DateTime.MinValue }
        );

        // Seed initial schedules for testing
        modelBuilder.Entity<Schedule>().HasData(
            new Schedule { ScheduleID = 1, CameraID = 1, ScheduleName = "Morning Shift", StartTime = DateTime.Today.AddHours(9), DurationInSec = 3600 },
            new Schedule { ScheduleID = 2, CameraID = 1, ScheduleName = "Lunch Break", StartTime = DateTime.Today.AddHours(12), DurationInSec = 1800 },
            new Schedule { ScheduleID = 3, CameraID = 2, ScheduleName = "Evening Shift", StartTime = DateTime.Today.AddHours(17), DurationInSec = 3600 }
        );
    }
}