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

    // FIXED: Enhanced GetCountTotalsFilteredAsync with better error handling and logging
    public async Task<CountTotals> GetCountTotalsFilteredAsync(
        List<int> cameraIDs, long fromTime, long toTime) 
    {
        try 
        {
            if (cameraIDs == null || !cameraIDs.Any())
            {
                Console.WriteLine("[COUNT DATA SERVICE] No camera IDs provided");
                return new CountTotals();
            }

            if (fromTime >= toTime)
            {
                Console.WriteLine($"[COUNT DATA SERVICE] Invalid time range: from={fromTime}, to={toTime}");
                return new CountTotals();
            }

            Console.WriteLine($"[COUNT DATA SERVICE] GetCountTotalsFiltered - Cameras: [{string.Join(", ", cameraIDs)}], From: {fromTime}, To: {toTime}");
            Console.WriteLine($"[COUNT DATA SERVICE] Time range: {DateTimeOffset.FromUnixTimeSeconds(fromTime)} to {DateTimeOffset.FromUnixTimeSeconds(toTime)}");
        
            var totals = await _context.CountData
                .AsNoTracking()
                .Where(cd =>
                    cameraIDs.Contains(cd.CameraId) &&
                    cd.StartTime >= fromTime &&
                    cd.StartTime < toTime) // Changed from EndTime <= toTime to StartTime < toTime for better accuracy
                .GroupBy(_ => 1)
                .Select(g => new CountTotals
                {
                    TotalIn = g.Sum(cd => cd.In),
                    TotalOut = g.Sum(cd => cd.Out)
                })
                .FirstOrDefaultAsync();

            var result = totals ?? new CountTotals();
            
            Console.WriteLine($"[COUNT DATA SERVICE] Query result - In: {result.TotalIn}, Out: {result.TotalOut}");
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[COUNT DATA SERVICE ERROR] GetCountTotalsFilteredAsync failed: {ex.Message}");
            Console.WriteLine($"[COUNT DATA SERVICE ERROR] Stack trace: {ex.StackTrace}");
            return new CountTotals(); // Return safe defaults
        }
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

    // ENHANCED: Get current people count with better error handling
    public async Task<int> GetCurrentPeopleCountAsync(List<int> cameraIDs)
    {
        try
        {
            if (cameraIDs == null || !cameraIDs.Any())
            {
                Console.WriteLine("[COUNT DATA SERVICE] No camera IDs provided for current count");
                return 0;
            }

            // Get all data from the beginning of today until now
            var today = DateTime.Today;
            var startOfDay = ((DateTimeOffset)today.ToUniversalTime()).ToUnixTimeSeconds();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            Console.WriteLine($"[COUNT DATA SERVICE] Getting current count for cameras: [{string.Join(", ", cameraIDs)}]");
            
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
            {
                Console.WriteLine("[COUNT DATA SERVICE] No current count data found");
                return 0;
            }
            
            // Current count = Total In - Total Out
            var currentCount = Math.Max(0, totalCounts.TotalIn - totalCounts.TotalOut);
            Console.WriteLine($"[COUNT DATA SERVICE] Current count calculated: {currentCount} (In: {totalCounts.TotalIn}, Out: {totalCounts.TotalOut})");
            
            return currentCount;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[COUNT DATA SERVICE ERROR] GetCurrentPeopleCountAsync failed: {ex.Message}");
            return 0;
        }
    }

    // ENHANCED: Get people count for a specific time range with validation
    public async Task<int> GetPeopleCountAtTimeAsync(List<int> cameraIDs, long fromTime, long toTime)
    {
        try
        {
            if (cameraIDs == null || !cameraIDs.Any())
            {
                Console.WriteLine("[COUNT DATA SERVICE] No camera IDs provided for time range count");
                return 0;
            }

            if (fromTime >= toTime)
            {
                Console.WriteLine($"[COUNT DATA SERVICE] Invalid time range for count: from={fromTime}, to={toTime}");
                return 0;
            }

            Console.WriteLine($"[COUNT DATA SERVICE] Getting count for time range - Cameras: [{string.Join(", ", cameraIDs)}], From: {fromTime}, To: {toTime}");
            
            var totalCounts = await _context.CountData
                .AsNoTracking()
                .Where(cd => 
                    cameraIDs.Contains(cd.CameraId) &&
                    cd.StartTime >= fromTime && 
                    cd.StartTime < toTime)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalIn = g.Sum(cd => cd.In),
                    TotalOut = g.Sum(cd => cd.Out)
                })
                .FirstOrDefaultAsync();
            
            if (totalCounts == null)
            {
                Console.WriteLine("[COUNT DATA SERVICE] No count data found for time range");
                return 0;
            }
            
            var result = Math.Max(0, totalCounts.TotalIn - totalCounts.TotalOut);
            Console.WriteLine($"[COUNT DATA SERVICE] Time range count calculated: {result} (In: {totalCounts.TotalIn}, Out: {totalCounts.TotalOut})");
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[COUNT DATA SERVICE ERROR] GetPeopleCountAtTimeAsync failed: {ex.Message}");
            return 0;
        }
    }

    // NEW: Get hourly breakdown data for analytics
    public async Task<List<HourlyData>> GetHourlyDataAsync(List<int> cameraIDs, DateTime date)
    {
        try
        {
            if (cameraIDs == null || !cameraIDs.Any())
            {
                Console.WriteLine("[COUNT DATA SERVICE] No camera IDs provided for hourly data");
                return new List<HourlyData>();
            }

            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);
            
            var startUnix = ((DateTimeOffset)startOfDay.ToUniversalTime()).ToUnixTimeSeconds();
            var endUnix = ((DateTimeOffset)endOfDay.ToUniversalTime()).ToUnixTimeSeconds();
            
            Console.WriteLine($"[COUNT DATA SERVICE] Getting hourly data for {date:yyyy-MM-dd}");
            
            var hourlyData = new List<HourlyData>();
            
            for (int hour = 0; hour < 24; hour++)
            {
                var hourStart = startOfDay.AddHours(hour);
                var hourEnd = hourStart.AddHours(1);
                
                var hourStartUnix = ((DateTimeOffset)hourStart.ToUniversalTime()).ToUnixTimeSeconds();
                var hourEndUnix = ((DateTimeOffset)hourEnd.ToUniversalTime()).ToUnixTimeSeconds();
                
                var hourTotals = await _context.CountData
                    .AsNoTracking()
                    .Where(cd => 
                        cameraIDs.Contains(cd.CameraId) &&
                        cd.StartTime >= hourStartUnix && 
                        cd.StartTime < hourEndUnix)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        TotalIn = g.Sum(cd => cd.In),
                        TotalOut = g.Sum(cd => cd.Out)
                    })
                    .FirstOrDefaultAsync();
                
                hourlyData.Add(new HourlyData
                {
                    Hour = hour,
                    HourDisplay = hourStart.ToString("HH:mm"),
                    PeopleIn = hourTotals?.TotalIn ?? 0,
                    PeopleOut = hourTotals?.TotalOut ?? 0,
                    Timestamp = hourStart
                });
            }
            
            Console.WriteLine($"[COUNT DATA SERVICE] Generated hourly data for {hourlyData.Count} hours");
            return hourlyData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[COUNT DATA SERVICE ERROR] GetHourlyDataAsync failed: {ex.Message}");
            return new List<HourlyData>();
        }
    }

    // NEW: Get interval data for charts (e.g., last hour in 10-minute intervals)
    public async Task<List<IntervalData>> GetIntervalDataAsync(List<int> cameraIDs, DateTime startTime, int intervalMinutes, int numberOfIntervals)
    {
        try
        {
            if (cameraIDs == null || !cameraIDs.Any())
            {
                Console.WriteLine("[COUNT DATA SERVICE] No camera IDs provided for interval data");
                return new List<IntervalData>();
            }

            Console.WriteLine($"[COUNT DATA SERVICE] Getting interval data - {numberOfIntervals} intervals of {intervalMinutes} minutes each");
            
            var intervalData = new List<IntervalData>();
            
            for (int i = 0; i < numberOfIntervals; i++)
            {
                var intervalStart = startTime.AddMinutes(i * intervalMinutes);
                var intervalEnd = intervalStart.AddMinutes(intervalMinutes);
                
                var startUnix = ((DateTimeOffset)intervalStart).ToUnixTimeSeconds();
                var endUnix = ((DateTimeOffset)intervalEnd).ToUnixTimeSeconds();
                
                var intervalTotals = await _context.CountData
                    .AsNoTracking()
                    .Where(cd => 
                        cameraIDs.Contains(cd.CameraId) &&
                        cd.StartTime >= startUnix && 
                        cd.StartTime < endUnix)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        TotalIn = g.Sum(cd => cd.In),
                        TotalOut = g.Sum(cd => cd.Out)
                    })
                    .FirstOrDefaultAsync();
                
                intervalData.Add(new IntervalData
                {
                    IntervalStart = intervalStart,
                    IntervalEnd = intervalEnd,
                    IntervalDisplay = intervalStart.ToString("HH:mm"),
                    PeopleIn = intervalTotals?.TotalIn ?? 0,
                    PeopleOut = intervalTotals?.TotalOut ?? 0
                });
            }
            
            return intervalData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[COUNT DATA SERVICE ERROR] GetIntervalDataAsync failed: {ex.Message}");
            return new List<IntervalData>();
        }
    }

    // NEW: Get summary statistics for a date range
    public async Task<CountSummary> GetCountSummaryAsync(List<int> cameraIDs, long fromTime, long toTime)
    {
        try
        {
            if (cameraIDs == null || !cameraIDs.Any())
            {
                Console.WriteLine("[COUNT DATA SERVICE] No camera IDs provided for summary");
                return new CountSummary();
            }

            Console.WriteLine($"[COUNT DATA SERVICE] Getting count summary for time range");
            
            var data = await _context.CountData
                .AsNoTracking()
                .Where(cd => 
                    cameraIDs.Contains(cd.CameraId) &&
                    cd.StartTime >= fromTime && 
                    cd.StartTime < toTime)
                .ToListAsync();
            
            if (!data.Any())
            {
                return new CountSummary();
            }
            
            var summary = new CountSummary
            {
                TotalEntries = data.Count,
                TotalIn = data.Sum(cd => cd.In),
                TotalOut = data.Sum(cd => cd.Out),
                AverageInPerEntry = data.Count > 0 ? (double)data.Sum(cd => cd.In) / data.Count : 0,
                MaxInSingleEntry = data.Max(cd => cd.In),
                MinInSingleEntry = data.Min(cd => cd.In),
                FirstEntryTime = data.Min(cd => cd.StartTime),
                LastEntryTime = data.Max(cd => cd.StartTime)
            };
            
            summary.CurrentPresent = Math.Max(0, summary.TotalIn - summary.TotalOut);
            
            Console.WriteLine($"[COUNT DATA SERVICE] Summary calculated: {summary.TotalEntries} entries, {summary.TotalIn} in, {summary.TotalOut} out");
            
            return summary;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[COUNT DATA SERVICE ERROR] GetCountSummaryAsync failed: {ex.Message}");
            return new CountSummary();
        }
    }
}

// Supporting classes for enhanced functionality
public class HourlyData
{
    public int Hour { get; set; }
    public string HourDisplay { get; set; } = string.Empty;
    public int PeopleIn { get; set; }
    public int PeopleOut { get; set; }
    public DateTime Timestamp { get; set; }
}

public class IntervalData
{
    public DateTime IntervalStart { get; set; }
    public DateTime IntervalEnd { get; set; }
    public string IntervalDisplay { get; set; } = string.Empty;
    public int PeopleIn { get; set; }
    public int PeopleOut { get; set; }
}

public class CountSummary
{
    public int TotalEntries { get; set; }
    public int TotalIn { get; set; }
    public int TotalOut { get; set; }
    public int CurrentPresent { get; set; }
    public double AverageInPerEntry { get; set; }
    public int MaxInSingleEntry { get; set; }
    public int MinInSingleEntry { get; set; }
    public long FirstEntryTime { get; set; }
    public long LastEntryTime { get; set; }
}