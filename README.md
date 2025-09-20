# AIPilot.POC (MSFS 2024)
**WinForms proof-of-concept** for an AI Pilot add-on that connects to **Microsoft Flight Simulator 2024** via **SimConnect**.  
The app subscribes to core simvars, sends basic control events (throttle, brakes, parking brake, gear, rudder/toe brakes), and runs a minimal controller loop (**Taxi → Takeoff → Climb → Cruise**).  
This is the foundation for **Level 1 (C172, LROP→LRTR)**.

---

## Design docs & QA

-  Functional Requirements: [`/docs/FRD.md`](./docs/FRD.md)  
-  Acceptance Checklists: [Level 1](./docs/checklists/level1.md) • [Level 2](./docs/checklists/level2.md) • [Level 3](./docs/checklists/level3.md)  
- ️ Roadmap: [`/docs/ROADMAP.md`](./docs/ROADMAP.md)

---

## Repository layout

```

/AIPilot.POC
AIPilot.POC.csproj
Program.cs
MainForm.cs
SimConnectManager.cs
StateMachine.cs
AltitudeController.cs
Controls.cs
Logger.cs
Config.cs
appsettings.json
app.manifest
/Properties
AssemblyInfo.cs
/docs
FRD.md
/checklists
level1.md
level2.md
level3.md
ROADMAP.md
/.gitignore
/README.md

```

---

## Current capabilities

- **SimConnect** client with guards (single-init, safe disconnect, COM exception handling).
- **Telemetry** (live UI + logs): Title, Lat/Lon, Altitude (ft), IAS (kt), Magnetic Heading (deg), On Ground, **Ground Speed (kt)**, **Parking Brake**, **Engine Combustion (1)**, **Engine RPM (1)**.
- **Controls** (rate-limited ≤ ~10 Hz per channel; throttle is “send-on-change”):
  - Throttle **axis** (0..16383)
  - **Foot brakes** tap & **press-and-hold**
  - **Toe brakes** (left/right axes)
  - **Parking brake** (Toggle + Set ON/OFF)
  - **Gear toggle**
  - **Rudder** axis pulses (Left / Center / Right)
- **State Machine** (5 Hz): Taxi (hold-short, taxi speed cap, auto-release park brake), Takeoff, Climb, Cruise.
- **Config** via `appsettings.json`.
- **Structured logs** to `./logs/ai_pilot_poc_YYYYMMDD_HHMMSS.log`.
- **Auto-Reconnect** (UI timer): attempts connect every 5 s if the sim isn’t ready.

---

## Prerequisites

- **MSFS 2024** installed and **loaded into an active flight** (not just the main menu).
- **MSFS 2024 SDK** installed (for SimConnect).
- **Visual Studio 2022**, **.NET Framework 4.8** (Windows).
- **VC++ 2015–2022 Redistributable (x64)**.

---

## Build & setup

1. **Managed SimConnect** reference (already in the `.csproj`):
```

<MSFS SDK>\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll

````
2. **Native SimConnect.dll** next to the EXE (copied automatically if it exists at the path):
```xml
<PropertyGroup>
  <SimConnectNativePath>C:\MSFS 2024 SDK\SimConnect SDK\lib\SimConnect.dll</SimConnectNativePath>
</PropertyGroup>
<Target Name="CopySimConnectNative" AfterTargets="Build"
        Condition="Exists('$(SimConnectNativePath)')">
  <Copy SourceFiles="$(SimConnectNativePath)"
        DestinationFolder="$(OutputPath)"
        SkipUnchangedFiles="true" />
</Target>
````

3. **C# settings** (already set):

   ```xml
   <LangVersion>latest</LangVersion>
   <!-- Nullable may be enabled; WinForms fields are annotated where needed. -->
   <!-- <Nullable>enable</Nullable> -->
   ```
4. **System.Web.Extensions** reference
   Needed for `JavaScriptSerializer` in `Config.cs`. If missing:
   *Project → Add Reference → Assemblies → Framework →* **System.Web.Extensions**.
5. Build configuration: **Debug | x64** recommended. `appsettings.json` is set to **Copy Always**.

---

## Configuration (`appsettings.json`)

```json
{
  "SimConnect": { "UpdateHz": 5 },
  "Logging":   { "Level": "INFO" },
  "Level1":    { "TargetAltitudeFeet": 4500, "TaxiSpeedKtsMax": 15 }
}
```

* `UpdateHz`: baseline SimConnect request rate (the SM loop itself runs at 5 Hz).
* `TargetAltitudeFeet`: simple POC cruise/climb target.
* `TaxiSpeedKtsMax`: cap used by the Taxi phase (trickle throttle + brake taps if exceeded).

---

## Running the POC

1. In **MSFS 2024**, load a **C172** at a stand or on a runway (engines running OK).
2. Launch the app.
3. Click **Connect** (or wait for auto-reconnect). You should see telemetry update.
4. Try the **UI buttons**:

   * **Throttle 50%** → sends `AXIS_THROTTLE_SET ≈ 8191`.
   * **Brakes** → one tap of the foot brakes.
   * **Brakes (HOLD)** → press and hold the button to hold brakes; release to let go.
   * **Toe 100% / Toe 0%** → sets **both** toe brake axes to full / idle (use inside turns).
   * **Parking Brake** → **Toggle** or **ON/OFF** (Set).
   * **Rudder ◄ / ◌ / ►** → brief pulses on the rudder axis (helps taxi steering).
   * **Gear Toggle**.
