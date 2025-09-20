// UPDATED 2025-09-20 — adds rudder/tiller axis commands; toe brakes already present
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.FlightSimulator.SimConnect;

namespace AIPilot.POC
{
    public class SimConnectManager
    {
        public event EventHandler<string>? LogMessage;
        public event EventHandler? SimDataUpdated;

        public string StatusText { get; private set; } = "Disconnected";
        public DataSnapshot? LastDataSnapshot { get; private set; }
        public bool IsConnected => _connected;

        public SimConnectManager(Form messageWindow)
        {
            _messageWindow = messageWindow ?? throw new ArgumentNullException(nameof(messageWindow));
            _messageWindow.HandleDestroyed += (s, e) => { if (_sim != null) Disconnect(); };
        }

        public void Connect()
        {
            if (_connected) return;
            try
            {
                _sim = new SimConnect("AIPilot.POC", _messageWindow.Handle, WM_USER_SIMCONNECT, null, 0);
                Status("Connected (initializing)");

                _sim.OnRecvOpen -= OnRecvOpen;
                _sim.OnRecvOpen += OnRecvOpen;
                _sim.OnRecvQuit += (s, e) => { Log("Simulator quit"); Disconnect(); };
                _sim.OnRecvException += (s, e) =>
                {
                    Log($"SimConnect exception: {e.dwException} (sendId={e.dwSendID}, index={e.dwIndex})");
                };
                _sim.OnRecvAssignedObjectId += (s, e) => Log($"Assigned ObjectID: {e.dwObjectID}");
                _sim.OnRecvSimobjectData += OnRecvSimobjectData;

                SetupSubscriptions();
                EnsureMessageFilter();

                _connected = true;
                Status("Connected");
            }
            catch (COMException ex)
            {
                Status("SimConnect connection failed: " + ex.Message);
                Log("Ensure MSFS is running and you are inside an active flight.");
                _sim = null!;
                _connected = false;
            }
        }

        public void Disconnect()
        {
            RemoveMessageFilter();

            if (!_connected && _sim == null) { Status("Disconnected"); return; }

            try
            {
                _sim?.Dispose();
                Log("SimConnect disposed");
            }
            catch (COMException ex)
            {
                Log($"Dispose COMException (ignored on shutdown): {ex.Message}");
            }
            finally
            {
                _sim = null!;
                _connected = false;
                Status("Disconnected");
            }
        }

        // ---------- Commands ----------

        public void SetThrottleAxis(int value0to16383)
        {
            if (!_connected) return;
            uint v = (uint)Math.Max(0, Math.Min(16383, value0to16383));
            SafeSim(() => _sim.TransmitClientEvent(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                EVENTS.THROTTLE_AXIS_SET, v,
                GROUPS.GENERIC,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY), "Transmit THROTTLE_AXIS_SET");
        }

        public void GearToggle()
        {
            if (!_connected) return;
            SafeSim(() => _sim.TransmitClientEvent(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                EVENTS.GEAR_TOGGLE, 0,
                GROUPS.GENERIC,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY), "Transmit GEAR_TOGGLE");
        }

        // Momentary brakes
        public void ApplyBrakes()
        {
            if (!_connected) return;
            SafeSim(() => _sim.TransmitClientEvent(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                EVENTS.BRAKES, 0,
                GROUPS.GENERIC,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY), "Transmit BRAKES");
        }

        public void ParkingBrakeToggle()
        {
            if (!_connected) return;
            SafeSim(() => _sim.TransmitClientEvent(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                EVENTS.PARKING_BRAKES, 0,
                GROUPS.GENERIC,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY), "Transmit PARKING_BRAKES");
        }

        public void ParkingBrakeSet(bool engage)
        {
            if (!_connected) return;
            uint v = (uint)(engage ? 1 : 0);
            SafeSim(() => _sim.TransmitClientEvent(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                EVENTS.PARKING_BRAKE_SET, v,
                GROUPS.GENERIC,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY), "Transmit PARKING_BRAKE_SET");
        }

        // Toe brakes (axes) 0..16383
        public void SetToeBrakesAxis(int left0to16383, int right0to16383)
        {
            if (!_connected) return;
            uint l = (uint)Math.Max(0, Math.Min(16383, left0to16383));
            uint r = (uint)Math.Max(0, Math.Min(16383, right0to16383));

            SafeSim(() => _sim.TransmitClientEvent(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                EVENTS.AXIS_LEFT_BRAKE_SET, l,
                GROUPS.GENERIC,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY), "Transmit AXIS_LEFT_BRAKE_SET");

            SafeSim(() => _sim.TransmitClientEvent(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                EVENTS.AXIS_RIGHT_BRAKE_SET, r,
                GROUPS.GENERIC,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY), "Transmit AXIS_RIGHT_BRAKE_SET");
        }

