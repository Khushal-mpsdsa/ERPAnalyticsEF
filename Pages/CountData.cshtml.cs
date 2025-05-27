using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

public class CountDataModel(CountDataService _countDataService, CameraService _cameraService) : PageModel
{
    public List<CountData> CountDataList { get; set; }
    public required List<Camera> Cameras { get; set; }

    public DateTime? FilterDate { get; set; }
    public int? SelectedCameraId { get; set; }

    public void OnGet(DateTime? filterDate, int? cameraId)
    {
        FilterDate = filterDate;
        SelectedCameraId = cameraId;
        Cameras = _cameraService.GetCameras();

        if (filterDate.HasValue && cameraId.HasValue) {
            CountDataList = _countDataService
                .GetCountDataByDateAndCamera(filterDate.Value, cameraId.Value)
                .AsEnumerable()
                .Reverse()
                .ToList(); 
        }
        else {
            CountDataList = new List<CountData>();
        }
    }

    // public void OnGet(DateTime? filterDate, int? cameraId)
    // {
    //     FilterDate = filterDate;
    //     SelectedCameraId = cameraId;
    //     Cameras = _cameraService.GetCameras();

    //     if (filterDate.HasValue && cameraId.HasValue)
    //     {
    //         CountDataList = _countDataService.GetCountDataByDateAndCamera(filterDate.Value, cameraId.Value);
    //     }
    //     else
    //     {
    //         CountDataList = new List<CountData>(); // or load all if you prefer
    //     }
    // }
}