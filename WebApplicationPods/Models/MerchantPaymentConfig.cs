namespace WebApplicationPods.Models
{
    public class MerchantPaymentConfig
    {
        public int Id { get; set; }
        public int UserId { get; set; }           // int (FK para ApplicationUser.Id)
        public string Provider { get; set; } = "";
        public string ConfigJson { get; set; } = "";
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
