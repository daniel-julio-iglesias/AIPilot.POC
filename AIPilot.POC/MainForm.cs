// UPDATED 2025-09-20 — adds rudder buttons (left/center/right)
using System;
using System.Windows.Forms;

namespace AIPilot.POC
{
    public partial class MainForm : Form
    {
        private readonly SimConnectManager _sim;
        private readonly StateMachine _sm;

        private TextBox _txtStatus = null!;
        private TextBox _txtTelemetry = null!;
        private ListBox _lstLog = null!;
        private Timer _uiTimer = null!;
        private Timer _reconnectTimer = null!;
        private Timer _brakeHoldTimer = null!;
        private Label _lblPhase = null!;
        private CheckBox _chkHoldShort = null!;

        public MainForm()
        {
            InitializeComponent();

            _sim = new SimConnectManager(this);
            _sim.LogMessage += OnLog;
            _sim.SimDataUpdated += OnSimDataUpdated;

            _sm = new StateMachine(_sim);
            _sm.PhaseChanged += (s, phase) => _lblPhase.Text = $"Phase: {phase}";
        }

        private void InitializeComponent()
        {
            this.Text = "AI Pilot POC – SimConnect";
            this.Width = 1220;
            this.Height = 690;

            var btnConnect       = new Button { Text = "Connect",         Left = 10,   Top = 10, Width = 100 };
            var btnDisconnect    = new Button { Text = "Disconnect",      Left = 120,  Top = 10, Width = 100 };
            _chkHoldShort        = new CheckBox { Left = 1030, Top = 12, Width = 160, Text = "Hold Short (stop)" };

            var btnStart         = new Button { Text = "Start Loop",      Left = 10,   Top = 50, Width = 100 };
            var btnStop          = new Button { Text = "Stop Loop",       Left = 120,  Top = 50, Width = 100 };
            var btnThrottle50    = new Button { Text = "Throttle 50%",    Left = 230,  Top = 50, Width = 120 };
            var btnGearToggle    = new Button { Text = "Gear Toggle",     Left = 360,  Top = 50, Width = 100 };

            var btnBrakes        = new Button { Text = "Tap Brakes",      Left = 470,  Top = 50, Width = 100 };
            var btnBrakesHold    = new Button { Text = "Hold Brakes",     Left = 580,  Top = 50, Width = 110 };
            var btnToe100        = new Button { Text = "Toe 100%",        Left = 700,  Top = 50, Width = 100 };
            var btnToe0          = new Button { Text = "Toe 0%",          Left = 810,  Top = 50, Width = 100 };

            var btnParkToggle    = new Button { Text = "Parking Brake",   Left = 920,  Top = 50, Width = 120 };
            var btnParkOn        = new Button { Text = "Park ON",         Left = 1050, Top = 50, Width = 75 };
            var btnParkOff       = new Button { Text = "Park OFF",        Left = 1130, Top = 50, Width = 75 };

            var btnRudLeft       = new Button { Text = "Rudder ◄",        Left = 10,   Top = 84, Width = 100 };
            var btnRudCenter     = new Button { Text = "Rudder 0",        Left = 120,  Top = 84, Width = 100 };
            var btnRudRight      = new Button { Text = "Rudder ►",        Left = 230,  Top = 84, Width = 100 };

            _lblPhase            = new Label  { Left = 350, Top = 88, Width = 160, Text = "Phase: Idle" };
            _txtStatus    = new TextBox { Left = 520, Top = 84, Width = 670, Height = 24, ReadOnly = true };
            _txtTelemetry = new TextBox { Left = 10, Top = 120, Width = 1180, Height = 180, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            _lstLog       = new ListBox { Left = 10, Top = 310, Width = 1180, Height = 330 };

            btnConnect.Click       += (s, e) => { Logger.Info("[UI] Connect");        _sim.Connect(); };
            btnDisconnect.Click    += (s, e) => { Logger.Info("[UI] Disconnect");     _sim.Disconnect(); };
            btnStart.Click         += (s, e) => { Logger.Info("[UI] Start Loop");     _sm.Start(); };
            btnStop.Click          += (s, e) => { Logger.Info("[UI] Stop Loop");      _sm.Stop(); };
            btnThrottle50.Click    += (s, e) => { Logger.Info("[UI] Throttle 50%");   ControlsHelper.SetThrottlePercent(_sim, 50); };
            btnGearToggle.Click    += (s, e) => { Logger.Info("[UI] Gear Toggle");    ControlsHelper.GearToggle(_sim); };

            btnBrakes.Click        += (s, e) => { Logger.Info("[UI] Brakes tap");     ControlsHelper.Brakes(_sim); };
            _brakeHoldTimer = new Timer { Interval = 125 };
            _brakeHoldTimer.Tick += (s, e) => ControlsHelper.BrakesHoldTick(_sim);
            btnBrakesHold.MouseDown += (s, e) => { Logger.Info("[UI] Brakes HOLD start"); _brakeHoldTimer.Start(); };
            btnBrakesHold.MouseUp   += (s, e) => { Logger.Info("[UI] Brakes HOLD stop");  _brakeHoldTimer.Stop();  };
            btnBrakesHold.MouseLeave+= (s, e) => { _brakeHoldTimer.Stop(); };

            btnToe100.Click        += (s, e) => { Logger.Info("[UI] Toe 100%"); ControlsHelper.SetToeBrakesPercent(_sim, 100, 100); };
            btnToe0.Click          += (s, e) => { Logger.Info("[UI] Toe 0%");   ControlsHelper.SetToeBrakesPercent(_sim, 0, 0); };

            btnParkToggle.Click     += (s, e) => { Logger.Info("[UI] Parking Toggle"); ControlsHelper.ParkingBrakeToggle(_sim); };
            btnParkOn .Click        += (s, e) => { Logger.Info("[UI] Parking ON");     ControlsHelper.ParkingBrakeSet(_sim, true); };
            btnParkOff.Click        += (s, e) => { Logger.Info("[UI] Parking OFF");    ControlsHelper.ParkingBrakeSet(_sim, false); };

            // Rudder buttons: small pulses and center
            btnRudLeft.Click   += (s, e) => { Logger.Info("[UI] Rudder -25%"); ControlsHelper.RudderPercent(_sim, -25); };
            btnRudCenter.Click += (s, e) => { Logger.Info("[UI] Rudder 0");    ControlsHelper.RudderCenter(_sim); };
            btnRudRight.Click  += (s, e) => { Logger.Info("[UI] Rudder +25%"); ControlsHelper.RudderPercent(_sim, +25); };

            this.Controls.AddRange(new Control[]
            {
                btnConnect, btnDisconnect, _chkHoldShort,
                btnStart, btnStop, btnThrottle50, btnGearToggle,
                btnBrakes, btnBrakesHold, btnToe100, btnToe0,
                btnParkToggle, btnParkOn, btnParkOff,
                btnRudLeft, btnRudCenter, btnRudRight,
                _lblPhase, _txtStatus, _txtTelemetry, _lstLog
            });

            _uiTimer = new Timer { Interval = 500 };
            _uiTimer.Tick += (s, e) =>
            {
                if (_sim.LastDataSnapshot != null)
                {
                    var d = _sim.LastDataSnapshot;
                    _txtTelemetry.Text =
                        $"Title: {d.Title}\r\n" +
                        $"Lat/Lon: {d.Latitude:F6}, {d.Longitude:F6}\r\n" +
                        $"Alt MSL: {d.AltitudeFeet:F0} ft\r\n" +
                        $"Airspeed: {d.IndicatedAirspeedKts:F0} kt\r\n" +
                        $"Heading: {d.HeadingMagdeg:F0}°\r\n" +
                        $"On Ground: {d.OnGround}\r\n" +
                        $"Gnd Spd: {d.GroundSpeedKts:F1} kt\r\n" +
                        $"Park Brake: {(d.ParkingBrakeOn ? "ON" : "OFF")}\r\n" +
                        $"Brake L/R: {d.BrakeLeftPct:F0}% / {d.BrakeRightPct:F0}%\r\n" +
                        $"Combustion1: {d.EngineCombustion1}\r\n" +
                        $"RPM1: {d.EngineRpm1:F0}";
                }
                _txtStatus.Text = _sim.StatusText;
            };
            _uiTimer.Start();

            _reconnectTimer = new Timer { Interval = 5000 };
            _reconnectTimer.Tick += (s, e) =>
            {
                if (!_sim.IsConnected)
                {
                    Logger.Info("[AUTO] Attempting reconnect...");
                    _sim.Connect();
                }
            };
            _reconnectTimer.Start();
        }

        private void OnLog(object? sender, string e)
        {
            _lstLog.Items.Add($"{DateTime.Now:HH:mm:ss} {e}");
            _lstLog.TopIndex = _lstLog.Items.Count - 1;
        }

        private void OnSimDataUpdated(object? sender, EventArgs e) { }
    }
}
