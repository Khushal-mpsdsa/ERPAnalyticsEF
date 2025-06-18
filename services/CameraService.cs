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

    // Method to get a camera by ID
    public Camera? GetCameraById(int cameraId)
    {
        return _dbContext.Cameras.FirstOrDefault(c => c.CameraID == cameraId);
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
                existingCamera.CameraAPIURL = camera.CameraAPIURL;
            }
            else
            {
                // Insert new camera
                _dbContext.Cameras.Add(camera);
            }
        }
        await _dbContext.SaveChangesAsync();
    }

    // Method to delete a camera by ID
    public async Task<bool> DeleteCameraAsync(int cameraId)
    {
        try
        {
            var camera = await _dbContext.Cameras.FindAsync(cameraId);
            if (camera != null)
            {
                _dbContext.Cameras.Remove(camera);
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

    // Method to check if camera is being used in schedules
    public bool IsCameraInUse(int cameraId)
    {
        return _dbContext.Schedules.Any(s => s.CameraID == cameraId);
    }
}