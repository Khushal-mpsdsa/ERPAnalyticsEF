using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

public class ScheduleModel(ScheduleService _scheduleService, CameraService _cameraService) : PageModel
{

    public List<Camera> Cameras { get; set; }     // API endpoint to delete a schedule (Enhanced with logging)
    public async Task<JsonResult> OnPostDeleteScheduleAsync([FromBody] DeleteScheduleRequest request)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Delete request received for schedule ID: {request?.ScheduleID}");
            
            if (request == null || request.ScheduleID <= 0)
            {
                Console.WriteLine($"[ERROR] Invalid request: {request?.ScheduleID}");
                return new JsonResult(new { success = false, message = "Invalid schedule ID" });
            }

            // Check if schedule exists first
            if (!_scheduleService.ScheduleExists(request.ScheduleID))
            {
                Console.WriteLine($"[ERROR] Schedule {request.ScheduleID} does not exist");
                return new JsonResult(new { success = false, message = "Schedule not found" });
            }

            // Get schedule details before deletion for better user feedback
            var (schedule, cameraName) = await _scheduleService.GetScheduleWithCameraAsync(request.ScheduleID);
            
            if (schedule == null)
            {
                Console.WriteLine($"[ERROR] Schedule {request.ScheduleID} could not be retrieved");
                return new JsonResult(new { success = false, message = "Schedule not found" });
            }

            Console.WriteLine($"[DEBUG] Deleting schedule: {schedule.ScheduleName} (ID: {schedule.ScheduleID})");

            // Delete the schedule
            bool deleted = await _scheduleService.DeleteScheduleAsync(request.ScheduleID);
            
            if (deleted)
            {
                Console.WriteLine($"[SUCCESS] Schedule {request.ScheduleID} deleted successfully");
                return new JsonResult(new { 
                    success = true, 
                    message = $"Schedule '{schedule.ScheduleName}' has been successfully deleted.",
                    scheduleId = request.ScheduleID,
                    cameraId = schedule.CameraID
                });
            }
            else
            {
                Console.WriteLine($"[ERROR] Failed to delete schedule {request.ScheduleID}");
                return new JsonResult(new { 
                    success = false, 
                    message = "Failed to delete schedule. Please try again." 
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Exception in delete handler: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            return new JsonResult(new { 
                success = false, 
                message = "An error occurred while deleting the schedule: " + ex.Message 
            });
        }
    }

    // Request model for delete operation
    public class DeleteScheduleRequest
    {
        public int ScheduleID { get; set; }
    }

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
            // Example return: returning ScheduleName as "count"
            return new JsonResult(new { success = true, count = schedule.ScheduleName, message = "Schedule added successfully" });
        }
        catch (Exception ex) {
            // Log the exception if needed
            return new JsonResult(new { success = false, message = "Internal server error: " + ex.Message });
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
        // Load camera list for UI
        Cameras = _cameraService.GetCameras();
    }

    // API endpoint to get schedules for a specific camera
    public async Task<JsonResult> OnGetGetSchedulesAsync([FromQuery] int cameraId) {
        try 
        {
            if (cameraId <= 0)
            {
                return new JsonResult(new { success = false, message = "Invalid camera ID", data = new List<Schedule>() });
            }

            var schedules = _scheduleService.GetSchedules(cameraId);
            
            // Format the schedules for JSON response
            var formattedSchedules = schedules.Select(s => new {
                scheduleID = s.ScheduleID,
                cameraID = s.CameraID,
                scheduleName = s.ScheduleName,
                startTime = s.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                durationInSec = s.DurationInSec
            }).ToList();

            return new JsonResult(new { 
                success = true, 
                data = formattedSchedules,
                count = formattedSchedules.Count,
                message = $"Found {formattedSchedules.Count} schedules for camera {cameraId}"
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { 
                success = false, 
                message = "Error retrieving schedules: " + ex.Message,
                data = new List<Schedule>()
            });
        }
    }

    // API endpoint to get schedule by ID
    public async Task<JsonResult> OnGetGetScheduleByIDAsync([FromQuery] int scheduleID) {
        try 
        {
            if (scheduleID <= 0)
            {
                return new JsonResult(new { success = false, message = "Invalid schedule ID", data = new List<Schedule>() });
            }

            var schedules = _scheduleService.GetScheduleByID(scheduleID);
            
            if (schedules != null && schedules.Any())
            {
                var schedule = schedules.First();
                var formattedSchedule = new {
                    scheduleID = schedule.ScheduleID,
                    cameraID = schedule.CameraID,
                    scheduleName = schedule.ScheduleName,
                    startTime = schedule.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    durationInSec = schedule.DurationInSec
                };

                return new JsonResult(new { 
                    success = true, 
                    data = new[] { formattedSchedule },
                    message = "Schedule found successfully"
                });
            }
            else
            {
                return new JsonResult(new { 
                    success = false, 
                    message = "Schedule not found",
                    data = new List<Schedule>()
                });
            }
        }
        catch (Exception ex)
        {
            return new JsonResult(new { 
                success = false, 
                message = "Error retrieving schedule: " + ex.Message,
                data = new List<Schedule>()
            });
        }
    }
}