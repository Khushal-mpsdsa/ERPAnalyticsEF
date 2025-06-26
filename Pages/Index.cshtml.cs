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

    // UPDATED: Queue properties (was Last 5 Minutes)
    public int LastFiveMinutesIn { get; set; }
    public int LastFiveMinutesOut { get; set; }
    public int LastFiveMinutesPresent { get; set; } // This now represents queue count (In - Out)


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
        public DashboardData DashboardData { get; set; } = new DashboardData();
    }
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

            // UPDATED: Get last 5 minutes queue data
            var fiveMinutesAgoUnix = ((DateTimeOffset)now.AddMinutes(-5).ToUniversalTime()).ToUnixTimeSeconds();
            var lastFiveMinutesData = await _countDataService.GetCountTotalsFilteredAsync(
                new List<int> { activeSchedule.CameraID },
                fiveMinutesAgoUnix,
                nowUnix
            );

            LastFiveMinutesIn = lastFiveMinutesData.TotalIn;
            LastFiveMinutesOut = lastFiveMinutesData.TotalOut;
            // UPDATED: Calculate queue count as In - Out
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

            // Queue also zero when no active schedule
            LastFiveMinutesIn = 0;
            LastFiveMinutesOut = 0;
            LastFiveMinutesPresent = 0;
        }
    }

    // UPDATED: Queue data API endpoint
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
            var queueData = new { In = 0, Out = 0, Present = 0 };

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

                // UPDATED: Calculate queue count as In - Out
                var queueCount = Math.Max(0, totals.TotalIn - totals.TotalOut);

                queueData = new
                {
                    In = totals.TotalIn,
                    Out = totals.TotalOut,
                    Present = queueCount  // This is now the queue count
                };

                Console.WriteLine($"[QUEUE API] Schedule: {activeSchedule.ScheduleName}, In: {totals.TotalIn}, Out: {totals.TotalOut}, Queue: {queueCount}");
            }
            else
            {
                Console.WriteLine($"[QUEUE API] No active schedule");
            }

            return new JsonResult(new
            {
                success = true,
                data = queueData,
                timeWindow = "Last 5 minutes",
                activeSchedule = activeSchedule?.ScheduleName ?? "No Active Schedule",
                hasActiveSchedule = activeSchedule != null,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QUEUE API ERROR] {ex.Message}");
            return new JsonResult(new
            {
                success = false,
                error = ex.Message,
                data = new { In = 0, Out = 0, Present = 0 }
            });
        }
    }

    // Helper method to find current active schedule
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

    // UPDATED: API endpoint for getting system overview data with queue calculation
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

            // Get last 5 minutes data for queue
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
        // UPDATED: Calculate queue count as In - Out
        var lastFiveMinPresent = Math.Max(0, lastFiveMinIn - lastFiveMinOut);

        return new JsonResult(new
        {
            totalCameras,
            totalCameraCapacity = 32,
            activeCameras,
            peopleIn = peopleIn,
            peopleInCapacity = 3000,
            peopleOut = peopleOut,
            totalCamerasTrend = 4,
            activeCamerasTrend = 0,
            peopleInChangeTrend = -12,
            peopleOutTrend = 8,
            currentScheduleName = activeSchedule?.ScheduleName ?? "No Active Schedule",
            hasActiveSchedule = activeSchedule != null,
            totalPresent = totalPresent,
            // UPDATED: Queue data (was Last 5 minutes data)
            lastFiveMinutesIn = lastFiveMinIn,
            lastFiveMinutesOut = lastFiveMinOut,
            lastFiveMinutesPresent = lastFiveMinPresent  // This is now the queue count
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

    // UPDATED: API endpoint for getting current people count
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
                currentCount = Math.Max(0, rawCount);

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
            var count = Math.Max(0, totals.TotalIn - totals.TotalOut);

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



    // UPDATED: API endpoint to get current schedule status with meal header information
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
                    timeRemaining = (int)Math.Max(0, timeRemaining.TotalSeconds),
                    scheduleStart = scheduleStart.ToString("HH:mm:ss"),
                    scheduleEnd = scheduleEnd.ToString("HH:mm:ss"),
                    // UPDATED: Additional data for meal header
                    currentMealName = activeSchedule.ScheduleName,
                    isLunchTime = activeSchedule.ScheduleName?.ToLower().Contains("lunch") ?? false,
                    isBreakfastTime = activeSchedule.ScheduleName?.ToLower().Contains("breakfast") ?? false,
                    isDinnerTime = activeSchedule.ScheduleName?.ToLower().Contains("dinner") ?? false
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

    // Test endpoint for Math.Max functionality
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
                cameraName = camera?.CameraName ?? "Unknown Camera",
                startTime = schedule.StartTime.ToString("HH:mm:ss"),
                duration = schedule.DurationInSec
            };

            if (isToday)
            {
                var status = GetScheduleStatusForDate(schedule, targetDate);

                if (status == "active" || status == "completed")
                {
                    var now = DateTime.Now;
                    var scheduleStartToday = new DateTime(
                        targetDate.Year, targetDate.Month, targetDate.Day,
                        schedule.StartTime.Hour, schedule.StartTime.Minute, schedule.StartTime.Second
                    );

                    var scheduleStartUnix = ((DateTimeOffset)scheduleStartToday.ToUniversalTime()).ToUnixTimeSeconds();
                    var scheduleEndUnix = scheduleStartUnix + schedule.DurationInSec;
                    var currentTimeUnix = ((DateTimeOffset)now.ToUniversalTime()).ToUnixTimeSeconds();

                    // Get data from schedule start to now (for active) or schedule end (for completed)
                    var queryEndTime = status == "active" ? currentTimeUnix : scheduleEndUnix;

                    var totals = await _countDataService.GetCountTotalsFilteredAsync(
                        new List<int> { schedule.CameraID },
                        scheduleStartUnix,
                        queryEndTime
                    );

                    var totalIn = totals.TotalIn;
                    var totalOut = totals.TotalOut;
                    var totalPresent = Math.Max(0, totalIn - totalOut);

                    dashboardData = new
                    {
                        totalIn = totalIn,
                        totalOut = totalOut,
                        totalPresent = totalPresent,
                        lastFiveMinutesIn = 0,
                        lastFiveMinutesOut = 0,
                        lastFiveMinutesPresent = 0,
                        isActive = status == "active",
                        hasData = true,
                        status = status,
                        scheduleName = schedule.ScheduleName,
                        cameraName = camera?.CameraName ?? "Unknown Camera",
                        startTime = schedule.StartTime.ToString("HH:mm:ss"),
                        duration = schedule.DurationInSec
                    };

                    // Get last 5 minutes data for active schedules
                    if (status == "active")
                    {
                        var fiveMinutesAgoUnix = currentTimeUnix - (5 * 60);
                        var lastFiveMinutesTotals = await _countDataService.GetCountTotalsFilteredAsync(
                            new List<int> { schedule.CameraID },
                            fiveMinutesAgoUnix,
                            currentTimeUnix
                        );

                        dashboardData = new
                        {
                            totalIn = totalIn,
                            totalOut = totalOut,
                            totalPresent = totalPresent,
                            lastFiveMinutesIn = lastFiveMinutesTotals.TotalIn,
                            lastFiveMinutesOut = lastFiveMinutesTotals.TotalOut,
                            lastFiveMinutesPresent = Math.Max(0, lastFiveMinutesTotals.TotalIn - lastFiveMinutesTotals.TotalOut),
                            isActive = true,
                            hasData = true,
                            status = status,
                            scheduleName = schedule.ScheduleName,
                            cameraName = camera?.CameraName ?? "Unknown Camera",
                            startTime = schedule.StartTime.ToString("HH:mm:ss"),
                            duration = schedule.DurationInSec
                        };
                    }
                }
            }

            return new JsonResult(new
            {
                success = true,
                data = dashboardData,
                date = targetDate.ToString("yyyy-MM-dd")
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

    // API endpoint for getting dashboard data for a specific date (no schedule selected)
    public async Task<JsonResult> OnGetGetDashboardDataForDateAsync([FromQuery] string date)
    {
        try
        {
            var targetDate = DateTime.Parse(date);
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
                status = "no_schedule",
                scheduleName = (string)null,
                cameraName = (string)null
            };

            if (isToday)
            {
                // Check if there's any currently active schedule
                var cameras = _cameraService.GetCameras();
                var allSchedules = new List<Schedule>();

                foreach (var camera in cameras)
                {
                    var schedules = _scheduleService.GetSchedules(camera.CameraID);
                    allSchedules.AddRange(schedules);
                }

                var activeSchedule = GetCurrentActiveScheduleFromList(allSchedules);

                if (activeSchedule != null)
                {
                    // Redirect to active schedule data
                    var scheduleResponse = await OnGetGetDashboardDataForScheduleAsync(activeSchedule.ScheduleID, date);
                    return scheduleResponse;
                }
                else
                {
                    // No active schedule - show overall system status or zeros
                    dashboardData = new
                    {
                        totalIn = 0,
                        totalOut = 0,
                        totalPresent = 0,
                        lastFiveMinutesIn = 0,
                        lastFiveMinutesOut = 0,
                        lastFiveMinutesPresent = 0,
                        isActive = false,
                        hasData = false,
                        status = "no_active_schedule",
                        scheduleName = (string)null,
                        cameraName = (string)null
                    };
                }
            }

            return new JsonResult(new
            {
                success = true,
                data = dashboardData,
                date = targetDate.ToString("yyyy-MM-dd"),
                isToday = isToday
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

    // API endpoint for getting real-time dashboard updates for active schedules
    public async Task<JsonResult> OnGetGetRealTimeDashboardDataAsync()
    {
        try
        {
            var today = DateTime.Today;
            var cameras = _cameraService.GetCameras();
            var allSchedules = new List<Schedule>();

            foreach (var camera in cameras)
            {
                var schedules = _scheduleService.GetSchedules(camera.CameraID);
                allSchedules.AddRange(schedules);
            }

            var activeSchedule = GetCurrentActiveScheduleFromList(allSchedules);

            if (activeSchedule == null)
            {
                return new JsonResult(new
                {
                    success = true,
                    hasActiveSchedule = false,
                    data = new
                    {
                        totalIn = 0,
                        totalOut = 0,
                        totalPresent = 0,
                        lastFiveMinutesIn = 0,
                        lastFiveMinutesOut = 0,
                        lastFiveMinutesPresent = 0,
                        isActive = false,
                        hasData = false
                    }
                });
            }

            // Get data for the active schedule
            var scheduleResponse = await OnGetGetDashboardDataForScheduleAsync(
                activeSchedule.ScheduleID,
                today.ToString("yyyy-MM-dd")
            );

            return new JsonResult(new
            {
                success = true,
                hasActiveSchedule = true,
                activeSchedule = new
                {
                    scheduleId = activeSchedule.ScheduleID,
                    scheduleName = activeSchedule.ScheduleName,
                    cameraId = activeSchedule.CameraID
                },
                data = ((JsonResult)scheduleResponse).Value
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

    // API endpoint for getting aggregated dashboard data across all cameras for a date
    public async Task<JsonResult> OnGetGetAggregatedDashboardDataAsync([FromQuery] string date)
    {
        try
        {
            var targetDate = DateTime.Parse(date);
            var isToday = targetDate.Date == DateTime.Today;
            var cameras = _cameraService.GetCameras();

            var aggregatedData = new
            {
                totalIn = 0,
                totalOut = 0,
                totalPresent = 0,
                lastFiveMinutesIn = 0,
                lastFiveMinutesOut = 0,
                lastFiveMinutesPresent = 0,
                isActive = false,
                hasData = false,
                activeCameras = 0,
                totalCameras = cameras.Count
            };

            if (!isToday)
            {
                // For non-today dates, return historical aggregated data if available
                // This would require implementing historical data aggregation
                return new JsonResult(new
                {
                    success = true,
                    data = aggregatedData,
                    date = targetDate.ToString("yyyy-MM-dd"),
                    message = "Historical aggregated data not available"
                });
            }

            // For today, get data across all active cameras
            var activeCameraIds = cameras.Where(c => c.RefreshRateInSeconds > 0).Select(c => c.CameraID).ToList();

            if (activeCameraIds.Any())
            {
                var startOfDay = targetDate.Date;
                var now = DateTime.Now;
                var startOfDayUnix = ((DateTimeOffset)startOfDay.ToUniversalTime()).ToUnixTimeSeconds();
                var nowUnix = ((DateTimeOffset)now.ToUniversalTime()).ToUnixTimeSeconds();

                // Get aggregated data from all active cameras for today
                var todayTotals = await _countDataService.GetCountTotalsFilteredAsync(
                    activeCameraIds,
                    startOfDayUnix,
                    nowUnix
                );

                // Get last 5 minutes data
                var fiveMinutesAgoUnix = nowUnix - (5 * 60);
                var lastFiveMinutesTotals = await _countDataService.GetCountTotalsFilteredAsync(
                    activeCameraIds,
                    fiveMinutesAgoUnix,
                    nowUnix
                );

                aggregatedData = new
                {
                    totalIn = todayTotals.TotalIn,
                    totalOut = todayTotals.TotalOut,
                    totalPresent = Math.Max(0, todayTotals.TotalIn - todayTotals.TotalOut),
                    lastFiveMinutesIn = lastFiveMinutesTotals.TotalIn,
                    lastFiveMinutesOut = lastFiveMinutesTotals.TotalOut,
                    lastFiveMinutesPresent = Math.Max(0, lastFiveMinutesTotals.TotalIn - lastFiveMinutesTotals.TotalOut),
                    isActive = false, // Not tied to a specific schedule
                    hasData = true,
                    activeCameras = activeCameraIds.Count,
                    totalCameras = cameras.Count
                };
            }

            return new JsonResult(new
            {
                success = true,
                data = aggregatedData,
                date = targetDate.ToString("yyyy-MM-dd"),
                isAggregated = true
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

    // Enhanced system overview API that supports schedule-based filtering
    public async Task<JsonResult> OnGetEnhancedSystemOverviewAsync(
        [FromQuery] int? scheduleId = null,
        [FromQuery] string date = null)
    {
        try
        {
            var cameras = _cameraService.GetCameras();
            var targetDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var isToday = targetDate.Date == DateTime.Today;

            var totalCameras = cameras.Count;
            var activeCameras = cameras.Count(c => c.RefreshRateInSeconds > 0);

            // Default system overview data
            var systemData = new
            {
                totalCameras = totalCameras,
                totalCameraCapacity = 32,
                activeCameras = activeCameras,
                peopleIn = 0,
                peopleInCapacity = 165,
                peopleOut = 0,
                totalPresent = 0,
                lastFiveMinutesIn = 0,
                lastFiveMinutesOut = 0,
                lastFiveMinutesPresent = 0,
                currentScheduleName = "No Schedule Selected",
                hasActiveSchedule = false,
                hasSelectedSchedule = scheduleId.HasValue,
                selectedDate = targetDate.ToString("yyyy-MM-dd"),
                isToday = isToday
            };

            if (scheduleId.HasValue)
            {
                // Get data for specific schedule
                var scheduleDataResponse = await OnGetGetDashboardDataForScheduleAsync(scheduleId.Value, targetDate.ToString("yyyy-MM-dd"));
                var scheduleDataResult = (JsonResult)scheduleDataResponse;
                var scheduleDataObj = scheduleDataResult.Value;

                // Extract data from the response
                var dataProperty = scheduleDataObj.GetType().GetProperty("data");
                if (dataProperty != null)
                {
                    var scheduleData = dataProperty.GetValue(scheduleDataObj);
                    var scheduleName = scheduleData.GetType().GetProperty("scheduleName")?.GetValue(scheduleData)?.ToString() ?? "Selected Schedule";
                    var totalIn = (int)(scheduleData.GetType().GetProperty("totalIn")?.GetValue(scheduleData) ?? 0);
                    var totalOut = (int)(scheduleData.GetType().GetProperty("totalOut")?.GetValue(scheduleData) ?? 0);
                    var totalPresent = (int)(scheduleData.GetType().GetProperty("totalPresent")?.GetValue(scheduleData) ?? 0);
                    var lastFiveMinIn = (int)(scheduleData.GetType().GetProperty("lastFiveMinutesIn")?.GetValue(scheduleData) ?? 0);
                    var lastFiveMinOut = (int)(scheduleData.GetType().GetProperty("lastFiveMinutesOut")?.GetValue(scheduleData) ?? 0);
                    var lastFiveMinPresent = (int)(scheduleData.GetType().GetProperty("lastFiveMinutesPresent")?.GetValue(scheduleData) ?? 0);
                    var isActive = (bool)(scheduleData.GetType().GetProperty("isActive")?.GetValue(scheduleData) ?? false);

                    systemData = new
                    {
                        totalCameras = totalCameras,
                        totalCameraCapacity = 32,
                        activeCameras = activeCameras,
                        peopleIn = totalIn,
                        peopleInCapacity = 165,
                        peopleOut = totalOut,
                        totalPresent = totalPresent,
                        lastFiveMinutesIn = lastFiveMinIn,
                        lastFiveMinutesOut = lastFiveMinOut,
                        lastFiveMinutesPresent = lastFiveMinPresent,
                        currentScheduleName = scheduleName,
                        hasActiveSchedule = isActive,
                        hasSelectedSchedule = true,
                        selectedDate = targetDate.ToString("yyyy-MM-dd"),
                        isToday = isToday
                    };
                }
            }
            else if (isToday)
            {
                // No specific schedule selected, check for active schedule
                var allSchedules = new List<Schedule>();
                foreach (var camera in cameras)
                {
                    var schedules = _scheduleService.GetSchedules(camera.CameraID);
                    allSchedules.AddRange(schedules);
                }

                var activeSchedule = GetCurrentActiveScheduleFromList(allSchedules);

                if (activeSchedule != null)
                {
                    // Recursively call with the active schedule
                    return await OnGetEnhancedSystemOverviewAsync(activeSchedule.ScheduleID, targetDate.ToString("yyyy-MM-dd"));
                }
            }

            return new JsonResult(systemData);
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

    // API endpoint for getting schedule summary for dashboard
    public async Task<JsonResult> OnGetGetScheduleSummaryForDashboardAsync([FromQuery] string date)
    {
        try
        {
            var targetDate = DateTime.Parse(date);
            var cameras = _cameraService.GetCameras();
            var allSchedules = new List<object>();

            foreach (var camera in cameras)
            {
                var schedules = _scheduleService.GetSchedules(camera.CameraID);

                foreach (var schedule in schedules)
                {
                    var status = GetScheduleStatusForDate(schedule, targetDate);
                    var startTime = schedule.StartTime;
                    var endTime = startTime.AddSeconds(schedule.DurationInSec);

                    allSchedules.Add(new
                    {
                        scheduleId = schedule.ScheduleID,
                        scheduleName = schedule.ScheduleName,
                        cameraId = schedule.CameraID,
                        cameraName = camera.CameraName,
                        startTime = startTime.ToString("HH:mm"),
                        endTime = endTime.ToString("HH:mm"),
                        duration = schedule.DurationInSec,
                        status = status,
                        isClickable = true
                    });
                }
            }

            // Sort by start time
            var sortedSchedules = allSchedules
                .OrderBy(s => DateTime.Parse(s.GetType().GetProperty("startTime").GetValue(s).ToString()))
                .ToList();

            var summary = new
            {
                date = targetDate.ToString("yyyy-MM-dd"),
                totalSchedules = sortedSchedules.Count,
                upcomingCount = sortedSchedules.Count(s => s.GetType().GetProperty("status").GetValue(s).ToString() == "upcoming"),
                activeCount = sortedSchedules.Count(s => s.GetType().GetProperty("status").GetValue(s).ToString() == "active"),
                completedCount = sortedSchedules.Count(s => s.GetType().GetProperty("status").GetValue(s).ToString() == "completed"),
                schedules = sortedSchedules
            };

            return new JsonResult(new
            {
                success = true,
                summary = summary
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
    // API endpoint for getting schedule status for a specific date
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

    // Helper method to get schedule status for a specific date (ADD THIS TO YOUR IndexModel CLASS)
    private string GetScheduleStatusForDate(Schedule schedule, DateTime targetDate)
    {
        var now = DateTime.Now;
        var isToday = targetDate.Date == now.Date;

        if (!isToday)
        {
            // For past or future dates
            if (targetDate.Date < now.Date)
            {
                return "completed";
            }
            else
            {
                return "upcoming";
            }
        }

        // For today, check actual time
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

    // API endpoint for getting all schedules for a date with their statuses
    public async Task<JsonResult> OnGetGetSchedulesWithStatusForDateAsync([FromQuery] string date)
    {
        try
        {
            var targetDate = DateTime.Parse(date);
            var cameras = _cameraService.GetCameras();
            var allSchedulesWithStatus = new List<object>();

            foreach (var camera in cameras)
            {
                var schedules = _scheduleService.GetSchedules(camera.CameraID);

                foreach (var schedule in schedules)
                {
                    var status = GetScheduleStatusForDate(schedule, targetDate);
                    var startTime = schedule.StartTime;
                    var endTime = startTime.AddSeconds(schedule.DurationInSec);

                    allSchedulesWithStatus.Add(new
                    {
                        scheduleID = schedule.ScheduleID,
                        scheduleName = schedule.ScheduleName,
                        cameraID = schedule.CameraID,
                        cameraName = camera.CameraName,
                        startTime = schedule.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                        endTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                        durationInSec = schedule.DurationInSec,
                        status = status,
                        isActive = status == "active",
                        isCompleted = status == "completed",
                        isUpcoming = status == "upcoming"
                    });
                }
            }

            // Sort by start time
            var sortedSchedules = allSchedulesWithStatus
                .OrderBy(s => DateTime.Parse(s.GetType().GetProperty("startTime").GetValue(s).ToString()))
                .ToList();

            return new JsonResult(new
            {
                success = true,
                date = targetDate.ToString("yyyy-MM-dd"),
                schedules = sortedSchedules,
                count = sortedSchedules.Count
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new
            {
                success = false,
                error = ex.Message,
                schedules = new List<object>()
            });
        }
    }

    // API endpoint to check if any schedule is currently active
    public async Task<JsonResult> OnGetGetCurrentActiveScheduleAsync()
    {
        try
        {
            var cameras = _cameraService.GetCameras();
            var allSchedules = new List<Schedule>();

            foreach (var cam in cameras)
            {
                var schedules = _scheduleService.GetSchedules(cam.CameraID);
                allSchedules.AddRange(schedules);
            }

            var today = DateTime.Today;
            var activeSchedule = GetCurrentActiveScheduleFromList(allSchedules);

            if (activeSchedule == null)
            {
                return new JsonResult(new
                {
                    success = true,
                    hasActiveSchedule = false,
                    message = "No active schedule found"
                });
            }

            var camera = _cameraService.GetCameraById(activeSchedule.CameraID);
            var now = DateTime.Now;
            var scheduleStartToday = new DateTime(
                today.Year, today.Month, today.Day,
                activeSchedule.StartTime.Hour,
                activeSchedule.StartTime.Minute,
                activeSchedule.StartTime.Second
            );
            var scheduleEndTime = scheduleStartToday.AddSeconds(activeSchedule.DurationInSec);
            var timeRemaining = scheduleEndTime - now;

            return new JsonResult(new
            {
                success = true,
                hasActiveSchedule = true,
                schedule = new
                {
                    scheduleID = activeSchedule.ScheduleID,
                    scheduleName = activeSchedule.ScheduleName,
                    cameraID = activeSchedule.CameraID,
                    cameraName = camera?.CameraName ?? "Unknown Camera",
                    startTime = activeSchedule.StartTime.ToString("HH:mm:ss"),
                    durationInSec = activeSchedule.DurationInSec,
                    timeRemainingSeconds = Math.Max(0, (int)timeRemaining.TotalSeconds),
                    status = "active"
                }
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new
            {
                success = false,
                error = ex.Message,
                hasActiveSchedule = false
            });
        }
    }

    // Enhanced helper method for getting current active schedule (UPDATE YOUR EXISTING ONE)
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

    // API endpoint to get dashboard overview with schedule context
    public async Task<JsonResult> OnGetGetScheduleBasedDashboardOverviewBetterAsync(
        [FromQuery] string date,
        [FromQuery] int? scheduleId = null)
    {
        try
        {
            var targetDate = DateTime.Parse(date);
            var isToday = targetDate.Date == DateTime.Today;
            var cameras = _cameraService.GetCameras();

            // Use defined classes instead of anonymous types
            var overview = new DashboardOverview
            {
                Date = targetDate.ToString("yyyy-MM-dd"),
                IsToday = isToday,
                TotalCameras = cameras.Count,
                ActiveCameras = cameras.Count(c => c.RefreshRateInSeconds > 0),
                HasSelectedSchedule = scheduleId.HasValue,
                SelectedScheduleId = scheduleId,
                DashboardData = new DashboardData() // Default empty data
            };

            if (scheduleId.HasValue)
            {
                var schedules = _scheduleService.GetScheduleByID(scheduleId.Value);
                if (schedules.Any())
                {
                    var schedule = schedules.First();
                    var camera = _cameraService.GetCameraById(schedule.CameraID);

                    overview.DashboardData.ScheduleName = schedule.ScheduleName;
                    overview.DashboardData.CameraName = camera?.CameraName ?? "Unknown Camera";

                    if (isToday)
                    {
                        var status = GetScheduleStatusForDate(schedule, targetDate);
                        if (status == "active" || status == "completed")
                        {
                            var now = DateTime.Now;
                            var scheduleStartToday = new DateTime(
                                targetDate.Year, targetDate.Month, targetDate.Day,
                                schedule.StartTime.Hour, schedule.StartTime.Minute, schedule.StartTime.Second
                            );

                            var scheduleStartUnix = ((DateTimeOffset)scheduleStartToday.ToUniversalTime()).ToUnixTimeSeconds();
                            var scheduleEndUnix = scheduleStartUnix + schedule.DurationInSec;
                            var currentTimeUnix = ((DateTimeOffset)now.ToUniversalTime()).ToUnixTimeSeconds();

                            var queryEndTime = status == "active" ? currentTimeUnix : scheduleEndUnix;

                            var totals = await _countDataService.GetCountTotalsFilteredAsync(
                                new List<int> { schedule.CameraID },
                                scheduleStartUnix,
                                queryEndTime
                            );

                            overview.DashboardData.TotalIn = totals.TotalIn;
                            overview.DashboardData.TotalOut = totals.TotalOut;
                            overview.DashboardData.TotalPresent = Math.Max(0, totals.TotalIn - totals.TotalOut);
                            overview.DashboardData.IsActive = status == "active";
                            overview.DashboardData.HasData = true;

                            // Get last 5 minutes data for active schedules
                            if (status == "active")
                            {
                                var fiveMinutesAgoUnix = currentTimeUnix - (5 * 60);
                                var lastFiveMinutesTotals = await _countDataService.GetCountTotalsFilteredAsync(
                                    new List<int> { schedule.CameraID },
                                    fiveMinutesAgoUnix,
                                    currentTimeUnix
                                );

                                overview.DashboardData.LastFiveMinutesIn = lastFiveMinutesTotals.TotalIn;
                                overview.DashboardData.LastFiveMinutesOut = lastFiveMinutesTotals.TotalOut;
                                overview.DashboardData.LastFiveMinutesPresent = Math.Max(0, lastFiveMinutesTotals.TotalIn - lastFiveMinutesTotals.TotalOut);
                            }
                        }
                    }

                    overview.HasSelectedSchedule = true;
                }
            }
            else if (isToday)
            {
                // Check for active schedule
                var allSchedules = new List<Schedule>();
                foreach (var camera in cameras)
                {
                    var schedules = _scheduleService.GetSchedules(camera.CameraID);
                    allSchedules.AddRange(schedules);
                }

                var activeSchedule = GetCurrentActiveScheduleFromList(allSchedules);
                if (activeSchedule != null)
                {
                    // Recursively get data for active schedule
                    return await OnGetGetScheduleBasedDashboardOverviewBetterAsync(date, activeSchedule.ScheduleID);
                }
            }

            return new JsonResult(new
            {
                success = true,
                overview = overview
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
  
  // API endpoint for getting all schedules (for the main dashboard)
public async Task<JsonResult> OnGetGetAllSchedulesAsync()
{
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
                    cameraID = schedule.CameraID,
                    scheduleName = schedule.ScheduleName,
                    startTime = schedule.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    durationInSec = schedule.DurationInSec,
                    cameraName = camera.CameraName,
                    // Add time-only for daily recurring display
                    timeOnly = schedule.StartTime.ToString("HH:mm"),
                    endTime = schedule.StartTime.AddSeconds(schedule.DurationInSec).ToString("HH:mm")
                });
            }
        }

        // Sort by start time
        var sortedSchedules = allSchedules
            .OrderBy(s => DateTime.Parse(s.GetType().GetProperty("startTime").GetValue(s).ToString()))
            .ToList();

        return new JsonResult(sortedSchedules);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Error loading all schedules: {ex.Message}");
        return new JsonResult(new List<object>());
    }
}

// API endpoint for getting dashboard data when a schedule is selected
public async Task<JsonResult> OnGetGetDashboardDataForSelectedScheduleAsync([FromQuery] int scheduleId)
{
    try
    {
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
        var now = DateTime.Now;
        var today = now.Date;
        
        // Convert schedule to today's timeframe
        var scheduleStartToday = new DateTime(
            today.Year, today.Month, today.Day,
            schedule.StartTime.Hour, schedule.StartTime.Minute, schedule.StartTime.Second
        );
        var scheduleEndToday = scheduleStartToday.AddSeconds(schedule.DurationInSec);
        
        // Determine if schedule is active right now
        var isActive = now >= scheduleStartToday && now <= scheduleEndToday;
        var hasData = false;
        var totalIn = 0;
        var totalOut = 0;
        var totalPresent = 0;
        var lastFiveMinutesIn = 0;
        var lastFiveMinutesOut = 0;
        var lastFiveMinutesPresent = 0;

        if (isActive)
        {
            // Get data from schedule start to now
            var scheduleStartUnix = ((DateTimeOffset)scheduleStartToday.ToUniversalTime()).ToUnixTimeSeconds();
            var nowUnix = ((DateTimeOffset)now.ToUniversalTime()).ToUnixTimeSeconds();
            
            var totals = await _countDataService.GetCountTotalsFilteredAsync(
                new List<int> { schedule.CameraID },
                scheduleStartUnix,
                nowUnix
            );

            totalIn = totals.TotalIn;
            totalOut = totals.TotalOut;
            totalPresent = Math.Max(0, totalIn - totalOut);
            hasData = true;

            // Get last 5 minutes data
            var fiveMinutesAgoUnix = nowUnix - (5 * 60);
            var lastFiveMinutesTotals = await _countDataService.GetCountTotalsFilteredAsync(
                new List<int> { schedule.CameraID },
                fiveMinutesAgoUnix,
                nowUnix
            );

            lastFiveMinutesIn = lastFiveMinutesTotals.TotalIn;
            lastFiveMinutesOut = lastFiveMinutesTotals.TotalOut;
            lastFiveMinutesPresent = Math.Max(0, lastFiveMinutesIn - lastFiveMinutesOut);
        }
        else if (now > scheduleEndToday)
        {
            // Schedule completed today - get full schedule data
            var scheduleStartUnix = ((DateTimeOffset)scheduleStartToday.ToUniversalTime()).ToUnixTimeSeconds();
            var scheduleEndUnix = ((DateTimeOffset)scheduleEndToday.ToUniversalTime()).ToUnixTimeSeconds();
            
            var totals = await _countDataService.GetCountTotalsFilteredAsync(
                new List<int> { schedule.CameraID },
                scheduleStartUnix,
                scheduleEndUnix
            );

            totalIn = totals.TotalIn;
            totalOut = totals.TotalOut;
            totalPresent = Math.Max(0, totalIn - totalOut);
            hasData = true;
        }

        var status = now < scheduleStartToday ? "upcoming" : 
                     isActive ? "active" : "completed";

        return new JsonResult(new
        {
            success = true,
            scheduleId = scheduleId,
            scheduleName = schedule.ScheduleName,
            cameraName = camera?.CameraName ?? "Unknown Camera",
            status = status,
            isActive = isActive,
            hasData = hasData,
            startTime = scheduleStartToday.ToString("HH:mm"),
            endTime = scheduleEndToday.ToString("HH:mm"),
            data = new
            {
                totalIn = totalIn,
                totalOut = totalOut,
                totalPresent = totalPresent,
                lastFiveMinutesIn = lastFiveMinutesIn,
                lastFiveMinutesOut = lastFiveMinutesOut,
                lastFiveMinutesPresent = lastFiveMinutesPresent
            }
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Error getting dashboard data for schedule {scheduleId}: {ex.Message}");
        return new JsonResult(new
        {
            success = false,
            error = ex.Message
        });
    }
}
}