// UPDATED 2025-09-20 — adds RudderPercent/Center and TillerPercent
using System;
using System.Collections.Concurrent;

namespace AIPilot.POC
{
    public static class ControlsHelper
    {
        private static readonly ConcurrentDictionary<string, DateTime> _lastSent = new();
        private static readonly ConcurrentDictionary<string, double>   _lastValue = new();

        private static bool Allow(string channel, int minMs = 100)
        {
            var now = DateTime.UtcNow;
            var last = _lastSent.GetOrAdd(channel, DateTime.MinValue);
            if ((now - last).TotalMilliseconds < minMs) return false;
            _lastSent[channel] = now;
            return true;
        }

        private static bool Changed(string channel, double value, double minDelta = 0.5)
        {
            var last = _lastValue.GetOrAdd(channel, double.NaN);
            if (double.IsNaN(last) || Math.Abs(value - last) >= minDelta)
            {
                _lastValue[channel] = value;
                return true;
            }
            return false;
        }

        // Throttle (0..100%) -> 0..16383
        public static void SetThrottlePercent(SimConnectManager sim, int percent)
        {
            if (sim == null || !sim.IsConnected) return;
            if (!Allow("THROTTLE")) return;

            percent = Math.Max(0, Math.Min(100, percent));
            if (!Changed("THROTTLE", percent)) return;

            int axis = (int)(16383.0 * (percent / 100.0));
            sim.SetThrottleAxis(axis);
            Logger.Info($"CMD THROTTLE {percent}% (axis={axis})");
        }

        public static void GearToggle(SimConnectManager sim)
        {
            if (sim == null || !sim.IsConnected) return;
            if (!Allow("GEAR", 150)) return;
            sim.GearToggle();
            Logger.Info("CMD GEAR_TOGGLE");
        }

        public static void Brakes(SimConnectManager sim)
        {
            if (sim == null || !sim.IsConnected) return;
            if (!Allow("BRAKES", 150)) return;
            sim.ApplyBrakes();
            Logger.Info("CMD BRAKES");
        }

        public static void BrakesHoldTick(SimConnectManager sim)
        {
            if (sim == null || !sim.IsConnected) return;
            sim.ApplyBrakes();
        }

        // Toe brakes via axes (0..100% -> 0..16383)
        public static void SetToeBrakesPercent(SimConnectManager sim, int leftPercent, int rightPercent)
        {
            if (sim == null || !sim.IsConnected) return;
            leftPercent  = Math.Max(0, Math.Min(100, leftPercent));
            rightPercent = Math.Max(0, Math.Min(100, rightPercent));

            if (!Allow("TOE_BRAKES", 50) && !Changed("TOE_L", leftPercent) && !Changed("TOE_R", rightPercent)) return;

            int l = (int)(16383.0 * (leftPercent / 100.0));
            int r = (int)(16383.0 * (rightPercent / 100.0));
            sim.SetToeBrakesAxis(l, r);
            Logger.Info($"CMD TOE_BRAKES L={leftPercent}% R={rightPercent}% (axes {l}/{r})");
        }

        // Rudder percent −100..+100 -> −16383..+16383
        public static void RudderPercent(SimConnectManager sim, int percent)
        {
            if (sim == null || !sim.IsConnected) return;
            percent = Math.Max(-100, Math.Min(100, percent));
            if (!Allow("RUDDER", 50) && !Changed("RUDDER", percent, 1.0)) return;

            int axis = (int)(16383.0 * (percent / 100.0));
            sim.SetRudderAxis(axis);
            Logger.Info($"CMD RUDDER {percent}% (axis={axis})");
        }

        public static void RudderCenter(SimConnectManager sim) => RudderPercent(sim, 0);

        // Optional tiller (nosewheel steering) percent −100..+100
        public static void TillerPercent(SimConnectManager sim, int percent)
        {
            if (sim == null || !sim.IsConnected) return;
            percent = Math.Max(-100, Math.Min(100, percent));
            if (!Allow("TILLER", 50) && !Changed("TILLER", percent, 1.0)) return;

            int axis = (int)(16383.0 * (percent / 100.0));
            sim.SetTillerAxis(axis);
            Logger.Info($"CMD TILLER {percent}% (axis={axis})");
        }

        public static void ParkingBrakeToggle(SimConnectManager sim)
        {
            if (sim == null || !sim.IsConnected) return;
            if (!Allow("PARKBRAKE", 250)) return;
            sim.ParkingBrakeToggle();
            Logger.Info("CMD PARKING_BRAKES_TOGGLE");
        }

        public static void ParkingBrakeSet(SimConnectManager sim, bool engage)
        {
            if (sim == null || !sim.IsConnected) return;
            if (!Allow("PARKBRAKE_SET", 250)) return;
            sim.ParkingBrakeSet(engage);
            Logger.Info($"CMD PARKING_BRAKES {(engage ? "SET" : "RELEASE")}");
        }
    }
}
