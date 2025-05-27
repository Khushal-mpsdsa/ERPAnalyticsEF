public class ScheduleService
{
    private readonly ApplicationDBContext _dbContext;

    // Inject ApplicationDBContext via constructor
    public ScheduleService(ApplicationDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Method to get all cameras from the database
   public List<Schedule> GetSchedules(int cameraID)
    {
        return _dbContext.Schedules
            .Where(s => s.CameraID == cameraID)
            .ToList();
    }

    public List<Schedule> GetScheduleByID(int scheduleID)
    {
        return _dbContext.Schedules
            .Where(s => s.ScheduleID == scheduleID)
            .ToList();
    }

    public async Task AddScheduleAsync(Schedule newSchedule)
    {
        _dbContext.Schedules.Add(newSchedule);
        await _dbContext.SaveChangesAsync();
    }

}