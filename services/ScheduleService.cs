public class ScheduleService
{
    private readonly ApplicationDBContext _dbContext;

    // Inject ApplicationDBContext via constructor
    public ScheduleService(ApplicationDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Method to get all schedules for a camera
    public List<Schedule> GetSchedules(int cameraID)
    {
        return _dbContext.Schedules
            .Where(s => s.CameraID == cameraID)
            .ToList();
    }

    // Method to get schedule by ID
    public List<Schedule> GetScheduleByID(int scheduleID)
    {
        return _dbContext.Schedules
            .Where(s => s.ScheduleID == scheduleID)
            .ToList();
    }

    // Method to get single schedule by ID
    public Schedule? GetSingleScheduleByID(int scheduleID)
    {
        return _dbContext.Schedules
            .FirstOrDefault(s => s.ScheduleID == scheduleID);
    }

    // Method to add a new schedule
    public async Task AddScheduleAsync(Schedule newSchedule)
    {
        _dbContext.Schedules.Add(newSchedule);
        await _dbContext.SaveChangesAsync();
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
                return true;
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Method to check if schedule exists
    public bool ScheduleExists(int scheduleID)
    {
        return _dbContext.Schedules.Any(s => s.ScheduleID == scheduleID);
    }

    // Method to get schedule with camera name for better user feedback
    public async Task<(Schedule? schedule, string cameraName)> GetScheduleWithCameraAsync(int scheduleID)
    {
        try
        {
            var scheduleWithCamera = await Task.Run(() =>
                (from schedule in _dbContext.Schedules
                 join camera in _dbContext.Cameras on schedule.CameraID equals camera.CameraID
                 where schedule.ScheduleID == scheduleID
                 select new { schedule, camera.CameraName })
                .FirstOrDefault());

            if (scheduleWithCamera != null)
            {
                return (scheduleWithCamera.schedule, scheduleWithCamera.CameraName);
            }

            return (null, string.Empty);
        }
        catch (Exception)
        {
            return (null, string.Empty);
        }
    }

    // NEW METHOD: Get currently active schedule
    public Schedule? GetCurrentActiveSchedule()
    {
        var now = DateTime.Now;
        var today = now.Date;

        var allSchedules = _dbContext.Schedules.ToList();

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

            // Check if current time is within the schedule window
            if (now >= scheduleTimeToday && now <= scheduleEndTime)
            {
                return schedule;
            }
        }

        return null;
    }

    // NEW METHOD: Get schedule status for a specific time
    public ScheduleStatus GetScheduleStatusAtTime(DateTime checkTime)
    {
        var date = checkTime.Date;
        var allSchedules = _dbContext.Schedules.ToList();

        foreach (var schedule in allSchedules)
        {
            // Convert schedule start time to the check date with the same time
            var scheduleTimeOnDate = new DateTime(
                date.Year, 
                date.Month, 
                date.Day,
                schedule.StartTime.Hour,
                schedule.StartTime.Minute,
                schedule.StartTime.Second
            );

            var scheduleEndTime = scheduleTimeOnDate.AddSeconds(schedule.DurationInSec);

            // Check if the time is within the schedule window
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

    // NEW METHOD: Get all schedules for today
    public List<TodayScheduleInfo> GetTodaysSchedules()
    {
        var today = DateTime.Today;
        var allSchedules = _dbContext.Schedules.ToList();
        var todaySchedules = new List<TodayScheduleInfo>();

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
            var now = DateTime.Now;

            var status = ScheduleStatusType.Upcoming;
            if (now >= scheduleTimeToday && now <= scheduleEndTime)
            {
                status = ScheduleStatusType.Active;
            }
            else if (now > scheduleEndTime)
            {
                status = ScheduleStatusType.Completed;
            }

            todaySchedules.Add(new TodayScheduleInfo
            {
                Schedule = schedule,
                ScheduleStartToday = scheduleTimeToday,
                ScheduleEndToday = scheduleEndTime,
                Status = status
            });
        }

        return todaySchedules.OrderBy(s => s.ScheduleStartToday).ToList();
    }

    // NEW METHOD: Get next upcoming schedule
    public Schedule? GetNextUpcomingSchedule()
    {
        var now = DateTime.Now;
        var today = now.Date;
        var tomorrow = today.AddDays(1);

        var allSchedules = _dbContext.Schedules.ToList();
        var upcomingSchedules = new List<(Schedule schedule, DateTime scheduletime)>();

        // Check schedules for today
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

            if (scheduleTimeToday > now)
            {
                upcomingSchedules.Add((schedule, scheduleTimeToday));
            }
        }

        // If no upcoming schedules today, check tomorrow
        if (!upcomingSchedules.Any())
        {
            foreach (var schedule in allSchedules)
            {
                var scheduleTimeTomorrow = new DateTime(
                    tomorrow.Year, 
                    tomorrow.Month, 
                    tomorrow.Day,
                    schedule.StartTime.Hour,
                    schedule.StartTime.Minute,
                    schedule.StartTime.Second
                );

                upcomingSchedules.Add((schedule, scheduleTimeTomorrow));
            }
        }

        return upcomingSchedules
            .OrderBy(s => s.scheduletime)
            .FirstOrDefault().schedule;
    }

    // NEW METHOD: Check if a time range overlaps with any existing schedule
    public bool HasScheduleConflict(DateTime startTime, int durationInSeconds, int? excludeScheduleId = null)
    {
        var endTime = startTime.AddSeconds(durationInSeconds);
        var date = startTime.Date;

        var allSchedules = _dbContext.Schedules.ToList();

        foreach (var schedule in allSchedules)
        {
            // Skip the schedule we're updating
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

            // Check for overlap
            if (startTime < scheduleEndOnDate && endTime > scheduleStartOnDate)
            {
                return true; // Conflict found
            }
        }

        return false; // No conflict
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
    public Schedule Schedule { get; set; }
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