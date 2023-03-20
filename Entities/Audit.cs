using System.ComponentModel.DataAnnotations;

namespace BeautyContestAPI.Entities
{
    public class Audit
    {
        [Key]
        public string SessionId { get; set; }
        public DateTime Created { get; set; } = DateTime.UtcNow;
    }
}