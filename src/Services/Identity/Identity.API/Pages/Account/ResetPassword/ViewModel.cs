namespace Identity.API.Pages.Account.ResetPassword;

public class ViewModel
{
    public string? Message { set; get;}
    public string? Code { set; get;}
    
    public bool InvalidCode { set; get; } = false;
}