using System.Globalization;
using System.Security.Claims;
using Duende.IdentityServer;
using Identity.API.Services.Account;

namespace Identity.API.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account");

        group.MapPost("/register", RegisterAsync)
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .RequireRateLimiting("register");

        group.MapPost("/register/resend-verification-email", ResendVerificationEmailAsync)
            .AddEndpointFilter<ValidationFilter<ResendVerificationEmailRequest>>()
            .RequireRateLimiting("register");

        group.MapPost("/forgot-password", ForgotPasswordAsync)
            .AddEndpointFilter<ValidationFilter<ForgotPasswordRequest>>()
            .RequireRateLimiting("forgot-password");

        group.MapGet("/reset-password/validate", ValidateResetCodeAsync)
            .RequireRateLimiting("reset-password");

        group.MapPost("/reset-password", ResetPasswordAsync)
            .AddEndpointFilter<ValidationFilter<ResetPasswordRequest>>()
            .RequireRateLimiting("reset-password");

        group.MapPost("/email-verification", VerifyEmailAsync)
            .AddEndpointFilter<ValidationFilter<VerifyEmailRequest>>()
            .RequireRateLimiting("email-verification");

        group.MapGet("/profile", GetProfileAsync)
            .RequireAuthorization(IdentityServerConstants.LocalApi.PolicyName)
            .RequireRateLimiting("profile");

        group.MapPut("/profile", UpdateProfileAsync)
            .AddEndpointFilter<ValidationFilter<UpdateProfileRequest>>()
            .RequireAuthorization(IdentityServerConstants.LocalApi.PolicyName)
            .RequireRateLimiting("profile");

        return app;
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request, IAccountService accountService)
    {
        var result = await accountService.RegisterAsync(request.Email, request.Password, request.DisplayName);
        if (!result.Succeeded)
        {
            var statusCode = result.ErrorCode == AccountErrorCode.Conflict
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;

            return Results.Problem(
                statusCode: statusCode,
                title: "Registration failed",
                detail: string.Join(" ", result.Errors));
        }

        var response = new RegisterResponse(result.Value!.UserId, result.Value.Email);
        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ResendVerificationEmailAsync(
        ResendVerificationEmailRequest request, IAccountService accountService)
    {
        await accountService.ResendVerificationEmailAsync(request.Email);
        return Results.Ok(new MessageResponse("If that email exists, a new verification email has been sent."));
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request, IAccountService accountService)
    {
        await accountService.ForgotPasswordAsync(request.Email, request.ReturnUrl);
        return Results.Ok(new MessageResponse("If that email exists, a password reset link has been sent."));
    }

    private static async Task<IResult> ValidateResetCodeAsync(string code, IAccountService accountService)
    {
        var result = await accountService.ValidateResetCodeAsync(code);
        if (!result.Succeeded)
        {
            return MapCodeError(result.ErrorCode, result.Errors);
        }

        return Results.Ok(new ValidateResetCodeResponse(result.Value!));
    }

    private static async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request, IAccountService accountService)
    {
        var result = await accountService.ResetPasswordAsync(request.UserId, request.Code, request.Password);
        if (!result.Succeeded)
        {
            if (result.ErrorCode == AccountErrorCode.NotFound)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "User not found",
                    detail: string.Join(" ", result.Errors));
            }

            if (result.ErrorCode == AccountErrorCode.ValidationFailed)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Password reset failed",
                    detail: string.Join(" ", result.Errors));
            }

            return MapCodeError(result.ErrorCode, result.Errors);
        }

        return Results.Ok(new MessageResponse("Your password has been successfully reset."));
    }

    private static async Task<IResult> VerifyEmailAsync(VerifyEmailRequest request, IAccountService accountService)
    {
        var result = await accountService.VerifyEmailAsync(request.Code);
        if (!result.Succeeded)
        {
            if (result.ErrorCode == AccountErrorCode.NotFound)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "User not found",
                    detail: string.Join(" ", result.Errors));
            }

            return MapCodeError(result.ErrorCode, result.Errors);
        }

        return Results.Ok(new MessageResponse("Your email has been successfully verified!"));
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal principal, HttpResponse response, IAccountService accountService)
    {
        var userId = principal.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var result = await accountService.GetProfileAsync(userId);
        if (!result.Succeeded)
        {
            return MapProfileError(result.ErrorCode, result.Errors);
        }

        var profile = result.Value!;
        response.Headers.ETag = $"\"{profile.Version}\"";
        return Results.Ok(new ProfileResponse(
            profile.UserName, profile.Email, profile.Name, profile.LastName, profile.ProfilePicture));
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        HttpRequest httpRequest,
        HttpResponse httpResponse,
        IAccountService accountService)
    {
        var userId = principal.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        if (!TryParseETagVersion(httpRequest.Headers.IfMatch.ToString(), out var expectedVersion))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: "Missing precondition",
                detail: "Send the profile's current version in the If-Match header, e.g. If-Match: \"3\".");
        }

        var result = await accountService.UpdateProfileAsync(
            userId, expectedVersion, request.Name, request.LastName, request.ProfilePicture);

        if (!result.Succeeded)
        {
            return MapProfileError(result.ErrorCode, result.Errors);
        }

        var profile = result.Value!;
        httpResponse.Headers.ETag = $"\"{profile.Version}\"";
        return Results.Ok(new ProfileResponse(
            profile.UserName, profile.Email, profile.Name, profile.LastName, profile.ProfilePicture));
    }

    private static bool TryParseETagVersion(string? etag, out int version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(etag))
        {
            return false;
        }

        var trimmed = etag.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '"' || trimmed[^1] != '"')
        {
            return false;
        }

        return int.TryParse(trimmed[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }

    private static IResult MapProfileError(AccountErrorCode errorCode, IReadOnlyList<string> errors)
    {
        return errorCode switch
        {
            AccountErrorCode.NotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found",
                detail: string.Join(" ", errors)),
            AccountErrorCode.ConcurrencyConflict => Results.Problem(
                statusCode: StatusCodes.Status412PreconditionFailed,
                title: "Profile has changed",
                detail: string.Join(" ", errors)),
            _ => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Profile update failed",
                detail: string.Join(" ", errors))
        };
    }

    private static IResult MapCodeError(AccountErrorCode errorCode, IReadOnlyList<string> errors)
    {
        var statusCode = errorCode is AccountErrorCode.ExpiredCode or AccountErrorCode.AlreadyActivated
            ? StatusCodes.Status410Gone
            : StatusCodes.Status400BadRequest;

        return Results.Problem(
            statusCode: statusCode,
            title: "Invalid verification code",
            detail: string.Join(" ", errors));
    }
}
