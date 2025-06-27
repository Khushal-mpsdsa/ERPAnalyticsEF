using Microsoft.EntityFrameworkCore;

public class ScheduleService
{
    private readonly ApplicationDBContext _dbContext;

    public ScheduleService(ApplicationDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ====================
    // OPTIMIZED METHODS (NEW & FIXED)
    // ====================

    // FIXED: Get all schedules with camera information in a single query
    public List<dynamic> GetAllSchedulesWithCameraInfo()
    {
        try
        {
            Console.WriteLine("[SCHEDULE SERVICE] Starting GetAllSchedulesWithCameraInfo");
            
            // FIXED: Proper LINQ JOIN syntax
            var schedulesWithCamera = (from schedule in _dbContext.Schedules.AsNoTracking()
                                     join camera in _dbContext.Cameras.AsNoTracking() 
                                     on schedule.CameraID equals camera.CameraID
                                     select new
                                     {
                                         ScheduleID = schedule.ScheduleID,
                                         ScheduleName = schedule.ScheduleName,
                                         StartTime = schedule.StartTime,
                                         DurationInSec = schedule.DurationInSec,
                                         CameraID = schedule.CameraID,
                                         CameraName = camera.CameraName
                                     })
                                     .OrderBy(s => s.StartTime)
                                     .ToList<dynamic>();

            Console.WriteLine($"[SCHEDULE SERVICE] Successfully loaded {schedulesWithCamera.Count} schedules with camera info");
            
            return schedulesWithCamera;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SCHEDULE SERVICE ERROR] GetAllSchedulesWithCameraInfo failed: {ex.Message}");
            
            // FALLBACK: Try alternative approach using separate queries
            try 
            {
                Console.WriteLine("[SCHEDULE SERVICE] Attempting fallback method");
                
                var cameras = _dbContext.Cameras.AsNoTracking().ToList();
                var schedules = _dbContext.Schedules.AsNoTracking().ToList();
                
                var result = new List<dynamic>();
                
                foreach (var schedule in schedules)
                {
                    var camera = cameras.FirstOrDefault(c => c.CameraID == schedule.CameraID);
                    result.Add(new
                    {
                        ScheduleID = schedule.ScheduleID,
                        ScheduleName = schedule.ScheduleName ?? "Unnamed Schedule",
                        StartTime = schedule.StartTime,
                        DurationInSec = schedule.DurationInSec,
                        CameraID = schedule.CameraID,
                        CameraName = camera?.CameraName ?? "Unknown Camera"
                    });
                }
                
                Console.WriteLine($"[SCHEDULE SERVICE] Fallback method successfully loaded {result.Count} schedules");
                return result;
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"[SCHEDULE SERVICE FALLBACK ERROR] {fallbackEx.Message}");
                return new List<dynamic>();
            }
        }
    }

    // NEW: Optimized method to get schedules for multiple cameras at once
    public List<Schedule> GetSchedulesForCameras(List<int> cameraIds)
    {
        try
        {
            if (cameraIds == null || !cameraIds.Any())
            {
                return new List<Schedule>();
            }

            var schedules = _dbContext.Schedules
                .AsNoTracking()
                .Where(s => cameraIds.Contains(s.CameraID))
                .OrderBy(s => s.CameraID)
                .ThenBy(s => s.StartTime)
                .ToList();

            Console.WriteLine($"[SCHEDULE SERVICE] Loaded {schedules.Count} schedules for {cameraIds.Count} cameras");
            return schedules;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SCHEDULE SERVICE ERROR] GetSchedulesForCameras failed: {ex.Message}");
            return new List<Schedule>();
        }
    }

    // OPTIMIZED: Enhanced existing method with AsNoTracking
    public List<Schedule> GetSchedules(int cameraID)
    {
        try
        {
            var schedules = _dbContext.Schedules
                .AsNoTracking()
                .Where(s => s.CameraID == cameraID)
                .OrderBy(s => s.StartTime)
                .ToList();

            Console.WriteLine($"[SCHEDULE SERVICE] Loaded {schedules.Count} schedules for camera {cameraID}");
            return schedules;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SCHEDULE SERVICE ERROR] GetSchedules failed for camera {cameraID}: {ex.Message}");
            return new List<Schedule>();
        }
    }

    // OPTIMIZED: Method to get schedule by ID with AsNoTracking
    public List<Schedule> GetScheduleByID(int scheduleID)
    {
        try
        {
            var schedules = _dbContext.Schedules
                .AsNoTracking()
                .Where(s => s.ScheduleID == scheduleID)
                .ToList();

            Console.WriteLine($"[SCHEDULE SERVICE] Found {schedules.Count} schedules with ID {scheduleID}");
            return schedules;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SCHEDULE SERVICE ERROR] GetScheduleByID failed for ID {scheduleID}: {ex.Message}");
            return new List<Schedule>();
        }
    }

    // OPTIMIZED: Method to get single schedule by ID with AsNoTracking
    public Schedule? GetSingleScheduleByID(int scheduleID)
    {
        try
        {
            var schedule = _dbContext.Schedules
                .AsNoTracking()
                .FirstOrDefault(s => s.ScheduleID == scheduleID);

            return schedule;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SCHEDULE SERVICE ERROR] GetSingleScheduleByID failed for ID {scheduleID}: {ex.Message}");
            return null;
        }
    }

    // Method to add a new schedule
    public async Task AddScheduleAsync(Schedule newSchedule)
    {
        try
        {
            _dbContext.Schedules.Add(newSchedule);
            await _dbContext.SaveChangesAsync();
            Console.WriteLine($"[SCHEDULE SERVICE] Successfully added new schedule: {newSchedule.ScheduleName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SCHEDULE SERVICE ERROR] Adding schedule failed: {ex.Message}");
            throw;
        }
    }

    // Method to delete a schedule by ID
    public async Task<bool> DeleteScheduleAsync(int scheduleID)
    {
        try
        {
            var schedule = await _dbContext.Schedules.FindAsync(scheduleID);
            if (schedule != null)
            {
                _dbContext.Schedules.Remove(schedule);
                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"[SCHEDULE SERVICE] Successfully deleted schedule {scheduleID}");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SCHEDULE SERVICE ERROR] Deleting schedule {scheduleID} failed: {ex.Message}");
            return false;
        }
    }

    // OPTIMIZED: Method to check if schedule exists with AsNoTracking
    public bool ScheduleExists(int scheduleID)
    {
        try
        {
            return _dbContext.Schedules
                .AsNoTracking()
                .Any(s => s.ScheduleID == scheduleID);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SCHEDULE SERVICE ERROR] Checking schedule existence {scheduleID} failed: {ex.Message}");
            return false;
        }
    }

    // OPTIMIZED: Method to get schedule with camera name with AsNoTracking
    public async Task<(Schedule? schedule, string cameraName)> GetScheduleWithCameraAsync(int scheduleID)
    {
        try
        {
            var scheduleWithCamera = await Task.Run(() =>
                (from schedule in _dbContext.Schedules.AsNoTracking()
                 join camera in _dbContext.Cameras.AsNoTracking() on schedule.CameraID equals camera.CameraID
                 where schedule.ScheduleID == scheduleID
                 select new { schedule, camera.CameraName })
                .FirstOrDefault());

            if (scheduleWithCamera != null)
            {
                return (scheduleWithCamera.schedule, scheduleWithCamera.CameraName);
            }

            return (null, string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SCHEDULE SERVICE ERROR] GetScheduleWithCamera {scheduleID} failed: {ex.Message}");
            return (null, string.Empty);
        }
    }

    // OPTIMIZED: Get currently active schedule
    public Schedule? GetCurrentActiveSchedule()
    {
        try
        {
            var now = DateTime.Now;
            var today = now.Date;

            var allSchedules = _dbContext.Schedules.AsNoTracking().ToList();

            foreach (var schedule in allSchedules)
            {
                var scheduleTimeToday = new DateTime(
                    today.Year, 
                    today.Month, 
                    today.Day,
                    schedule.StartTime.Hour,
                    schedule.StartTime.Minute,
                    schedule.StartTime.Second
                );

                var scheduleEndTime = scheduleTimeToday.AddSeconds(schedule.DurationInSec);

                if (now >= scheduleTimeToday && now <= scheduleEndTime)
                {
                    Console.WriteLine($"[SCHEDULE SERVICE] Active schedule found: {schedule.ScheduleName}");
                    return schedule;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SCHEDULE SERVICE ERROR] GetCurrentActiveSchedule failed: {ex.Message}");
            return null;
        }
    }

    // NEW METHOD: Get schedule status for a specific time
    public ScheduleStatus GetScheduleStatusAtTime(DateTime checkTime)
    {
        try
        {
            var date = checkTime.Date;
            var allSchedules = _dbContext.Schedules.AsNoTracking().ToList();

            foreach (var schedule in allSchedules)
            {
                var scheduleTimeOnDate = new DateTime(
                    date.Year, 
                    date.Month, 
                    date.Day,
                    schedule.StartTime.Hour,
                    schedule.StartTime.Minute,
                    schedule.StartTime.Second
                );

                var scheduleEndTime = scheduleTimeOnDate.AddSeconds(schedule.DurationInSec);

                if (checkTime >= scheduleTimeOnDate && checkTime <= scheduleEndTime)
                {
                    var timeRemaining = scheduleEndTime - checkTime;
                    return new ScheduleStatus
                    {
                        IsActive = true,
                        Schedule = schedule,
                        TimeRemaining = timeRemaining,
                        ScheduleStart = scheduleTimeOnDate,
                        ScheduleEnd = scheduleEndTime
                    };
                }
            }

            return new ScheduleStatus
            {
                IsActive = false,
                Schedule = null,
                TimeRemaining = TimeSpan.Zero,
                ScheduleStart = DateTime.MinValue,
                ScheduleEnd = DateTime.MinValue
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SCHEDULE SERVICE ERROR] GetScheduleStatusAtTime failed: {ex.Message}");
            return new ScheduleStatus
            {
                IsActive = false,
                Schedule = null,
                TimeRemaining = TimeSpan.Zero,
                ScheduleStart = DateTime.MinValue,
                ScheduleEnd = DateTime.MinValue
            };
        }
    }

    // NEW METHOD: Check if a time range overlaps with any existing schedule
    public bool HasScheduleConflict(DateTime startTime, int durationInSeconds, int? excludeScheduleId = null)
    {
        try
        {
            var endTime = startTime.AddSeconds(durationInSeconds);
            var date = startTime.Date;

            var allSchedules = _dbContext.Schedules.AsNoTracking().ToList();

            foreach (var schedule in allSchedules)
            {
                if (excludeScheduleId.HasValue && schedule.ScheduleID == excludeScheduleId.Value)
                    continue;

                var scheduleStartOnDate = new DateTime(
                    date.Year, 
                    date.Month, 
                    date.Day,
                    schedule.StartTime.Hour,
                    schedule.StartTime.Minute,
                    schedule.StartTime.Second
                );

                var scheduleEndOnDate = scheduleStartOnDate.AddSeconds(schedule.DurationInSec);

                if (startTime < scheduleEndOnDate && endTime > scheduleStartOnDate)
                {
                    return true; // Conflict found
                }
            }

            return false; // No conflict
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SCHEDULE SERVICE ERROR] HasScheduleConflict failed: {ex.Message}");
            return false;
        }
    }
}

// Supporting classes for schedule status
public class ScheduleStatus
{
    public bool IsActive { get; set; }
    public Schedule? Schedule { get; set; }
    public TimeSpan TimeRemaining { get; set; }
    public DateTime ScheduleStart { get; set; }
    public DateTime ScheduleEnd { get; set; }
}

public class TodayScheduleInfo
{
    public Schedule Schedule { get; set; } = new Schedule();
    public DateTime ScheduleStartToday { get; set; }
    public DateTime ScheduleEndToday { get; set; }
    public ScheduleStatusType Status { get; set; }
}

public enum ScheduleStatusType
{
    Upcoming,
    Active,
    Completed
}