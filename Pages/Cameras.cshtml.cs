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

    private void LoadCameras()
    {
        Cameras = _cameraService.GetCameras();
    }
}