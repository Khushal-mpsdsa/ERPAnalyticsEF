using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


public class Camera {
    // Property for Camera Name
    public required string CameraName { get; set; }

    // Property for Camera ID
    public int CameraID { get; set; }

    public int RefreshRateInSeconds { get; set; }

    // Property for Last Refresh Timestamp
    public DateTime LastRefreshTimestamp { get; set; }

    public string? CameraAPIURL { get; set; }

}