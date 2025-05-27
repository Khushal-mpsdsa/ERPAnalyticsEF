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
    public required List<Camera> Cameras { get; set; }

    public int TotalIn { get; set; }
    public int TotalOut { get; set; }
    public int TotalPresent { get; set; }

    public async Task OnGetAsync()
    {
        // Synchronous call for camera list (if you need to populate it for UI)
        Cameras = _cameraService.GetCameras();
    }

    public async Task<JsonResult> OnGetGetSchedulesAsync([FromQuery] int cameraId) {
        // Ideally, make your GetSchedules method async if DB supports it
        var schedules = _scheduleService.GetSchedules(cameraId);
        return new JsonResult(schedules);
    }

    public async Task<JsonResult> OnGetGetScheduleByIDAsync([FromQuery] int scheduleID) {
        // Ideally, make your GetSchedules method async if DB supports it
        var schedule = _scheduleService.GetScheduleByID(scheduleID);
        return new JsonResult(schedule);
    }

    public async Task<JsonResult> OnGetGetPeopleCountAsync([FromQuery] List<int> cameraIds,
        [FromQuery] long from,
        [FromQuery] long to) {
        int totalPresent = 0;
        (int totalIn, int totalOut) = await _countDataService.GetCountTotalsFilteredAsync(cameraIds, from, to);
        totalPresent = totalIn - totalOut;
        TotalIn = totalIn;
        TotalOut = totalOut;
        TotalPresent = totalIn - totalOut;
        return new JsonResult(new
        {
            totalIn,
            totalOut,
            totalPresent
        });
    }

}
