namespace Solvix.Server.Core.Interfaces
{
    public interface IOtpService
    {
        Task<string> GenerateOtpAsync(string phoneNumber);
        Task<bool> ValidateOtpAsync(string phoneNumber, string otpCode);
        Task<bool> SendOtpAsync(string phoneNumber, string otpCode);
    }
}
