public class TimerData
{
    public int RemainingSeconds { get; set; }
    public bool IsActive { get; set; }
    public DateTime? StartTime { get; set; }
    public string Lane { get; set; }

    public TimerData(string lane)
    {
        Lane = lane;
        RemainingSeconds = 0;
        IsActive = false;
        StartTime = null;
    }
} 