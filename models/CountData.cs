using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class CountData
{
    [Key]  
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]  // Auto-increment
    public int SrNo { get; set; }

    [JsonProperty("camera_id")] 
    public int CameraId { get; set; }

    [JsonProperty("camera_name")] 
    public required string CameraName { get; set; }

    [JsonProperty("in")] 
    public int In { get; set; }

    [JsonProperty("out")] 
    public int Out { get; set; }

    [JsonProperty("start_time")]
    public long StartTime { get; set; }

    [JsonIgnore]
    public DateTime Date => DateTimeOffset.FromUnixTimeSeconds(StartTime).ToLocalTime().Date;

    [JsonIgnore]
    public TimeSpan Time => DateTimeOffset.FromUnixTimeSeconds(StartTime).ToLocalTime().TimeOfDay;

    // If you still want to deserialize end_time but ignore it for DB:
    [JsonProperty("end_time")]
    public long EndTime { get; set; }
    
    // These will be persisted in the DB:
    public DateTime DateOnly { get; set; }
    public TimeSpan TimeOnly { get; set; }
}