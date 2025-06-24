using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

public class IndexModel(CameraService cameraService, CountDataService countDataService, ScheduleService scheduleService) : PageModel
{
    private readonly CameraService _cameraService = cameraService;
    private readonly CountDataService _countDataService = countDataService;
    private readonly ScheduleService _scheduleService = scheduleService;

    // Properties to hold data for the view
    public List<Camera> Cameras { get; set; } = new List<Camera>();
    public List<Schedule> AllSchedules { get; set; } = new List<Schedule>();

    // Dashboard metrics
    public int TotalCameras { get; set; }
    public int ActiveCameras { get; set; }
    public int TotalIn { get; set; }
    public int TotalOut { get; set; }
    public int TotalPresent { get; set; }

    // Schedule-based properties
    public string CurrentScheduleName { get; set; } = "No Active Schedule";
    public Schedule? CurrentActiveSchedule { get; set; }
    public bool HasActiveSchedule { get; set; } = false;
    
    public int LastFiveMinutesIn { get; set; }
    public int LastFiveMinutesOut { get; set; }
    public int LastFiveMinutesPresent { get; set; }

    public async Task OnGetAsync()
    {
        // Load cameras
        Cameras = _cameraService.GetCameras();

        // Load all schedules for dropdowns
        AllSchedules = new List<Schedule>();
        foreach (var camera in Cameras)
        {
            var schedules = _scheduleService.GetSchedules(camera.CameraID);
            AllSchedules.AddRange(schedules);
        }

        // Calculate dashboard metrics
        TotalCameras = Cameras.Count;
        ActiveCameras = Cameras.Count(c => c.RefreshRateInSeconds > 0);

        // Check for current active schedule
        var activeSchedule = GetCurrentActiveSchedule();
        if (activeSchedule != null)
        {
            HasActiveSchedule = true;
            CurrentActiveSchedule = activeSchedule;
            CurrentScheduleName = activeSchedule.ScheduleName ?? "Active Schedule";

            // Get data only for the active schedule timeframe and camera
            var now = DateTime.Now;
            var today = now.Date;
            var scheduleStartToday = new DateTime(
                today.Year, today.Month, today.Day,
                activeSchedule.StartTime.Hour,
                activeSchedule.StartTime.Minute,
                activeSchedule.StartTime.Second
            );
            var scheduleStartUnix = ((DateTimeOffset)scheduleStartToday.ToUniversalTime()).ToUnixTimeSeconds();
            var nowUnix = ((DateTimeOffset)now.ToUniversalTime()).ToUnixTimeSeconds();

            var scheduleData = await _countDataService.GetCountTotalsFilteredAsync(
                new List<int> { activeSchedule.CameraID },
                scheduleStartUnix,
                nowUnix
            );

            TotalIn = scheduleData.TotalIn;
            TotalOut = scheduleData.TotalOut;
            TotalPresent = Math.Max(0, TotalIn - TotalOut);

            // NEW: Get last 5 minutes data
            var fiveMinutesAgoUnix = ((DateTimeOffset)now.AddMinutes(-5).ToUniversalTime()).ToUnixTimeSeconds();
            var lastFiveMinutesData = await _countDataService.GetCountTotalsFilteredAsync(
                new List<int> { activeSchedule.CameraID },
                fiveMinutesAgoUnix,
                nowUnix
            );

            LastFiveMinutesIn = lastFiveMinutesData.TotalIn;
            LastFiveMinutesOut = lastFiveMinutesData.TotalOut;
            LastFiveMinutesPresent = Math.Max(0, LastFiveMinutesIn - LastFiveMinutesOut);
        }
        else
        {
            // No active schedule - show default values
            HasActiveSchedule = false;
            CurrentScheduleName = "No Active Schedule";
            TotalIn = 0;
            TotalOut = 0;
            TotalPresent = 0;

            // Last 5 minutes also zero when no active schedule
            LastFiveMinutesIn = 0;
            LastFiveMinutesOut = 0;
            LastFiveMinutesPresent = 0;
        }
    }

public async Task<JsonResult> OnGetLastFiveMinutesDataAsync()
{
    try
    {
        // Load all schedules for detection
        var cameras = _cameraService.GetCameras();
        var allSchedules = new List<Schedule>();
        foreach (var camera in cameras)
        {
            var schedules = _scheduleService.GetSchedules(camera.CameraID);
            allSchedules.AddRange(schedules);
        }

        var activeSchedule = GetCurrentActiveScheduleFromList(allSchedules);
        var lastFiveMinutesData = new { In = 0, Out = 0, Present = 0 };

        if (activeSchedule != null)
        {
            var now = DateTime.Now;
            var fiveMinutesAgo = now.AddMinutes(-5);
            
            var fiveMinutesAgoUnix = ((DateTimeOffset)fiveMinutesAgo.ToUniversalTime()).ToUnixTimeSeconds();
            var nowUnix = ((DateTimeOffset)now.ToUniversalTime()).ToUnixTimeSeconds();

            var totals = await _countDataService.GetCountTotalsFilteredAsync(
                new List<int> { activeSchedule.CameraID }, 
                fiveMinutesAgoUnix, 
                nowUnix
            );
            
            var presentCount = Math.Max(0, totals.TotalIn - totals.TotalOut);
            
            lastFiveMinutesData = new 
            { 
                In = totals.TotalIn, 
                Out = totals.TotalOut, 
                Present = presentCount 
            };

            Console.WriteLine($"[LAST 5 MIN] Schedule: {activeSchedule.ScheduleName}, In: {totals.TotalIn}, Out: {totals.TotalOut}, Present: {presentCount}");
        }
        else
        {
            Console.WriteLine($"[LAST 5 MIN] No active schedule");
        }
        
        return new JsonResult(new
        {
            success = true,
            data = lastFiveMinutesData,
            timeWindow = "Last 5 minutes",
            activeSchedule = activeSchedule?.ScheduleName ?? "No Active Schedule",
            hasActiveSchedule = activeSchedule != null,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[LAST 5 MIN ERROR] {ex.Message}");
        return new JsonResult(new
        {
            success = false,
            error = ex.Message,
            data = new { In = 0, Out = 0, Present = 0 }
        });
    }
}


    // Helper method to find current active schedule
    // Add this method to your IndexModel class for debugging
    private Schedule? GetCurrentActiveSchedule()
    {
        var now = DateTime.Now;
        var today = now.Date;

        Console.WriteLine($"[DEBUG] Current DateTime: {now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"[DEBUG] Today's Date: {today:yyyy-MM-dd}");

        foreach (var schedule in AllSchedules)
        {
            Console.WriteLine($"\n[DEBUG] Checking Schedule: {schedule.ScheduleName}");
            Console.WriteLine($"[DEBUG] Schedule.StartTime from DB: {schedule.StartTime:yyyy-MM-dd HH:mm:ss}");

            // Convert schedule start time to today's date with the same time
            var scheduleTimeToday = new DateTime(
                today.Year,
                today.Month,
                today.Day,
                schedule.StartTime.Hour,
                schedule.StartTime.Minute,
                schedule.StartTime.Second
            );

            var scheduleEndTime = scheduleTimeToday.AddSeconds(schedule.DurationInSec);

            Console.WriteLine($"[DEBUG] Schedule Start Today: {scheduleTimeToday:HH:mm:ss}");
            Console.WriteLine($"[DEBUG] Schedule End Today: {scheduleEndTime:HH:mm:ss}");
            Console.WriteLine($"[DEBUG] Duration: {schedule.DurationInSec} seconds");
            Console.WriteLine($"[DEBUG] Now >= Start: {now >= scheduleTimeToday}");
            Console.WriteLine($"[DEBUG] Now <= End: {now <= scheduleEndTime}");
            Console.WriteLine($"[DEBUG] Is Active: {now >= scheduleTimeToday && now <= scheduleEndTime}");

            // Check if current time is within the schedule window
            if (now >= scheduleTimeToday && now <= scheduleEndTime)
            {
                Console.WriteLine($"[DEBUG] *** ACTIVE SCHEDULE FOUND: {schedule.ScheduleName} ***");
                return schedule;
            }
        }

        Console.WriteLine($"[DEBUG] No active schedule found at {now:HH:mm:ss}");
        return null;
    }

    // API endpoint for getting people count
    public async Task<JsonResult> OnGetGetPeopleCountAsync([FromQuery] List<int> cameraIds,
    [FromQuery] long from,
    [FromQuery] long to)
{
    var totals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, from, to);
    int totalPresent = Math.Max(0, totals.TotalIn - totals.TotalOut); // Zero check added

    return new JsonResult(new
    {
        totalIn = totals.TotalIn,
        totalOut = totals.TotalOut,
        totalPresent = totalPresent
    });
}
    // API endpoint for getting schedules by camera
    public async Task<JsonResult> OnGetGetSchedulesAsync([FromQuery] int cameraId)
    {
        var schedules = _scheduleService.GetSchedules(cameraId);
        return new JsonResult(schedules);
    }

    // API endpoint for getting schedule by ID
    public async Task<JsonResult> OnGetGetScheduleByIDAsync([FromQuery] int scheduleID)
    {
        var schedule = _scheduleService.GetScheduleByID(scheduleID);
        return new JsonResult(schedule);
    }

    // API endpoint for getting system overview data
    public async Task<JsonResult> OnGetSystemOverviewAsync()
{
    var cameras = _cameraService.GetCameras();
    var totalCameras = cameras.Count;
    var activeCameras = cameras.Count(c => c.RefreshRateInSeconds > 0);

    var allSchedules = new List<Schedule>();
    foreach (var camera in cameras)
    {
        var schedules = _scheduleService.GetSchedules(camera.CameraID);
        allSchedules.AddRange(schedules);
    }

    var activeSchedule = GetCurrentActiveScheduleFromList(allSchedules);
    var todayTotals = new CountDataService.CountTotals();
    var lastFiveMinutesTotals = new CountDataService.CountTotals();

    if (activeSchedule != null)
    {
        var now = DateTime.Now;
        var today = now.Date;
        var scheduleStartToday = new DateTime(
            today.Year, today.Month, today.Day,
            activeSchedule.StartTime.Hour,
            activeSchedule.StartTime.Minute,
            activeSchedule.StartTime.Second
        );
        var scheduleStartUnix = ((DateTimeOffset)scheduleStartToday.ToUniversalTime()).ToUnixTimeSeconds();
        var nowUnix = ((DateTimeOffset)now.ToUniversalTime()).ToUnixTimeSeconds();

        // Get total data from schedule start to now
        todayTotals = await _countDataService.GetCountTotalsFilteredAsync(
            new List<int> { activeSchedule.CameraID }, 
            scheduleStartUnix, 
            nowUnix
        );

        // Get last 5 minutes data
        var fiveMinutesAgoUnix = ((DateTimeOffset)now.AddMinutes(-5).ToUniversalTime()).ToUnixTimeSeconds();
        lastFiveMinutesTotals = await _countDataService.GetCountTotalsFilteredAsync(
            new List<int> { activeSchedule.CameraID }, 
            fiveMinutesAgoUnix, 
            nowUnix
        );

        Console.WriteLine($"[API DEBUG] Active schedule: {activeSchedule.ScheduleName}");
        Console.WriteLine($"[API DEBUG] Total data - In: {todayTotals.TotalIn}, Out: {todayTotals.TotalOut}");
        Console.WriteLine($"[API DEBUG] Last 5 min - In: {lastFiveMinutesTotals.TotalIn}, Out: {lastFiveMinutesTotals.TotalOut}");
    }

    var peopleIn = todayTotals.TotalIn;
    var peopleOut = todayTotals.TotalOut;
    var totalPresent = Math.Max(0, peopleIn - peopleOut);
    
    var lastFiveMinIn = lastFiveMinutesTotals.TotalIn;
    var lastFiveMinOut = lastFiveMinutesTotals.TotalOut;
    var lastFiveMinPresent = Math.Max(0, lastFiveMinIn - lastFiveMinOut);

    return new JsonResult(new
    {
        totalCameras,
        totalCameraCapacity = 32,
        activeCameras,
        peopleIn = peopleIn,
        peopleInCapacity = 165,
        peopleOut = peopleOut,
        totalCamerasTrend = 4,
        activeCamerasTrend = 0,
        peopleInChangeTrend = -12,
        peopleOutTrend = 8,
        currentScheduleName = activeSchedule?.ScheduleName ?? "No Active Schedule",
        hasActiveSchedule = activeSchedule != null,
        totalPresent = totalPresent,
        // NEW: Last 5 minutes data
        lastFiveMinutesIn = lastFiveMinIn,
        lastFiveMinutesOut = lastFiveMinOut,
        lastFiveMinutesPresent = lastFiveMinPresent
    });
}

    // API endpoint for getting camera data
    public async Task<JsonResult> OnGetCamerasDataAsync()
    {
        var cameras = _cameraService.GetCameras();
        var cameraData = cameras.Select(c => new
        {
            id = c.CameraID,
            name = c.CameraName,
            status = c.RefreshRateInSeconds > 0 ? "active" : "inactive",
            refreshRate = c.RefreshRateInSeconds,
            lastRefresh = c.LastRefreshTimestamp,
            location = new { x = 10 + (c.CameraID * 15) % 80, y = 20 + (c.CameraID * 25) % 60 }
        });

        return new JsonResult(cameraData);
    }

    // API endpoint for getting hourly activity data
    public async Task<JsonResult> OnGetActivityDataAsync([FromQuery] List<int> cameraIds, [FromQuery] DateTime date)
    {
        var hourlyData = new List<object>();

        for (int hour = 7; hour < 16; hour++) // 7 AM to 3 PM
        {
            var startTime = new DateTime(date.Year, date.Month, date.Day, hour, 0, 0);
            var endTime = startTime.AddHours(1);

            var startUnix = ((DateTimeOffset)startTime.ToUniversalTime()).ToUnixTimeSeconds();
            var endUnix = ((DateTimeOffset)endTime.ToUniversalTime()).ToUnixTimeSeconds();

            var totals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, startUnix, endUnix);

            hourlyData.Add(new
            {
                hour = startTime.ToString("HH:mm"),
                peopleIn = totals.TotalIn,
                peopleOut = totals.TotalOut
            });
        }

        return new JsonResult(hourlyData);
    }

    public async Task<JsonResult> OnGetCurrentPeopleCountAsync([FromQuery] List<int> cameraIds)
{
    try
    {
        // Load all schedules for detection
        var cameras = _cameraService.GetCameras();
        var allSchedules = new List<Schedule>();
        foreach (var camera in cameras)
        {
            var schedules = _scheduleService.GetSchedules(camera.CameraID);
            allSchedules.AddRange(schedules);
        }

        var activeSchedule = GetCurrentActiveScheduleFromList(allSchedules);
        int currentCount = 0;

        if (activeSchedule != null)
        {
            // Get count for the active schedule only
            var now = DateTime.Now;
            var today = now.Date;
            var scheduleStartToday = new DateTime(
                today.Year, today.Month, today.Day,
                activeSchedule.StartTime.Hour,
                activeSchedule.StartTime.Minute,
                activeSchedule.StartTime.Second
            );
            var scheduleStartUnix = ((DateTimeOffset)scheduleStartToday.ToUniversalTime()).ToUnixTimeSeconds();
            var nowUnix = ((DateTimeOffset)now.ToUniversalTime()).ToUnixTimeSeconds();

            var scheduleTotals = await _countDataService.GetCountTotalsFilteredAsync(
                new List<int> { activeSchedule.CameraID }, 
                scheduleStartUnix, 
                nowUnix
            );
            
            var rawCount = scheduleTotals.TotalIn - scheduleTotals.TotalOut;
            currentCount = Math.Max(0, rawCount); // Zero check added
            
            Console.WriteLine($"[COUNT API DEBUG] Active schedule: {activeSchedule.ScheduleName}");
            Console.WriteLine($"[COUNT API DEBUG] Raw count: {rawCount}, Final count: {currentCount}");
        }
        else
        {
            Console.WriteLine($"[COUNT API DEBUG] No active schedule, count = 0");
        }
        
        return new JsonResult(new
        {
            success = true,
            currentCount = currentCount,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            activeSchedule = activeSchedule?.ScheduleName ?? "No Active Schedule",
            hasActiveSchedule = activeSchedule != null
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[COUNT API ERROR] {ex.Message}");
        return new JsonResult(new
        {
            success = false,
            error = ex.Message,
            currentCount = 0
        });
    }
}

    // API endpoint for historical count at specific time
    public async Task<JsonResult> OnGetPeopleCountAtTimeAsync(
    [FromQuery] List<int> cameraIds,
    [FromQuery] long fromTime,
    [FromQuery] long toTime)
{
    try
    {
        if (cameraIds == null || !cameraIds.Any())
        {
            cameraIds = _cameraService.GetCameras().Select(c => c.CameraID).ToList();
        }

        var totals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, fromTime, toTime);
        var count = Math.Max(0, totals.TotalIn - totals.TotalOut); // Zero check added
        
        return new JsonResult(new
        {
            success = true,
            count = count,
            totalIn = totals.TotalIn,
            totalOut = totals.TotalOut,
            fromTime = fromTime,
            toTime = toTime
        });
    }
    catch (Exception ex)
    {
        return new JsonResult(new
        {
            success = false,
            error = ex.Message,
            count = 0
        });
    }
}

    // Helper method to get current active schedule from a list
    private Schedule? GetCurrentActiveScheduleFromList(List<Schedule> allSchedules)
{
    var now = DateTime.Now;
    var today = now.Date;

    Console.WriteLine($"[SCHEDULE DEBUG] Current time: {now:HH:mm:ss}");
    Console.WriteLine($"[SCHEDULE DEBUG] Checking {allSchedules.Count} schedules");

    foreach (var schedule in allSchedules)
    {
        // Convert schedule start time to today's date with the same time
        var scheduleTimeToday = new DateTime(
            today.Year, 
            today.Month, 
            today.Day,
            schedule.StartTime.Hour,
            schedule.StartTime.Minute,
            schedule.StartTime.Second
        );

        var scheduleEndTime = scheduleTimeToday.AddSeconds(schedule.DurationInSec);

        Console.WriteLine($"[SCHEDULE DEBUG] {schedule.ScheduleName}: {scheduleTimeToday:HH:mm:ss} - {scheduleEndTime:HH:mm:ss}");
        Console.WriteLine($"[SCHEDULE DEBUG] Is active: {now >= scheduleTimeToday && now <= scheduleEndTime}");

        // Check if current time is within the schedule window
        if (now >= scheduleTimeToday && now <= scheduleEndTime)
        {
            Console.WriteLine($"[SCHEDULE DEBUG] *** FOUND ACTIVE: {schedule.ScheduleName} ***");
            return schedule;
        }
    }

    Console.WriteLine($"[SCHEDULE DEBUG] No active schedule found");
    return null;
}


    // API endpoint to get current schedule status
    public async Task<JsonResult> OnGetCurrentScheduleStatusAsync()
    {
        try
        {
            // Load all schedules
            var cameras = _cameraService.GetCameras();
            var allSchedules = new List<Schedule>();
            foreach (var camera in cameras)
            {
                var schedules = _scheduleService.GetSchedules(camera.CameraID);
                allSchedules.AddRange(schedules);
            }

            var activeSchedule = GetCurrentActiveScheduleFromList(allSchedules);

            Console.WriteLine($"[STATUS API DEBUG] Checking schedule status at {DateTime.Now:HH:mm:ss}");
            Console.WriteLine($"[STATUS API DEBUG] Total schedules to check: {allSchedules.Count}");

            if (activeSchedule != null)
            {
                var now = DateTime.Now;
                var today = now.Date;
                var scheduleStart = new DateTime(
                    today.Year, today.Month, today.Day,
                    activeSchedule.StartTime.Hour,
                    activeSchedule.StartTime.Minute,
                    activeSchedule.StartTime.Second
                );
                var scheduleEnd = scheduleStart.AddSeconds(activeSchedule.DurationInSec);
                var timeRemaining = scheduleEnd - now;

                Console.WriteLine($"[STATUS API DEBUG] Active schedule: {activeSchedule.ScheduleName}");
                Console.WriteLine($"[STATUS API DEBUG] Time remaining: {timeRemaining.TotalSeconds} seconds");

                return new JsonResult(new
                {
                    hasActiveSchedule = true,
                    scheduleName = activeSchedule.ScheduleName,
                    cameraName = _cameraService.GetCameraById(activeSchedule.CameraID)?.CameraName ?? "Unknown Camera",
                    timeRemaining = (int)Math.Max(0, timeRemaining.TotalSeconds), // Zero check added
                    scheduleStart = scheduleStart.ToString("HH:mm:ss"),
                    scheduleEnd = scheduleEnd.ToString("HH:mm:ss")
                });
            }
            else
            {
                Console.WriteLine($"[STATUS API DEBUG] No active schedule found");
                return new JsonResult(new
                {
                    hasActiveSchedule = false,
                    scheduleName = "No Active Schedule",
                    cameraName = "",
                    timeRemaining = 0
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[STATUS API ERROR] {ex.Message}");
            return new JsonResult(new
            {
                hasActiveSchedule = false,
                scheduleName = "Error loading schedule",
                error = ex.Message,
                timeRemaining = 0
            });
        }
    }
 public async Task<JsonResult> OnGetTestMathMaxAsync()
{
    int testIn = 5;
    int testOut = 10;
    int rawResult = testIn - testOut; // Should be -5
    int maxResult = Math.Max(0, rawResult); // Should be 0
    
    return new JsonResult(new
    {
        testIn = testIn,
        testOut = testOut,
        rawResult = rawResult,
        maxResult = maxResult,
        message = $"Raw: {rawResult}, After Math.Max: {maxResult}"
    });
}
}