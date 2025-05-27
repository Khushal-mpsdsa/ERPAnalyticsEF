using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class Schedule
{  
    public int ScheduleID { get; set; } //Schedule ID is auto generated .... 
    public int CameraID { get; set; }
    public string? ScheduleName { get; set; }
    public DateTime StartTime { get; set; }
    public int DurationInSec { get; set; }
}