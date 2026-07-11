namespace Identity.API.Endpoints;

public record RegisterResponse(string UserId, string Email);

public record MessageResponse(string Message);

public record ValidateResetCodeResponse(string UserId);

public record ProfileResponse(string UserName, string Email, string? Name, string? LastName, string ProfilePicture);
