
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Collections.Specialized;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);
builder.Services.AddHttpClient(); // Register HttpClient for dependency injection
builder.Services.AddHostedService<TimedHostedService>();
builder.Services.AddScoped<CameraService>();
builder.Services.AddScoped<CountDataService>();
builder.Services.AddScoped<ScheduleService>();


var app = builder.Build();

// Add database seeding or data access here (reading Camera data)
using (var scope = app.Services.CreateScope()) {
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
    // Query and read Camera data
    var cameras = dbContext.Cameras.ToList();
    // Print the cameras to the console
    foreach (var camera in cameras) {
        Console.WriteLine($"CameraID: {camera.CameraID}, CameraName: {camera.CameraName}");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();

