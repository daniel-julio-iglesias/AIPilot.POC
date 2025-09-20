// UPDATED 2025-09-20
using System;
using System.Timers;

namespace AIPilot.POC
{
    public enum FlightPhase { Idle, Taxi, Takeoff, Climb, Cruise, Descent, Approach, Landing, TaxiIn, Park }

    public class StateMachine
    {
        private readonly SimConnectManager _sim;
        private readonly Timer _loop;
        private readonly AltitudeController _altCtl = new();

        private volatile FlightPhase _phase = FlightPhase.Idle;
        private double _targetAltFeet;

        // Taxi hysteresis memory
        private DateTime _lastStop;

        public bool HoldShort { get; set; } = false;
        public FlightPhase CurrentPhase => _phase;
        public event EventHandler<FlightPhase>? PhaseChanged;

        public StateMachine(SimConnectManager sim)
        {
            _sim = sim;
            _loop = new Timer(200) { AutoReset = true }; // 5 Hz
            _loop.Elapsed += Loop;

            _targetAltFeet = Program.Config?.Level1?.TargetAltitudeFeet ?? 4500;
            Logger.Info($"SM init: targetAlt={_targetAltFeet} ft");
        }

        public void Start()
        {
            SetPhase(FlightPhase.Taxi);
            _loop.Start();
        }

        public void Stop()
        {
            _loop.Stop();
            SetPhase(FlightPhase.Idle);
        }

        private void SetPhase(FlightPhase p)
        {
            if (_phase == p) return;
            _phase = p;
            Logger.Info($"PHASE → {p}");
            PhaseChanged?.Invoke(this, p);
        }

        private void Loop(object? sender, ElapsedEventArgs e)
        {
            if (!_sim.IsConnected || _sim.LastDataSnapshot == null) return;
            var d = _sim.LastDataSnapshot;

            switch (_phase)
            {
                case FlightPhase.Taxi:
                {
                    if (!d.OnGround) { SetPhase(FlightPhase.Takeoff); break; }

                    // If we’re not holding short, make sure parking brake is released.
                    if (!HoldShort && d.ParkingBrakeOn)
                    {
                        ControlsHelper.ParkingBrakeSet(_sim, false);
                        break; // wait next tick for release to register
                    }

                    if (HoldShort)
                    {
                        ControlsHelper.SetThrottlePercent(_sim, 0);
                        ControlsHelper.ParkingBrakeSet(_sim, true);
                        ControlsHelper.Brakes(_sim);
                        break;
                    }

                    int taxiMax = Program.Config?.Level1?.TaxiSpeedKtsMax ?? 15;
                    double gs = d.GroundSpeedKts;

                    // If essentially stopped for >2 s, keep throttle at idle
                    if (gs < 0.5)
                    {
                        if (_lastStop == default) _lastStop = DateTime.UtcNow;
                        if ((DateTime.UtcNow - _lastStop).TotalSeconds > 2)
                        {
                            ControlsHelper.SetThrottlePercent(_sim, 0);
                            break;
                        }
                    }
                    else
                    {
                        _lastStop = default;
                    }

                    if (gs > taxiMax + 2)
                    {
                        ControlsHelper.SetThrottlePercent(_sim, 0);
                        ControlsHelper.Brakes(_sim);
                    }
                    else
                    {
                        ControlsHelper.SetThrottlePercent(_sim, 5);
                    }
                    break;
                }

                case FlightPhase.Takeoff:
                    if (d.OnGround)
                    {
                        // ensure the latch is released
                        if (d.ParkingBrakeOn) ControlsHelper.ParkingBrakeSet(_sim, false);
                        ControlsHelper.SetThrottlePercent(_sim, 90);
                    }
                    else
                    {
                        SetPhase(FlightPhase.Climb);
                    }
                    break;

                case FlightPhase.Climb:
                {
                    double err = _targetAltFeet - d.AltitudeFeet;
                    int thr = _altCtl.SuggestThrottle(err);
                    ControlsHelper.SetThrottlePercent(_sim, thr);
                    if (Math.Abs(err) < 300) SetPhase(FlightPhase.Cruise);
                    break;
                }

                case FlightPhase.Cruise:
                {
                    double err = _targetAltFeet - d.AltitudeFeet;
                    int thr = _altCtl.SuggestThrottle(err);
                    ControlsHelper.SetThrottlePercent(_sim, thr);
                    break;
                }
            }
        }
    }
}
