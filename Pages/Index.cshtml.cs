using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class IndexModel : PageModel
{
    private readonly CameraService _cameraService;
    private readonly CountDataService _countDataService;
    private readonly ScheduleService _scheduleService;
    private readonly ApplicationDBContext _context;

    // FIXED: Proper constructor with all dependencies
    public IndexModel(CameraService cameraService, CountDataService countDataService, ScheduleService scheduleService, ApplicationDBContext context)
    {
        _cameraService = cameraService;
        _countDataService = countDataService;
        _scheduleService = scheduleService;
        _context = context;
    }

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

    // Queue properties (was Last 5 Minutes)
    public int LastFiveMinutesIn { get; set; }
    public int LastFiveMinutesOut { get; set; }
    public int LastFiveMinutesPresent { get; set; }

    public class DashboardData
    {
        public int TotalIn { get; set; }
        public int TotalOut { get; set; }
        public int TotalPresent { get; set; }
        public int LastFiveMinutesIn { get; set; }
        public int LastFiveMinutesOut { get; set; }
        public int LastFiveMinutesPresent { get; set; }
        public bool IsActive { get; set; }
        public bool HasData { get; set; }
        public string? ScheduleName { get; set; }
        public string? CameraName { get; set; }
    }

    public class DashboardOverview
    {
        public string Date { get; set; } = string.Empty;
        public bool IsToday { get; set; }
        public int TotalCameras { get; set; }
        public int ActiveCameras { get; set; }
        public bool HasSelectedSchedule { get; set; }
        public int? SelectedScheduleId { get; set; }
        public string? SelectedScheduleName { get; set; }
        public string? SelectedCameraName { get; set; }
        public DashboardData Data { get; set; } = new DashboardData();
    }

    public async Task OnGetAsync()
    {
        try
        {
            Console.WriteLine("[OnGet] Loading dashboard data...");
            
            // Load cameras with optimization
            Cameras = _cameraService.GetCameras();
            TotalCameras = Cameras.Count;
            ActiveCameras = Cameras.Count(c => c.RefreshRateInSeconds > 0);

            // Load current active schedule
            CurrentActiveSchedule = _scheduleService.GetCurrentActiveSchedule();
            HasActiveSchedule = CurrentActiveSchedule != null;
            CurrentScheduleName = CurrentActiveSchedule?.ScheduleName ?? "No Active Schedule";

            if (HasActiveSchedule && CurrentActiveSchedule != null)
            {
                var camera = _cameraService.GetCameraById(CurrentActiveSchedule.CameraID);
                var now = DateTime.Now;
                var today = now.Date;

                var scheduleTimeToday = new DateTime(
                    today.Year, today.Month, today.Day,
                    CurrentActiveSchedule.StartTime.Hour,
                    CurrentActiveSchedule.StartTime.Minute,
                    CurrentActiveSchedule.StartTime.Second
                );

                var scheduleEndTime = scheduleTimeToday.AddSeconds(CurrentActiveSchedule.DurationInSec);
                var scheduleStartUnix = ((DateTimeOffset)scheduleTimeToday).ToUnixTimeSeconds();
                var currentTimeUnix = ((DateTimeOffset)now).ToUnixTimeSeconds();

                if (camera != null)
                {
                    var cameraIds = new List<int> { camera.CameraID };
                    var totals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, scheduleStartUnix, currentTimeUnix);
                    
                    TotalIn = totals.TotalIn;
                    TotalOut = totals.TotalOut;
                    TotalPresent = Math.Max(0, totals.TotalIn - totals.TotalOut);

                    var lastFiveMinutesStart = ((DateTimeOffset)now.AddMinutes(-5)).ToUnixTimeSeconds();
                    var lastFiveMinutesTotals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, lastFiveMinutesStart, currentTimeUnix);
                    
                    LastFiveMinutesIn = lastFiveMinutesTotals.TotalIn;
                    LastFiveMinutesOut = lastFiveMinutesTotals.TotalOut;
                    LastFiveMinutesPresent = Math.Max(0, lastFiveMinutesTotals.TotalIn - lastFiveMinutesTotals.TotalOut);
                }
            }

            Console.WriteLine($"[OnGet] Dashboard loaded - Active Schedule: {CurrentScheduleName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnGet] Error loading dashboard: {ex.Message}");
        }
    }

    // ====================
    // OPTIMIZED API ENDPOINTS
    // ====================

    // NEW: OPTIMIZED - Single API call to get all schedules at once
    public async Task<JsonResult> OnGetGetAllSchedulesOptimizedAsync()
    {
        try
        {
            Console.WriteLine("[OPTIMIZED API] Starting single-query schedule load");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Use ScheduleService instead of direct context access
            var schedulesWithCamera = _scheduleService.GetAllSchedulesWithCameraInfo();

            stopwatch.Stop();
            Console.WriteLine($"[OPTIMIZED API] Loaded {schedulesWithCamera.Count} schedules in {stopwatch.ElapsedMilliseconds}ms");

            // Ensure proper JSON serialization
            var result = schedulesWithCamera.Select(s => new {
                scheduleID = s.ScheduleID,
                scheduleName = s.ScheduleName ?? "Unnamed Schedule",
                startTime = s.StartTime,
                durationInSec = s.DurationInSec,
                cameraID = s.CameraID,
                cameraName = s.CameraName ?? "Unknown Camera"
            }).ToList();

            return new JsonResult(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OPTIMIZED API ERROR] {ex.Message}");
            
            // FALLBACK: Return data using old method if optimized fails
            try 
            {
                var cameras = _cameraService.GetCameras();
                var allSchedules = new List<object>();

                foreach (var camera in cameras)
                {
                    var schedules = _scheduleService.GetSchedules(camera.CameraID);
                    foreach (var schedule in schedules)
                    {
                        allSchedules.Add(new
                        {
                            scheduleID = schedule.ScheduleID,
                            scheduleName = schedule.ScheduleName ?? "Unnamed Schedule",
                            startTime = schedule.StartTime,
                            durationInSec = schedule.DurationInSec,
                            cameraID = schedule.CameraID,
                            cameraName = camera.CameraName ?? "Unknown Camera"
                        });
                    }
                }

                Console.WriteLine($"[FALLBACK API] Loaded {allSchedules.Count} schedules using fallback method");
                return new JsonResult(allSchedules);
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"[FALLBACK ERROR] {fallbackEx.Message}");
                return new JsonResult(new { 
                    error = ex.Message, 
                    schedules = new List<object>(),
                    success = false 
                });
            }
        }
    }

    // API endpoint for getting people count
    public async Task<JsonResult> OnGetGetPeopleCountAsync([FromQuery] List<int> cameraIds,
    [FromQuery] long from,
    [FromQuery] long to)
    {
        var totals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, from, to);
        int totalPresent = Math.Max(0, totals.TotalIn - totals.TotalOut);

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

    // API endpoint for getting all schedules (for compatibility)
    public async Task<JsonResult> OnGetGetAllSchedulesAsync()
    {
        try
        {
            var allSchedules = await Task.Run(() => 
                _context.Schedules.AsNoTracking().ToList()
            );
            return new JsonResult(allSchedules);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API ERROR] {ex.Message}");
            return new JsonResult(new { error = ex.Message });
        }
    }

    // API endpoint for getting current active schedule
    public async Task<JsonResult> OnGetGetCurrentActiveScheduleAsync()
    {
        try
        {
            var allSchedules = await Task.Run(() => 
                _context.Schedules.AsNoTracking().ToList()
            );
            
            var activeSchedule = GetCurrentActiveScheduleFromList(allSchedules);

            if (activeSchedule != null)
            {
                var camera = _cameraService.GetCameraById(activeSchedule.CameraID);
                return new JsonResult(new
                {
                    success = true,
                    hasActiveSchedule = true,
                    schedule = activeSchedule,
                    camera = camera
                });
            }
            else
            {
                return new JsonResult(new
                {
                    success = true,
                    hasActiveSchedule = false,
                    schedule = (Schedule?)null,
                    camera = (Camera?)null
                });
            }
        }
        catch (Exception ex)
        {
            return new JsonResult(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    // API endpoint for current schedule status
    public async Task<JsonResult> OnGetCurrentScheduleStatusAsync()
    {
        try
        {
            var allSchedules = await Task.Run(() => 
                _context.Schedules.AsNoTracking().ToList()
            );
            
            var activeSchedule = GetCurrentActiveScheduleFromList(allSchedules);

            if (activeSchedule != null)
            {
                var camera = _cameraService.GetCameraById(activeSchedule.CameraID);
                var now = DateTime.Now;
                var today = now.Date;

                var scheduleTimeToday = new DateTime(
                    today.Year, today.Month, today.Day,
                    activeSchedule.StartTime.Hour,
                    activeSchedule.StartTime.Minute,
                    activeSchedule.StartTime.Second
                );

                var scheduleEndTime = scheduleTimeToday.AddSeconds(activeSchedule.DurationInSec);
                var timeRemaining = scheduleEndTime - now;

                return new JsonResult(new
                {
                    hasActiveSchedule = true,
                    scheduleName = activeSchedule.ScheduleName,
                    cameraName = camera?.CameraName ?? "Unknown Camera",
                    timeRemaining = (int)timeRemaining.TotalSeconds,
                    currentMealName = activeSchedule.ScheduleName,
                    isLunchTime = activeSchedule.ScheduleName?.ToLower().Contains("lunch") ?? false,
                    isBreakfastTime = activeSchedule.ScheduleName?.ToLower().Contains("breakfast") ?? false,
                    isDinnerTime = activeSchedule.ScheduleName?.ToLower().Contains("dinner") ?? false
                });
            }
            else
            {
                return new JsonResult(new
                {
                    hasActiveSchedule = false,
                    scheduleName = "No Active Schedule",
                    cameraName = "",
                    timeRemaining = 0,
                    currentMealName = "No Active Schedule",
                    isLunchTime = false,
                    isBreakfastTime = false,
                    isDinnerTime = false
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
                timeRemaining = 0,
                currentMealName = "Error loading schedule"
            });
        }
    }

    // FIXED: Enhanced System Overview with better error handling
    public async Task<JsonResult> OnGetSystemOverviewAsync()
    {
        try
        {
            var cameras = _cameraService.GetCameras();
            var totalCameras = cameras?.Count ?? 0;
            var activeCameras = cameras?.Count(c => c.RefreshRateInSeconds > 0) ?? 0;

            var activeSchedule = _scheduleService.GetCurrentActiveSchedule();

            int totalIn = 0, totalOut = 0, totalPresent = 0;
            int lastFiveMinutesIn = 0, lastFiveMinutesOut = 0, lastFiveMinutesPresent = 0;

            if (activeSchedule != null)
            {
                var camera = _cameraService.GetCameraById(activeSchedule.CameraID);
                if (camera != null)
                {
                    var now = DateTime.Now;
                    var today = now.Date;

                    var scheduleTimeToday = new DateTime(
                        today.Year, today.Month, today.Day,
                        activeSchedule.StartTime.Hour,
                        activeSchedule.StartTime.Minute,
                        activeSchedule.StartTime.Second
                    );

                    var scheduleStartUnix = ((DateTimeOffset)scheduleTimeToday).ToUnixTimeSeconds();
                    var currentTimeUnix = ((DateTimeOffset)now).ToUnixTimeSeconds();

                    try 
                    {
                        var cameraIds = new List<int> { camera.CameraID };
                        var totals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, scheduleStartUnix, currentTimeUnix);
                        
                        totalIn = totals?.TotalIn ?? 0;
                        totalOut = totals?.TotalOut ?? 0;
                        totalPresent = Math.Max(0, totalIn - totalOut);

                        var lastFiveMinutesStart = ((DateTimeOffset)now.AddMinutes(-5)).ToUnixTimeSeconds();
                        var lastFiveMinutesTotals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, lastFiveMinutesStart, currentTimeUnix);
                        
                        lastFiveMinutesIn = lastFiveMinutesTotals?.TotalIn ?? 0;
                        lastFiveMinutesOut = lastFiveMinutesTotals?.TotalOut ?? 0;
                        lastFiveMinutesPresent = Math.Max(0, lastFiveMinutesIn - lastFiveMinutesOut);
                    }
                    catch (Exception dataEx)
                    {
                        Console.WriteLine($"[SYSTEM OVERVIEW] Error getting count data: {dataEx.Message}");
                        // Keep default zero values
                    }
                }
            }

            var result = new
            {
                totalCameras = totalCameras,
                activeCameras = activeCameras,
                totalIn = totalIn,
                totalOut = totalOut,
                totalPresent = totalPresent,
                lastFiveMinutesIn = lastFiveMinutesIn,
                lastFiveMinutesOut = lastFiveMinutesOut,
                lastFiveMinutesPresent = lastFiveMinutesPresent,
                hasActiveSchedule = activeSchedule != null,
                currentScheduleName = activeSchedule?.ScheduleName ?? "No Active Schedule"
            };

            Console.WriteLine($"[SYSTEM OVERVIEW] Returning data: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return new JsonResult(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SYSTEM OVERVIEW ERROR] {ex.Message}");
            Console.WriteLine($"[SYSTEM OVERVIEW STACK] {ex.StackTrace}");
            
            return new JsonResult(new
            {
                error = ex.Message,
                totalCameras = 0,
                activeCameras = 0,
                totalIn = 0,
                totalOut = 0,
                totalPresent = 0,
                lastFiveMinutesIn = 0,
                lastFiveMinutesOut = 0,
                lastFiveMinutesPresent = 0,
                hasActiveSchedule = false,
                currentScheduleName = "Error loading data"
            });
        }
    }

    // API endpoint for schedule status for a specific date
    public async Task<JsonResult> OnGetGetScheduleStatusForDateAsync(
        [FromQuery] int scheduleId,
        [FromQuery] string date)
    {
        try
        {
            var targetDate = DateTime.Parse(date);
            var schedules = _scheduleService.GetScheduleByID(scheduleId);

            if (!schedules.Any())
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Schedule not found"
                });
            }

            var schedule = schedules.First();
            var status = GetScheduleStatusForDate(schedule, targetDate);

            return new JsonResult(new
            {
                success = true,
                scheduleId = scheduleId,
                date = targetDate.ToString("yyyy-MM-dd"),
                status = status,
                scheduleName = schedule.ScheduleName,
                startTime = schedule.StartTime.ToString("HH:mm:ss"),
                durationInSec = schedule.DurationInSec
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    // API endpoint for dashboard data for a specific schedule
    public async Task<JsonResult> OnGetGetDashboardDataForScheduleAsync(
        [FromQuery] int scheduleId,
        [FromQuery] string date)
    {
        try
        {
            var targetDate = DateTime.Parse(date);
            var schedules = _scheduleService.GetScheduleByID(scheduleId);

            if (!schedules.Any())
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Schedule not found"
                });
            }

            var schedule = schedules.First();
            var camera = _cameraService.GetCameraById(schedule.CameraID);
            var isToday = targetDate.Date == DateTime.Today;

            var dashboardData = new
            {
                totalIn = 0,
                totalOut = 0,
                totalPresent = 0,
                lastFiveMinutesIn = 0,
                lastFiveMinutesOut = 0,
                lastFiveMinutesPresent = 0,
                isActive = false,
                hasData = false,
                status = GetScheduleStatusForDate(schedule, targetDate),
                scheduleName = schedule.ScheduleName,
                cameraName = camera?.CameraName ?? "Unknown Camera"
            };

            return new JsonResult(new
            {
                success = true,
                data = dashboardData
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    // Test endpoint for Math.Max functionality
    public async Task<JsonResult> OnGetTestMathMaxAsync()
    {
        int testIn = 5;
        int testOut = 10;
        int rawResult = testIn - testOut;
        int maxResult = Math.Max(0, rawResult);

        return new JsonResult(new
        {
            testIn = testIn,
            testOut = testOut,
            rawResult = rawResult,
            maxResult = maxResult,
            message = $"Raw: {rawResult}, After Math.Max: {maxResult}"
        });
    }

    // ====================
    // HELPER METHODS
    // ====================

    private Schedule? GetCurrentActiveScheduleFromList(List<Schedule> allSchedules)
    {
        var now = DateTime.Now;
        var today = now.Date;

        Console.WriteLine($"[DEBUG] Current DateTime: {now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"[DEBUG] Today's Date: {today:yyyy-MM-dd}");

        foreach (var schedule in allSchedules)
        {
            Console.WriteLine($"\n[DEBUG] Checking Schedule: {schedule.ScheduleName}");
            Console.WriteLine($"[DEBUG] Schedule.StartTime from DB: {schedule.StartTime:yyyy-MM-dd HH:mm:ss}");

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

            if (now >= scheduleTimeToday && now <= scheduleEndTime)
            {
                Console.WriteLine($"[DEBUG] *** ACTIVE SCHEDULE FOUND: {schedule.ScheduleName} ***");
                return schedule;
            }
        }

        Console.WriteLine($"[DEBUG] No active schedule found at {now:HH:mm:ss}");
        return null;
    }

    private string GetScheduleStatusForDate(Schedule schedule, DateTime targetDate)
    {
        var now = DateTime.Now;
        var isToday = targetDate.Date == now.Date;

        if (!isToday)
        {
            if (targetDate.Date < now.Date)
            {
                return "completed";
            }
            else
            {
                return "upcoming";
            }
        }

        var scheduleTimeToday = new DateTime(
            targetDate.Year, targetDate.Month, targetDate.Day,
            schedule.StartTime.Hour, schedule.StartTime.Minute, schedule.StartTime.Second
        );
        var scheduleEndTime = scheduleTimeToday.AddSeconds(schedule.DurationInSec);

        if (now < scheduleTimeToday)
        {
            return "upcoming";
        }
        else if (now >= scheduleTimeToday && now <= scheduleEndTime)
        {
            return "active";
        }
        else
        {
            return "completed";
        }
    }

    // FIXED: Add missing GetCurrentActiveSchedule helper
    private Schedule? GetCurrentActiveSchedule()
    {
        try 
        {
            return _scheduleService.GetCurrentActiveSchedule();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GET ACTIVE SCHEDULE ERROR] {ex.Message}");
            return null;
        }
    }
}