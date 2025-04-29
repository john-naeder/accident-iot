using System.ComponentModel.DataAnnotations;

namespace AccidentMonitor.WebApp.Models;

public class LoginRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    
    public bool RememberMe { get; set; }
}

public class LoginResponse
{
    public bool Successful { get; set; }
    public string? Error { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? Expiry { get; set; }
}

public class RegistrationRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Please confirm your password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class RegistrationResponse
{
    public bool Successful { get; set; }
    public IEnumerable<string>? Errors { get; set; }
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public IList<string>? Roles { get; set; }
}
