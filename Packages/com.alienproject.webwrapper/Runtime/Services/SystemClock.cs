using System;

namespace AlienProject.WebWrapper
{
    public sealed class SystemClock : IClock
    {
        public long UtcNowUnixSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
