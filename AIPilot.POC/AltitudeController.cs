using System;

namespace AIPilot.POC
{
    public sealed class AltitudeController
    {
        // Simple step controller for POC (replace with proper PID or AP later)
        // Returns throttle percent [0..100]
        public int SuggestThrottle(double altitudeErrorFeet)
        {
            if (altitudeErrorFeet > 1000) return 90;
            if (altitudeErrorFeet > 500)  return 80;
            if (altitudeErrorFeet > 200)  return 65;

            if (altitudeErrorFeet < -800) return 15;
            if (altitudeErrorFeet < -300) return 20;
            if (altitudeErrorFeet < -150) return 30;

            return 45; // near target
        }
    }
}
