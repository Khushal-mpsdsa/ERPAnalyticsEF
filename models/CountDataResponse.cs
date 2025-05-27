using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;

[NotMapped]
public class CountDataResponse
{
    public required string Status { get; set; }
    public required List<CountData> Data { get; set; }
}