        // Rudder axis −16383..+16383
        public void SetRudderAxis(int valueMinus16383to16383)
        {
            if (!_connected) return;
            int clamped = Math.Max(-16383, Math.Min(16383, valueMinus16383to16383));
            uint v = unchecked((uint)clamped);
            SafeSim(() => _sim.TransmitClientEvent(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                EVENTS.AXIS_RUDDER_SET, v,
                GROUPS.GENERIC,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY), "Transmit AXIS_RUDDER_SET");
        }

        // Nosewheel tiller (optional; many GA ignore it, airliners use it)
        public void SetTillerAxis(int valueMinus16383to16383)
        {
            if (!_connected) return;
            int clamped = Math.Max(-16383, Math.Min(16383, valueMinus16383to16383));
            uint v = unchecked((uint)clamped);
            SafeSim(() => _sim.TransmitClientEvent(
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                EVENTS.AXIS_STEERING_SET, v,
                GROUPS.GENERIC,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY), "Transmit AXIS_STEERING_SET");
        }

        // ---------- Internals ----------

        private const int WM_USER_SIMCONNECT = 0x0402;
        private SimConnect _sim = null!;
        private bool _connected;
        private readonly Form _messageWindow;
        private SimConnectMessageFilter? _filter;

        private void OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN e)
        {
            Log($"SimConnect open: {e.szApplicationName} v{e.dwApplicationVersionMajor}.{e.dwApplicationVersionMinor}");
        }

        private void SetupSubscriptions()
        {
            // Telemetry (kept minimal/stable)
            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "TITLE", null,
                SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "PLANE LATITUDE", "Degrees",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "PLANE LONGITUDE", "Degrees",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "PLANE ALTITUDE", "Feet",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "AIRSPEED INDICATED", "Knots",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "PLANE HEADING DEGREES MAGNETIC", "Degrees",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "SIM ON GROUND", "Bool",
                SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "GROUND VELOCITY", "Feet per second",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "BRAKE PARKING INDICATOR", "Bool",
                SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "BRAKE LEFT POSITION", "Percent",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "BRAKE RIGHT POSITION", "Percent",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "GENERAL ENG COMBUSTION:1", "Bool",
                SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _sim.AddToDataDefinition(DEFINITIONS.BasicTelemetry, "GENERAL ENG RPM:1", "Revolutions per minute",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            _sim.RegisterDataDefineStruct<DataStructBasic>(DEFINITIONS.BasicTelemetry);

            SafeSim(() => _sim.RequestDataOnSimObject(
                DATA_REQUESTS.Request1,
                DEFINITIONS.BasicTelemetry,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SECOND,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0, 0, 0), "RequestDataOnSimObject");

            // Events
            _sim.MapClientEventToSimEvent(EVENTS.THROTTLE_AXIS_SET, "AXIS_THROTTLE_SET");
            _sim.MapClientEventToSimEvent(EVENTS.GEAR_TOGGLE, "GEAR_TOGGLE");
            _sim.MapClientEventToSimEvent(EVENTS.BRAKES, "BRAKES");
            _sim.MapClientEventToSimEvent(EVENTS.PARKING_BRAKES, "PARKING_BRAKES");
            _sim.MapClientEventToSimEvent(EVENTS.PARKING_BRAKE_SET, "PARKING_BRAKE_SET");
            _sim.MapClientEventToSimEvent(EVENTS.AXIS_LEFT_BRAKE_SET, "AXIS_LEFT_BRAKE_SET");
            _sim.MapClientEventToSimEvent(EVENTS.AXIS_RIGHT_BRAKE_SET, "AXIS_RIGHT_BRAKE_SET");
            _sim.MapClientEventToSimEvent(EVENTS.AXIS_RUDDER_SET, "AXIS_RUDDER_SET");
            _sim.MapClientEventToSimEvent(EVENTS.AXIS_STEERING_SET, "AXIS_STEERING_SET");

            _sim.AddClientEventToNotificationGroup(GROUPS.GENERIC, EVENTS.THROTTLE_AXIS_SET, false);
            _sim.AddClientEventToNotificationGroup(GROUPS.GENERIC, EVENTS.GEAR_TOGGLE, false);
            _sim.AddClientEventToNotificationGroup(GROUPS.GENERIC, EVENTS.BRAKES, false);
            _sim.AddClientEventToNotificationGroup(GROUPS.GENERIC, EVENTS.PARKING_BRAKES, false);
            _sim.AddClientEventToNotificationGroup(GROUPS.GENERIC, EVENTS.PARKING_BRAKE_SET, false);
            _sim.AddClientEventToNotificationGroup(GROUPS.GENERIC, EVENTS.AXIS_LEFT_BRAKE_SET, false);
            _sim.AddClientEventToNotificationGroup(GROUPS.GENERIC, EVENTS.AXIS_RIGHT_BRAKE_SET, false);
            _sim.AddClientEventToNotificationGroup(GROUPS.GENERIC, EVENTS.AXIS_RUDDER_SET, false);
            _sim.AddClientEventToNotificationGroup(GROUPS.GENERIC, EVENTS.AXIS_STEERING_SET, false);
            _sim.SetNotificationGroupPriority(GROUPS.GENERIC, SimConnect.SIMCONNECT_GROUP_PRIORITY_HIGHEST);
        }

        private void OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            if ((DEFINITIONS)data.dwDefineID != DEFINITIONS.BasicTelemetry) return;

            var d = (DataStructBasic)data.dwData[0];

            const double FPS_PER_KT = 1.6878098571011957;
            double gsKts = d.GndSpdFps / FPS_PER_KT;

            LastDataSnapshot = new DataSnapshot
            {
                Title = d.Title,
                Latitude = d.Lat,
                Longitude = d.Lon,
                AltitudeFeet = d.AltFeet,
                IndicatedAirspeedKts = d.IASKts,
                HeadingMagdeg = d.HeadingDeg,
                OnGround = d.OnGround != 0,
                GroundSpeedKts = gsKts,
                ParkingBrakeOn = d.ParkBrakeIndicator != 0,
                BrakeLeftPct = d.BrakeLeftPct,
                BrakeRightPct = d.BrakeRightPct,
                EngineCombustion1 = d.EngCombustion1 != 0,
                EngineRpm1 = d.EngRpm1
            };

            SimDataUpdated?.Invoke(this, EventArgs.Empty);
        }

