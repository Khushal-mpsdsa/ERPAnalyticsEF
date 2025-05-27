using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

public class ScheduleModel(ScheduleService _scheduleService, CameraService _cameraService) : PageModel
{

    public List<Camera> Cameras { get; set; }
    [BindProperty]
    public string? ScheduleName { get; set; }

    [BindProperty]
    public string? StartTime { get; set; }

    [BindProperty]
    public int DurationInSec { get; set; }

    public async Task<IActionResult> OnPostAddSchedule([FromBody] Schedule schedule)
    {
        try{
            await _scheduleService.AddScheduleAsync(schedule);
            // Example return: returning CameraID as "count"
            return new JsonResult(new { count = schedule.ScheduleName });
        }
        catch (Exception ex) {
            // Log the exception if needed
            return StatusCode(500, "Internal server error: " + ex.Message);
        }
    }

    public class ScheduleInput
    {
        public int CameraID { get; set; }
        public string? ScheduleName { get; set; }
        public string? StartTime { get; set; }
        public int DurationInSec { get; set; }
    }

    public async Task OnGetAsync() {
        // Synchronous call for camera list (if you need to populate it for UI)
        Cameras = _cameraService.GetCameras();
    }
    public async Task<JsonResult> OnGetGetSchedulesAsync([FromQuery] int cameraId) {
        // Ideally, make your GetSchedules method async if DB supports it
        var schedules = _scheduleService.GetSchedules(cameraId);
        return new JsonResult(schedules);
    }
}