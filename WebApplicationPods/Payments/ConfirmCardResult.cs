namespace WebApplicationPods.Payments
{
    public class ConfirmCardResult
    {
        public bool Success { get; set; }
        public string? Brand { get; set; }
        public string? Last4 { get; set; }
        public string? FailureReason { get; set; }
    }
}
