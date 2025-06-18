using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

public class CamerasModel : PageModel
{
    private readonly CameraService _cameraService;

    public CamerasModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }

    [BindProperty]
    public Camera NewCamera { get; set; }

    public List<Camera> Cameras { get; set; }

    public void OnGet()
    {
        LoadCameras();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            LoadCameras();
            return Page();
        }

        // Set default timestamp (server-side)
        NewCamera.LastRefreshTimestamp = DateTime.UtcNow;

        await _cameraService.SaveOrUpdateCameras(new List<Camera> { NewCamera });

        // Reset form and reload list
        ModelState.Clear();
        LoadCameras();

        return Page();
    }

    // Handler for deleting a camera
    public async Task<IActionResult> OnPostDeleteCameraAsync([FromBody] DeleteCameraRequest request)
    {
        try
        {
            // Check if camera exists
            var camera = _cameraService.GetCameraById(request.CameraId);
            if (camera == null)
            {
                return new JsonResult(new { success = false, message = "Camera not found" });
            }

            // Check if camera is being used in schedules
            if (_cameraService.IsCameraInUse(request.CameraId))
            {
                return new JsonResult(new { 
                    success = false, 
                    message = "Cannot delete camera as it is being used in active schedules. Please remove the schedules first." 
                });
            }

            // Delete the camera
            bool deleted = await _cameraService.DeleteCameraAsync(request.CameraId);
            
            if (deleted)
            {
                return new JsonResult(new { 
                    success = true, 
                    message = $"Camera '{camera.CameraName}' has been successfully deleted." 
                });
            }
            else
            {
                return new JsonResult(new { 
                    success = false, 
                    message = "Failed to delete camera. Please try again." 
                });
            }
        }
        catch (Exception ex)
        {
            return new JsonResult(new { 
                success = false, 
                message = "An error occurred while deleting the camera: " + ex.Message 
            });
        }
    }

    // Handler for getting camera details
    public async Task<IActionResult> OnGetCameraDetailsAsync([FromQuery] int cameraId)
    {
        try
        {
            var camera = _cameraService.GetCameraById(cameraId);
            if (camera == null)
            {
                return new JsonResult(new { success = false, message = "Camera not found" });
            }

            bool isInUse = _cameraService.IsCameraInUse(cameraId);

            return new JsonResult(new { 
                success = true, 
                camera = new {
                    id = camera.CameraID,
                    name = camera.CameraName,
                    refreshRate = camera.RefreshRateInSeconds,
                    lastRefresh = camera.LastRefreshTimestamp,
                    apiUrl = camera.CameraAPIURL,
                    isInUse = isInUse
                }
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { 
                success = false, 
                message = "Error retrieving camera details: " + ex.Message 
            });
        }
    }

    private void LoadCameras()
    {
        Cameras = _cameraService.GetCameras();
    }

    // Request model for delete operation
    public class DeleteCameraRequest
    {
        public int CameraId { get; set; }
    }
}