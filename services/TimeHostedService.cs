using Newtonsoft.Json;
using RestSharp;

public class TimedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TimedHostedService> _logger;
    private readonly HttpClient _httpClient;  
    private Timer _timer;

    public TimedHostedService(IServiceScopeFactory scopeFactory, 
        ILogger<TimedHostedService> logger, HttpClient httpClient) {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClient = httpClient; 
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        _timer = new Timer(HandleTimerElapsed, null, 0, 1000); //1 sec
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        _timer?.Dispose();
        return Task.CompletedTask;
    }

    private async void HandleTimerElapsed(object state) {
        try{
            Console.WriteLine("HandleTimerElapsed : " + DateTime.Now);
            
            long currentTimeSecs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using (var scope = _scopeFactory.CreateScope()) 
            {
                var cameraService = scope.ServiceProvider.GetRequiredService<CameraService>();
                var cameras = cameraService.GetCameras();  // Get camera data
                foreach (var camera in cameras) {
                    _logger.LogInformation($"CameraID: {camera.CameraID}, CameraName: {camera.CameraName}");
                }

                var allCamerasAPI = new List<Object>();
                var allCamerasDB = new List<Camera>();

                foreach(Camera camera in cameras) {
                    long lastRefreshTime = new DateTimeOffset(camera.LastRefreshTimestamp.ToUniversalTime()).ToUnixTimeSeconds();
                    long refreshRateSecs = camera.RefreshRateInSeconds;
                    long nextRefreshTime = lastRefreshTime + refreshRateSecs;
                    // long currentTimeSecs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    Console.WriteLine($"lastRefreshTime: {lastRefreshTime} cameraId: {camera.CameraID} currentTime: {currentTimeSecs} refreshRateSecs: {refreshRateSecs}");

                    if (currentTimeSecs < nextRefreshTime) {
                        //This camera does not need count right now ... skip
                        continue;
                    }
                    var newcamera = new  {
                        camera_name = camera.CameraName,  
                        camera_id = camera.CameraID,               
                        start_time = ((DateTimeOffset.UtcNow.ToUnixTimeSeconds() - refreshRateSecs)),
                        end_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    allCamerasAPI.Add(newcamera); // Add the camera object to the list
                    allCamerasDB.Add(camera);
                }


                if (allCamerasAPI.Count == 0) {
                    //List empty. Need not fire API ... return 
                    return;
                }
                UpdateLastFetchedTimestamps(allCamerasDB);
                // Serialize the list of cameras into a JSON string
                var values = new Dictionary<string, string> {
                    { "request_data", JsonConvert.SerializeObject(allCamerasAPI) }
                };
                Console.WriteLine($"API body: {JsonConvert.SerializeObject(allCamerasAPI)}");
                var content = new FormUrlEncodedContent(values);
                //TODO: handle exceptions here .... app should not crash for no n/w
                var response = await _httpClient.PostAsync("http://172.16.5.5:5100/multi-camera-occupancy-data-within-time-range", content);
                if (response.IsSuccessStatusCode){
                    var jsonString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"API response: {jsonString}");
                    //TODO: Add await   
                    SaveCountDataFromApiResponse(jsonString);
                }else {
                    var errorText = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"API Error: {response.StatusCode} - {errorText}");
                }
            }
        }catch (Exception ex) {
            _logger.LogError(ex, "Error occurred in HandleTimerElapsed");
            Console.WriteLine("Exception: " + ex.ToString());
        }
    }

    public async Task SaveCountDataFromApiResponse(string jsonString) {
        using (var scope = _scopeFactory.CreateScope()) {
            var countDataService = scope.ServiceProvider.GetRequiredService<CountDataService>();
            var jsonObject = JsonConvert.DeserializeObject<CountDataResponse>(jsonString);
            try {
                if (jsonObject != null && jsonObject.Status == "success" && jsonObject.Data.Any()) {
                    // Save the count data to the database
                    await countDataService.SaveCountDataAsync(jsonObject.Data);
                    Console.WriteLine("Count data has been saved.");
                }
            }catch (Exception ex) {
                // Catch and print any exception that occurs
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }
    }
    public async Task UpdateLastFetchedTimestamps(List<Camera> cameras) {
        long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        DateTime localTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime().DateTime;
        foreach(Camera camera in cameras) {
            camera.LastRefreshTimestamp = localTime;
        }

        using (var scope = _scopeFactory.CreateScope()) {
            var cameraService = scope.ServiceProvider.GetRequiredService<CameraService>();
            try {
                await cameraService.SaveOrUpdateCameras(cameras);
                Console.WriteLine("LastRefreshTimestamps updated");
            }catch (Exception ex) {
                // Catch and print any exception that occurs
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }
    }
}


// Loop to add camera objects to the list
// for (int i = 1; i <= numberOfCameras; i++)
// {
//     var camera = new 
//     {
//         camera_name = "camera1",  // Dynamically changing the camera name
//         camera_id = i,               // Dynamically changing the camera ID
//         start_time = 1743142013,
//         end_time = 1743142613
//     };
//     allCameras.Add(camera); // Add the camera object to the list
// }