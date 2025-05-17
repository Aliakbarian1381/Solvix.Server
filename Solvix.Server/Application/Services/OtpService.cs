using Microsoft.Extensions.Caching.Memory;
using Solvix.Server.Core.Interfaces;
using System.Text;
using System.Text.Json;

namespace Solvix.Server.Application.Services
{
    public class OtpService : IOtpService
    {
        private readonly ILogger<OtpService> _logger;
        private readonly IMemoryCache _cache;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        // پیشوند کلید کش برای کدهای OTP
        private const string OTP_CACHE_KEY_PREFIX = "OTP_";
        // زمان انقضای کد OTP (5 دقیقه)
        private readonly TimeSpan _otpExpiration = TimeSpan.FromMinutes(5);

        public OtpService(ILogger<OtpService> logger, IMemoryCache cache, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _logger = logger;
            _cache = cache;
            _httpClient = httpClientFactory.CreateClient("OtpClient");
            _apiKey = configuration["SmsService:ApiKey"];
        }

        public async Task<string> GenerateOtpAsync(string phoneNumber)
        {
            // تولید کد 6 رقمی تصادفی
            var otpCode = Random.Shared.Next(100000, 999999).ToString();

            // ذخیره کد در کش با زمان انقضا
            var cacheKey = $"{OTP_CACHE_KEY_PREFIX}{phoneNumber}";
            _cache.Set(cacheKey, otpCode, _otpExpiration);

            // ارسال پیامک OTP
            await SendOtpAsync(phoneNumber, otpCode);

            _logger.LogInformation("کد OTP برای شماره {PhoneNumber} تولید شد", phoneNumber);
            return otpCode;
        }

        public async Task<bool> ValidateOtpAsync(string phoneNumber, string otpCode)
        {
            var cacheKey = $"{OTP_CACHE_KEY_PREFIX}{phoneNumber}";

            if (_cache.TryGetValue(cacheKey, out string storedOtp))
            {
                // اگر کد OTP صحیح باشد، آن را از کش حذف می‌کنیم (یکبار مصرف)
                if (storedOtp == otpCode)
                {
                    _cache.Remove(cacheKey);
                    _logger.LogInformation("کد OTP برای شماره {PhoneNumber} با موفقیت تایید شد", phoneNumber);
                    return true;
                }
            }

            _logger.LogWarning("کد OTP نامعتبر برای شماره {PhoneNumber}", phoneNumber);
            return false;
        }

        public async Task<bool> SendOtpAsync(string phoneNumber, string otpCode)
        {
            try
            {
                var data = new
                {
                    OtpId = "1310",
                    ReplaceToken = new[] { otpCode },
                    MobileNumber = phoneNumber,
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("ApiKey", _apiKey);

                var jsonContent = JsonSerializer.Serialize(data);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.limosms.com/api/sendpatternmessage", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("کد OTP به شماره {PhoneNumber} ارسال شد. پاسخ: {Response}", phoneNumber, result);
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("خطا در ارسال کد OTP به شماره {PhoneNumber}. وضعیت: {Status}, خطا: {Error}",
                        phoneNumber, response.StatusCode, error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ارسال کد OTP به شماره {PhoneNumber}", phoneNumber);
                return false;
            }
        }
    }
}
