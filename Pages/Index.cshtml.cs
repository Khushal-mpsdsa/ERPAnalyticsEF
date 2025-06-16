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
        ActiveCameras = Cameras.Count(c => c.RefreshRateInSeconds > 0); // Assuming active if refresh rate > 0

        // Get today's counts
        var today = DateTime.Today;
        var startOfDay = ((DateTimeOffset)today.ToUniversalTime()).ToUnixTimeSeconds();
        var endOfDay = ((DateTimeOffset)today.AddDays(1).ToUniversalTime()).ToUnixTimeSeconds();
        var allCameraIds = Cameras.Select(c => c.CameraID).ToList();

        if (allCameraIds.Any())
        {
            var todayTotals = await _countDataService.GetCountTotalsFilteredAsync(allCameraIds, startOfDay, endOfDay);
            TotalIn = todayTotals.TotalIn;
            TotalOut = todayTotals.TotalOut;
            TotalPresent = TotalIn - TotalOut;
        }
        else
        {
            TotalIn = 0;
            TotalOut = 0;
            TotalPresent = 0;
        }
    }

    // API endpoint for getting people count
    public async Task<JsonResult> OnGetGetPeopleCountAsync([FromQuery] List<int> cameraIds,
        [FromQuery] long from,
        [FromQuery] long to)
    {
        var totals = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, from, to);
        int totalPresent = totals.TotalIn - totals.TotalOut;

        return new JsonResult(new
        {
            totalIn = totals.TotalIn,
            totalOut = totals.TotalOut,
            totalPresent
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

        // Get today's data
        var today = DateTime.Today;
        var startOfDay = ((DateTimeOffset)today.ToUniversalTime()).ToUnixTimeSeconds();
        var endOfDay = ((DateTimeOffset)today.AddDays(1).ToUniversalTime()).ToUnixTimeSeconds();
        var allCameraIds = cameras.Select(c => c.CameraID).ToList();

        var todayTotals = new CountDataService.CountTotals();
        if (allCameraIds.Any())
        {
            todayTotals = await _countDataService.GetCountTotalsFilteredAsync(allCameraIds, startOfDay, endOfDay);
        }

        return new JsonResult(new
        {
            totalCameras,
            totalCameraCapacity = 32, // You might want to make this configurable
            activeCameras,
            peopleIn = todayTotals.TotalIn,
            peopleInCapacity = 165, // You might want to make this configurable
            peopleOut = todayTotals.TotalOut,
            totalCamerasTrend = 4, // Calculate based on historical data
            activeCamerasTrend = 0,
            peopleInChangeTrend = -12, // Calculate based on historical data
            peopleOutTrend = 8
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
            location = new { x = 10 + (c.CameraID * 15) % 80, y = 20 + (c.CameraID * 25) % 60 } // Mock locations
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
            if (cameraIds == null || !cameraIds.Any())
            {
                // If no specific cameras, use all cameras
                cameraIds = _cameraService.GetCameras().Select(c => c.CameraID).ToList();
            }

            var currentCount = await _countDataService.GetCurrentPeopleCountAsync(cameraIds);
            
            return new JsonResult(new
            {
                success = true,
                currentCount = currentCount,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }
        catch (Exception ex)
        {
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

            var count = await _countDataService.GetPeopleCountAtTimeAsync(cameraIds, fromTime, toTime);
            
            return new JsonResult(new
            {
                success = true,
                count = count,
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
}