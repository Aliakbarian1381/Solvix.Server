using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging; // برای لاگ کردن

public static class FirebaseAdminSetup
{
    public static void Initialize(IApplicationBuilder app) // از IApplicationBuilder استفاده می‌کنیم تا به سرویس‌ها دسترسی داشته باشیم
    {
        // جلوگیری از راه‌اندازی مجدد در محیط توسعه (موقع Hot Reload)
        if (FirebaseApp.DefaultInstance != null)
        {
            return;
        }

        try
        {
            var credentials = GoogleCredential.FromFile("fcm-service-account.json");
            FirebaseApp.Create(new AppOptions()
            {
                Credential = credentials,
            });

            // لاگ گرفتن برای اطمینان از موفقیت‌آمیز بودن
            var logger = app.ApplicationServices.GetRequiredService<ILogger<StartupBase>>(); // یا هر کلاس دیگری برای لاگ
            logger.LogInformation("Firebase Admin SDK initialized successfully.");
        }
        catch (Exception ex)
        {
            // لاگ کردن خطا در صورت بروز مشکل
            var logger = app.ApplicationServices.GetRequiredService<ILogger<StartupBase>>();
            logger.LogError(ex, "Firebase Admin SDK initialization failed.");
            throw; // بهتر است برنامه در صورت عدم موفقیت، بالا نیاید
        }
    }
}