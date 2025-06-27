using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class IndexModel : PageModel
{
    private readonly CameraService _cameraService;
    private readonly CountDataService _countDataService;
    private readonly ScheduleService _scheduleService;
    private readonly ApplicationDBContext _context;

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
        public string Status { get; set; } = "inactive";
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
    // NEW: FIXED MISSING API ENDPOINTS
    // ====================

    // NEW: GetAllSchedulesOptimized - The missing endpoint that frontend expects
    public async Task<JsonResult> OnGetGetAllSchedulesOptimizedAsync()
    {
        try
        {
            Console.WriteLine("[API] GetAllSchedulesOptimized called");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var schedulesWithCamera = _scheduleService.GetAllSchedulesWithCameraInfo();

            stopwatch.Stop();
            Console.WriteLine($"[API] Loaded {schedulesWithCamera.Count} schedules in {stopwatch.ElapsedMilliseconds}ms");

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
            Console.WriteLine($"[API ERROR] GetAllSchedulesOptimized failed: {ex.Message}");
            
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

                Console.WriteLine($"[API] Fallback loaded {allSchedules.Count} schedules");
                return new JsonResult(allSchedules);
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"[API ERROR] Fallback failed: {fallbackEx.Message}");
                return new JsonResult(new { 
                    error = ex.Message, 
                    schedules = new List<object>(),
                    success = false 
                });
            }
        }
    }

    // NEW: GetDashboardDataForSelectedSchedule - The missing endpoint for schedule selection
    public async Task<JsonResult> OnGetGetDashboardDataForSelectedScheduleAsync([FromQuery] int scheduleId)
    {
        try
        {
            Console.WriteLine($"[API] GetDashboardDataForSelectedSchedule called for schedule {scheduleId}");

            var schedule = _scheduleService.GetSingleScheduleByID(scheduleId);
            if (schedule == null)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Schedule not found"
                });
            }

            var camera = _cameraService.GetCameraById(schedule.CameraID);
            var now = DateTime.Now;
            var today = now.Date;

            // Calculate schedule time for today
            var scheduleTimeToday = new DateTime(
                today.Year, today.Month, today.Day,
                schedule.StartTime.Hour,
                schedule.StartTime.Minute,
                schedule.StartTime.Second
            );
            var scheduleEndTime = scheduleTimeToday.AddSeconds(schedule.DurationInSec);

            // Determine status
            string status;
            bool isActive = false;
            if (now < scheduleTimeToday)
            {
                status = "upcoming";
            }
            else if (now >= scheduleTimeToday && now <= scheduleEndTime)
            {
                status = "active";
                isActive = true;
            }
            else
            {
                status = "completed";
            }

            var dashboardData = new DashboardData
            {
                ScheduleName = schedule.ScheduleName,
                CameraName = camera?.CameraName ?? "Unknown Camera",
                Status = status,
                IsActive = isActive,
                HasData = false
            };

            // Get count data if schedule is active or completed today
            if (status == "active" || status == "completed")
            {
                try
                {
                    var cameraIds = new List<int> { schedule.CameraID };
                    var scheduleStartUnix = ((DateTimeOffset)scheduleTimeToday).ToUnixTimeSeconds();
                    var currentTimeUnix = ((DateTimeOffset)now).ToUnixTimeSeconds();
                    var scheduleEndUnix = ((DateTimeOffset)scheduleEndTime).ToUnixTimeSeconds();

                    // For active: get data from start to now, for completed: get data for entire schedule
                    var queryEndTime = status == "active" ? currentTimeUnix : scheduleEndUnix;

                    var totals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, scheduleStartUnix, queryEndTime);
                    
                    dashboardData.TotalIn = totals.TotalIn;
                    dashboardData.TotalOut = totals.TotalOut;
                    dashboardData.TotalPresent = Math.Max(0, totals.TotalIn - totals.TotalOut);
                    dashboardData.HasData = true;

                    // Get last 5 minutes data only for active schedules
                    if (status == "active")
                    {
                        var lastFiveMinutesStart = ((DateTimeOffset)now.AddMinutes(-5)).ToUnixTimeSeconds();
                        var recentTotals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, lastFiveMinutesStart, currentTimeUnix);
                        
                        dashboardData.LastFiveMinutesIn = recentTotals.TotalIn;
                        dashboardData.LastFiveMinutesOut = recentTotals.TotalOut;
                        dashboardData.LastFiveMinutesPresent = Math.Max(0, recentTotals.TotalIn - recentTotals.TotalOut);
                    }

                    Console.WriteLine($"[API] Schedule {scheduleId} data: In={dashboardData.TotalIn}, Out={dashboardData.TotalOut}, Present={dashboardData.TotalPresent}");
                }
                catch (Exception dataEx)
                {
                    Console.WriteLine($"[API] Error getting count data for schedule {scheduleId}: {dataEx.Message}");
                }
            }

            return new JsonResult(new
            {
                success = true,
                data = dashboardData,
                scheduleName = schedule.ScheduleName,
                cameraName = camera?.CameraName ?? "Unknown Camera",
                status = status,
                isActive = isActive
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API ERROR] GetDashboardDataForSelectedSchedule failed: {ex.Message}");
            return new JsonResult(new
            {
                success = false,
                error = ex.Message,
                data = new DashboardData()
            });
        }
    }

    // NEW: LastFiveMinutesData - The missing endpoint for queue data
    public async Task<JsonResult> OnGetLastFiveMinutesDataAsync()
    {
        try
        {
            Console.WriteLine("[API] LastFiveMinutesData called");

            var activeSchedule = _scheduleService.GetCurrentActiveSchedule();
            
            if (activeSchedule == null)
            {
                Console.WriteLine("[API] No active schedule for queue data");
                return new JsonResult(new
                {
                    success = false,
                    hasActiveSchedule = false,
                    data = new { In = 0, Out = 0, Present = 0 },
                    message = "No active schedule"
                });
            }

            var camera = _cameraService.GetCameraById(activeSchedule.CameraID);
            if (camera == null)
            {
                Console.WriteLine($"[API] Camera {activeSchedule.CameraID} not found for active schedule");
                return new JsonResult(new
                {
                    success = false,
                    hasActiveSchedule = true,
                    data = new { In = 0, Out = 0, Present = 0 },
                    message = "Camera not found"
                });
            }

            var now = DateTime.Now;
            var fiveMinutesAgo = now.AddMinutes(-5);
            
            var fiveMinutesAgoUnix = ((DateTimeOffset)fiveMinutesAgo).ToUnixTimeSeconds();
            var currentTimeUnix = ((DateTimeOffset)now).ToUnixTimeSeconds();

            var cameraIds = new List<int> { camera.CameraID };
            var recentTotals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, fiveMinutesAgoUnix, currentTimeUnix);

            var queueData = new
            {
                In = recentTotals.TotalIn,
                Out = recentTotals.TotalOut,
                Present = Math.Max(0, recentTotals.TotalIn - recentTotals.TotalOut)
            };

            Console.WriteLine($"[API] Queue data: In={queueData.In}, Out={queueData.Out}, Present={queueData.Present}");

            return new JsonResult(new
            {
                success = true,
                hasActiveSchedule = true,
                data = queueData,
                scheduleName = activeSchedule.ScheduleName,
                cameraName = camera.CameraName
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API ERROR] LastFiveMinutesData failed: {ex.Message}");
            return new JsonResult(new
            {
                success = false,
                hasActiveSchedule = false,
                data = new { In = 0, Out = 0, Present = 0 },
                error = ex.Message
            });
        }
    }

    // ENHANCED: SystemOverview with better error handling and queue data
    public async Task<JsonResult> OnGetSystemOverviewAsync()
    {
        try
        {
            Console.WriteLine("[API] SystemOverview called");

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

                        // Get last 5 minutes data
                        var lastFiveMinutesStart = ((DateTimeOffset)now.AddMinutes(-5)).ToUnixTimeSeconds();
                        var lastFiveMinutesTotals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, lastFiveMinutesStart, currentTimeUnix);
                        
                        lastFiveMinutesIn = lastFiveMinutesTotals?.TotalIn ?? 0;
                        lastFiveMinutesOut = lastFiveMinutesTotals?.TotalOut ?? 0;
                        lastFiveMinutesPresent = Math.Max(0, lastFiveMinutesIn - lastFiveMinutesOut);

                        Console.WriteLine($"[API] SystemOverview counts: Total In={totalIn}, Out={totalOut}, Present={totalPresent}");
                        Console.WriteLine($"[API] SystemOverview queue: In={lastFiveMinutesIn}, Out={lastFiveMinutesOut}, Present={lastFiveMinutesPresent}");
                    }
                    catch (Exception dataEx)
                    {
                        Console.WriteLine($"[API] Error getting count data in SystemOverview: {dataEx.Message}");
                    }
                }
            }

            var result = new
            {
                totalCameras = totalCameras,
                activeCameras = activeCameras,
                totalCameraCapacity = 32,
                peopleIn = totalIn,
                peopleOut = totalOut,
                peopleInCapacity = 165,
                lastFiveMinutesIn = lastFiveMinutesIn,
                lastFiveMinutesOut = lastFiveMinutesOut,
                lastFiveMinutesPresent = lastFiveMinutesPresent,
                hasActiveSchedule = activeSchedule != null,
                currentScheduleName = activeSchedule?.ScheduleName ?? "No Active Schedule"
            };

            return new JsonResult(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API ERROR] SystemOverview failed: {ex.Message}");
            
            return new JsonResult(new
            {
                error = ex.Message,
                totalCameras = 0,
                activeCameras = 0,
                totalCameraCapacity = 32,
                peopleIn = 0,
                peopleOut = 0,
                peopleInCapacity = 165,
                lastFiveMinutesIn = 0,
                lastFiveMinutesOut = 0,
                lastFiveMinutesPresent = 0,
                hasActiveSchedule = false,
                currentScheduleName = "Error loading data"
            });
        }
    }

    // ====================
    // EXISTING API ENDPOINTS (Updated)
    // ====================

    public async Task<JsonResult> OnGetGetPeopleCountAsync([FromQuery] List<int> cameraIds,
    [FromQuery] long from,
    [FromQuery] long to)
    {
        try
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
        catch (Exception ex)
        {
            Console.WriteLine($"[API ERROR] GetPeopleCount failed: {ex.Message}");
            return new JsonResult(new
            {
                totalIn = 0,
                totalOut = 0,
                totalPresent = 0,
                error = ex.Message
            });
        }
    }

    public async Task<JsonResult> OnGetGetSchedulesAsync([FromQuery] int cameraId)
    {
        try
        {
            var schedules = _scheduleService.GetSchedules(cameraId);
            return new JsonResult(schedules);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API ERROR] GetSchedules failed: {ex.Message}");
            return new JsonResult(new List<Schedule>());
        }
    }

    public async Task<JsonResult> OnGetGetScheduleByIDAsync([FromQuery] int scheduleID)
    {
        try
        {
            var schedule = _scheduleService.GetScheduleByID(scheduleID);
            return new JsonResult(schedule);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API ERROR] GetScheduleByID failed: {ex.Message}");
            return new JsonResult(new List<Schedule>());
        }
    }

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
            Console.WriteLine($"[API ERROR] GetAllSchedules failed: {ex.Message}");
            return new JsonResult(new { error = ex.Message });
        }
    }

    public async Task<JsonResult> OnGetGetCurrentActiveScheduleAsync()
    {
        try
        {
            var activeSchedule = _scheduleService.GetCurrentActiveSchedule();

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
            Console.WriteLine($"[API ERROR] GetCurrentActiveSchedule failed: {ex.Message}");
            return new JsonResult(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    public async Task<JsonResult> OnGetCurrentScheduleStatusAsync()
    {
        try
        {
            var activeSchedule = _scheduleService.GetCurrentActiveSchedule();

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
            Console.WriteLine($"[API ERROR] CurrentScheduleStatus failed: {ex.Message}");
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

    // ENHANCED: ActivityData and LastHourTraffic endpoints
    public async Task<JsonResult> OnGetActivityDataAsync([FromQuery] List<int> cameraIds, [FromQuery] string date)
    {
        try
        {
            Console.WriteLine($"[API] ActivityData called for date {date} and cameras: {string.Join(",", cameraIds)}");
            
            var targetDate = DateTime.Parse(date);
            var startOfDay = targetDate.Date;
            var endOfDay = startOfDay.AddDays(1);
            
            var startUnix = ((DateTimeOffset)startOfDay).ToUnixTimeSeconds();
            var endUnix = ((DateTimeOffset)endOfDay).ToUnixTimeSeconds();
            
            // Generate hourly data for the day
            var hourlyData = new List<object>();
            
            for (int hour = 0; hour < 24; hour++)
            {
                var hourStart = startOfDay.AddHours(hour);
                var hourEnd = hourStart.AddHours(1);
                
                var hourStartUnix = ((DateTimeOffset)hourStart).ToUnixTimeSeconds();
                var hourEndUnix = ((DateTimeOffset)hourEnd).ToUnixTimeSeconds();
                
                var totals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, hourStartUnix, hourEndUnix);
                
                hourlyData.Add(new
                {
                    hour = hourStart.ToString("HH:mm"),
                    peopleIn = totals.TotalIn,
                    peopleOut = totals.TotalOut
                });
            }
            
            return new JsonResult(hourlyData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API ERROR] ActivityData failed: {ex.Message}");
            return new JsonResult(new List<object>());
        }
    }

    public async Task<JsonResult> OnGetLastHourTrafficAsync([FromQuery] List<int> cameraIds)
    {
        try
        {
            Console.WriteLine($"[API] LastHourTraffic called for cameras: {string.Join(",", cameraIds)}");
            
            var now = DateTime.Now;
            var oneHourAgo = now.AddHours(-1);
            
            // Generate data for last hour in 10-minute intervals
            var intervalData = new List<object>();
            
            for (int i = 0; i < 6; i++) // 6 intervals of 10 minutes each
            {
                var intervalStart = oneHourAgo.AddMinutes(i * 10);
                var intervalEnd = intervalStart.AddMinutes(10);
                
                var startUnix = ((DateTimeOffset)intervalStart).ToUnixTimeSeconds();
                var endUnix = ((DateTimeOffset)intervalEnd).ToUnixTimeSeconds();
                
                var totals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, startUnix, endUnix);
                
                intervalData.Add(new
                {
                    hour = intervalStart.ToString("HH:mm"),
                    peopleIn = totals.TotalIn,
                    peopleOut = totals.TotalOut
                });
            }
            
            return new JsonResult(intervalData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API ERROR] LastHourTraffic failed: {ex.Message}");
            // Return default data structure
            var defaultData = new List<object>();
            for (int i = 0; i < 6; i++)
            {
                defaultData.Add(new
                {
                    hour = DateTime.Now.AddMinutes(-60 + (i * 10)).ToString("HH:mm"),
                    peopleIn = 0,
                    peopleOut = 0
                });
            }
            return new JsonResult(defaultData);
        }
    }

    public async Task<JsonResult> OnGetCamerasDataAsync()
    {
        try
        {
            var cameras = _cameraService.GetCameras();
            var cameraData = cameras.Select(camera => new
            {
                id = camera.CameraID,
                name = camera.CameraName,
                status = camera.RefreshRateInSeconds > 0 ? "active" : "inactive",
                refreshRate = camera.RefreshRateInSeconds,
                lastUpdate = camera.LastRefreshTimestamp,
                location = new { x = 50, y = 50 } // Default location
            }).ToList();
            
            return new JsonResult(cameraData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API ERROR] CamerasData failed: {ex.Message}");
            return new JsonResult(new List<object>());
        }
    }
}