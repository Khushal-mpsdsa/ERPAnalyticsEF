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
}