5. Click **Start Loop** to run Taxi → Takeoff → Climb → Cruise.
   Use **Hold Short** to stop at runway threshold; **Stop Loop** to halt the controller.

Logs are written to `./logs/ai_pilot_poc_YYYYMMDD_HHMMSS.log`.

---

## Telemetry & controls (SimConnect)

**Controls sent**

* `AXIS_THROTTLE_SET` (0..16383)
* `BRAKES` (foot brakes, momentary)
* `AXIS_LEFT_BRAKE_SET` / `AXIS_RIGHT_BRAKE_SET` (toe brakes, 0..16383)
* `PARKING_BRAKES_TOGGLE`
* `PARKING_BRAKES` (set 0/1)
* `GEAR_TOGGLE`
* `AXIS_RUDDER_SET` (−16383..+16383 pulses)

**SimVars read**

* `TITLE`
* `PLANE LATITUDE` / `PLANE LONGITUDE` (degrees)
* `PLANE ALTITUDE` (feet)
* `AIRSPEED INDICATED` (knots)
* `PLANE HEADING DEGREES MAGNETIC` (degrees)
* `SIM ON GROUND` (Bool)
* `GROUND VELOCITY` (**feet per second**; converted to knots in code)
* `PARKING BRAKE POSITION` (Bool)
* `GENERAL ENG COMBUSTION:1` (Bool)
* `GENERAL ENG RPM:1` (rpm)

> **Important:** The order of `AddToDataDefinition(...)` **must** match the struct field order.
> Wrong simvar names or units (e.g., using knots for `GROUND VELOCITY`) will break the subscription.

---

## Operational notes (taxi & braking)

* **Hold Short**: throttle 0, periodic brake taps; parking brake held while checked.
* **Taxi speed cap**: if GS > `TaxiSpeedKtsMax + 2`, throttle 0 + brake tap; otherwise \~5% trickle.
* **Steering**: C172 uses **rudder** for nosewheel steering at taxi speeds. Use short **Rudder** pulses and, for tight turns, a brief **Toe 100%** on the inside wheel. Crosswinds or high power may require more left rudder.
* **Altitude control**: simple step logic (`AltitudeController`)—will be replaced by AP/VS/LNAV later.
* **De-spam**: events capped to ≈10 Hz per channel; throttle only sent on change (min delta \~0.5%).

---

## Troubleshooting

### SimConnect exceptions you may see

| Code | Meaning (SimConnect)      | Typical cause here                                        | What to check/fix                                                      |
| ---: | ------------------------- | --------------------------------------------------------- | ---------------------------------------------------------------------- |
|    7 | `NAME_UNRECOGNIZED`       | Bad simvar name/unit in `AddToDataDefinition`             | Verify names/units; keep `GROUND VELOCITY` in **feet per second**.     |
|    3 | `UNRECOGNIZED_ID`         | Using a definition/event/group that wasn’t registered     | Ensure the enum was registered/mapped and added to a group before use. |
|    1 | `ERROR` (generic failure) | Transmit before mapping or outside a valid group/priority | Confirm mapping, group membership, and priority.                       |

If you see **code 7** right after connect and no data arrives, one of the simvar lines is invalid—fix and relaunch.

### Parking brake ignored

* Some aircraft/assists can override `PARKING_BRAKES_TOGGLE`. Use **`PARKING_BRAKES` set 0/1** (the UI has ON/OFF).
* Check the `PARKING BRAKE POSITION` simvar to confirm the actual state.

### “Plane won’t move”

* Ensure `PARKING BRAKE POSITION` is **false** and the engine is running (`GENERAL ENG COMBUSTION:1 = true`, RPM > 0).
* Throttle very low on taxi (C172 \~1000–1200 RPM).

### COMException on shutdown

* Benign if it happens during dispose; the app guards calls and disconnects cleanly.

---

## Manual acceptance (POC)

1. Spawn **C172** (LROP).
2. Connect: telemetry in ≤ \~1 s; no code 7.
3. Controls: throttle/gear/brakes/parking-brake log single commands.
4. Start Loop: Taxi capped; Hold-Short stops; transitions to Takeoff → Climb → **Cruise \~4500 ft**.
5. Run ≥ 10 minutes; stop & disconnect cleanly (no unhandled exceptions).

For detailed criteria, see [`/docs/checklists/level1.md`](./docs/checklists/level1.md).

---

## Roadmap to Level 1

* Replace throttle-only altitude logic with AP/VS/ALT/HDG, then LNAV/VNAV.
* Taxi pathfinding (airport graph + A\*), hold-short ATC logic, runway incursion guards.
* Modular split (`AIP.Core`, `AIP.Sim`, `AIP.UI`), tests, richer observability.

---

## Compliance & License

* Uses **public MSFS SDK** only; **no Microsoft assets** are included.
* **MIT** license.
