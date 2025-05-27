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
            modelBuilder.Entity<CountData>()
                    .HasKey(c => c.SrNo); // Define primary key explicitly (optional if [Key] attribute is used)
            modelBuilder.Entity<CountData>()
                    .Property(c => c.SrNo)
                    .ValueGeneratedOnAdd(); // Ensures auto-increment behavior
        });

        // Seed initial values for the Camera table
        modelBuilder.Entity<Camera>().HasData(
            new Camera { CameraID = 1, CameraName = "Kitchen Main Camera" },
            new Camera { CameraID = 2, CameraName = "Swadishta Camera" }
        );
    }
}
