namespace Solvix.Server.Application
{
    public static class Constants
    {
        // Message Status (mirroring client constants)
        public static class MessageStatus
        {
            public const int Unknown = -1;
            public const int Sending = 0;    // در حال ارسال
            public const int Sent = 1;       // ارسال شده به سرور
            public const int Delivered = 2;  // دریافت شده توسط گیرنده
            public const int Read = 3;       // خوانده شده توسط گیرنده
            public const int Failed = 4;     // خطا در ارسال
        }

        // سایر ثابت‌های مورد نیاز سرور را می‌توانید اینجا اضافه کنید
    }
}
