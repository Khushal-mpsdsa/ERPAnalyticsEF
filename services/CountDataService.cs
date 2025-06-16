using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

public class CountDataService
{
    private readonly ApplicationDBContext _context;

    public CountDataService(ApplicationDBContext context)
    {
        _context = context;
    }

    public async Task SaveCountDataAsync(IEnumerable<CountData> countDataList)
    {
        foreach (var item in countDataList) {
            item.DateOnly = item.Date;
            item.TimeOnly = item.Time;
        }
        await _context.CountData.AddRangeAsync(countDataList);
        await _context.SaveChangesAsync();
    }

    // public List<CountData> GetCountDataByDateAndCamera(DateTime date, int cameraId)
    // {
    //     return _context.CountData
    //         .Where(cd => cd.DateOnly.Date == date.Date && cd.CameraId == cameraId)
    //         .OrderByDescending(cd => cd.TimeOnly) // Sorting by TimeOnly
    //         .ToList();
    // }

 public List<CountData> GetCountDataByDateAndCamera(DateTime date, int cameraId)
{
    // Ensure that the provided 'date' is in local time
    var localDate = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local);
    
    // Convert to UTC
    var startOfDay = localDate.ToUniversalTime();
    var endOfDay = startOfDay.AddDays(1);

    // Convert the DateTime values to Unix timestamps (UTC)
    long startTimestamp = ((DateTimeOffset)startOfDay).ToUnixTimeSeconds();
    long endTimestamp = ((DateTimeOffset)endOfDay).ToUnixTimeSeconds();

    // Query the database using StartTime in Unix timestamps and CameraId
    return _context.CountData
        .AsNoTracking() // Improves performance for read-only queries
        .Where(cd => cd.CameraId == cameraId && cd.StartTime >= startTimestamp && cd.StartTime < endTimestamp)
        .ToList();
}

    public async Task<CountTotals> GetCountTotalsFilteredAsync(
    List<int> cameraIDs, long fromTime, long toTime) {

        Console.WriteLine($"GetCount Timestamps: {fromTime} {toTime} CameraIds: {string.Join(", ", cameraIDs)}");
    
        var totals = await _context.CountData
        .Where(cd =>
            cameraIDs.Contains(cd.CameraId) &&
            cd.StartTime >= fromTime &&
            cd.EndTime <= toTime)
        .GroupBy(_ => 1)
        .Select(g => new CountTotals
        {
            TotalIn = g.Sum(cd => cd.In),
            TotalOut = g.Sum(cd => cd.Out)
        })
        .FirstOrDefaultAsync();
        return totals ?? new CountTotals(); // Return 0s if no match
    }
    
    public class CountTotals
    {
        public int TotalIn { get; set; }
        public int TotalOut { get; set; }

        // Optional: enable deconstruction
        public void Deconstruct(out int totalIn, out int totalOut)
        {
            totalIn = this.TotalIn;
            totalOut = this.TotalOut;
        }
    }
   

public async Task<int> GetCurrentPeopleCountAsync(List<int> cameraIDs)
{
    // Get all data from the beginning of today until now
    var today = DateTime.Today;
    var startOfDay = ((DateTimeOffset)today.ToUniversalTime()).ToUnixTimeSeconds();
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    
    var totalCounts = await _context.CountData
        .AsNoTracking()
        .Where(cd => 
            cameraIDs.Contains(cd.CameraId) &&
            cd.StartTime >= startOfDay && 
            cd.StartTime <= now)
        .GroupBy(_ => 1)
        .Select(g => new
        {
            TotalIn = g.Sum(cd => cd.In),
            TotalOut = g.Sum(cd => cd.Out)
        })
        .FirstOrDefaultAsync();
    
    if (totalCounts == null)
        return 0;
    
    // Current count = Total In - Total Out
    return Math.Max(0, totalCounts.TotalIn - totalCounts.TotalOut);
}

// Alternative: Get current count for a specific time range
public async Task<int> GetPeopleCountAtTimeAsync(List<int> cameraIDs, long fromTime, long toTime)
{
    var totalCounts = await _context.CountData
        .AsNoTracking()
        .Where(cd => 
            cameraIDs.Contains(cd.CameraId) &&
            cd.StartTime >= fromTime && 
            cd.StartTime <= toTime)
        .GroupBy(_ => 1)
        .Select(g => new
        {
            TotalIn = g.Sum(cd => cd.In),
            TotalOut = g.Sum(cd => cd.Out)
        })
        .FirstOrDefaultAsync();
    
    if (totalCounts == null)
        return 0;
    
    return Math.Max(0, totalCounts.TotalIn - totalCounts.TotalOut);
}
}