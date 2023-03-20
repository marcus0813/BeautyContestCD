using System.ComponentModel.DataAnnotations;

namespace BeautyContestAPI.Entities
{
    public class Audit
    {
        public string SessionId { get; set; }
        public DateTime Created { get; set; } = DateTime.UtcNow;
    }
}