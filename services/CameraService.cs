public class CameraService
{
    private readonly ApplicationDBContext _dbContext;

    // Inject ApplicationDBContext via constructor
    public CameraService(ApplicationDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Method to get all cameras from the database
    public List<Camera> GetCameras()
    {
        return _dbContext.Cameras.ToList();
    }
    public async Task SaveOrUpdateCameras(List<Camera> cameras)
    {
        foreach (var camera in cameras)
        {
            var existingCamera = _dbContext.Cameras.FirstOrDefault(c => c.CameraID == camera.CameraID);
            
            if (existingCamera != null)
            {
                // Update fields
                existingCamera.CameraName = camera.CameraName;
                existingCamera.CameraID = camera.CameraID;
                existingCamera.RefreshRateInSeconds = camera.RefreshRateInSeconds;
                existingCamera.LastRefreshTimestamp = camera.LastRefreshTimestamp;
            }
            else
            {
                // Insert new camera
                _dbContext.Cameras.Add(camera);
            }
        }
        _dbContext.SaveChanges();
    }
}