        private bool SafeSim(Action action, string what)
        {
            if (!_connected || _sim == null) return false;
            try { action(); return true; }
            catch (COMException ex)
            {
                Log($"{what} -> COMException {ex.HResult:X8}. Disconnecting.");
                Disconnect();
                return false;
            }
        }

        private void EnsureMessageFilter()
        {
            if (_filter == null)
            {
                _filter = new SimConnectMessageFilter(WM_USER_SIMCONNECT, _sim, this);
                Application.AddMessageFilter(_filter);
            }
            else
            {
                _filter.UpdateSim(_sim);
            }
        }

        private void RemoveMessageFilter()
        {
            if (_filter != null)
            {
                Application.RemoveMessageFilter(_filter);
                _filter = null;
            }
        }

        private void Status(string s) { StatusText = s; Log(s); Logger.Info(s); }
        private void Log(string msg)  { LogMessage?.Invoke(this, msg); Logger.Info(msg); }

        private enum DEFINITIONS { BasicTelemetry }
        private enum DATA_REQUESTS { Request1 }
        private enum EVENTS
        {
            THROTTLE_AXIS_SET, GEAR_TOGGLE, BRAKES,
            PARKING_BRAKES, PARKING_BRAKE_SET,
            AXIS_LEFT_BRAKE_SET, AXIS_RIGHT_BRAKE_SET,
            AXIS_RUDDER_SET, AXIS_STEERING_SET
        }
        private enum GROUPS { GENERIC }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        private struct DataStructBasic
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Title;
            public double Lat;
            public double Lon;
            public double AltFeet;
            public double IASKts;
            public double HeadingDeg;
            public int    OnGround;
            public double GndSpdFps;          // feet per second
            public int    ParkBrakeIndicator; // Bool
            public double BrakeLeftPct;       // 0..100
            public double BrakeRightPct;      // 0..100
            public int    EngCombustion1;     // Bool
            public double EngRpm1;            // rpm
        }

        public class DataSnapshot
        {
            public string Title { get; set; } = string.Empty;
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double AltitudeFeet { get; set; }
            public double IndicatedAirspeedKts { get; set; }
            public double HeadingMagdeg { get; set; }
            public bool   OnGround { get; set; }
            public double GroundSpeedKts { get; set; }
            public bool   ParkingBrakeOn { get; set; }
            public double BrakeLeftPct { get; set; }
            public double BrakeRightPct { get; set; }
            public bool   EngineCombustion1 { get; set; }
            public double EngineRpm1 { get; set; }
        }

        private sealed class SimConnectMessageFilter : IMessageFilter
        {
            private readonly int _msgId;
            private SimConnect _sim;
            private readonly SimConnectManager _owner;

            public SimConnectMessageFilter(int msgId, SimConnect sim, SimConnectManager owner)
            {
                _msgId = msgId;
                _sim = sim;
                _owner = owner;
            }

            public void UpdateSim(SimConnect sim) => _sim = sim;

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg == _msgId && _sim != null)
                {
                    try { _sim.ReceiveMessage(); }
                    catch (COMException)
                    {
                        _owner.Log("ReceiveMessage COMException. Disconnecting.");
                        _owner.Disconnect();
                    }
                    return true;
                }
                return false;
            }
        }
    }
}
