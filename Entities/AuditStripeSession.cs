using System.ComponentModel.DataAnnotations;

namespace API.Entities
{
    public class AuditStripeSession
    {
        [Key]
        public string SessionId {get; set;}

    }
}