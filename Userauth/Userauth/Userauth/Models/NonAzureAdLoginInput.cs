using System.ComponentModel.DataAnnotations;

namespace Userauth.Models
{
    public class NonAzureAdLoginInput
    {
        [Required] public string UserName { get; set; }

        [Required] public string Password { get; set; }
    }
}
