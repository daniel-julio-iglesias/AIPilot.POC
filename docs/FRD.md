# Functional Requirements Document (FRD)
**AI Pilot Add-On for MSFS 2024**  
**Version:** 1.0  
**Date:** September 2025

---

## 1. Introduction

### 1.1 Purpose
This document defines the scope, objectives, acceptance criteria, architecture, risks, and roadmap for the **AI Pilot add-on for Microsoft Flight Simulator 2024 (MSFS 2024)**.

The add-on replicates and extends the “AI Co-pilot” functionality present in MSFS 2020, aligning closer to **real-world flight operations** while maintaining accessibility for casual sim users.

Core feature: **Passenger Mode** — the user can ride as a passenger while the AI performs a complete **gate-to-gate flight** (pushback, taxi, takeoff, climb, cruise, descent, approach, landing, taxi-in, parking).

### 1.2 Scope
- **Initial platform:** MSFS 2024 (PC/Steam).
- **Later expansion:** MSFS 2020 (backwards compatibility) and X-Plane 12 (select features).
- **Supported aircraft (launch):**
  - General Aviation: Cessna 172
  - Airliner: A320neo
  - Stress test: B737-800

### 1.3 Audience
- **Stakeholder**: Daniel (approves acceptance scenarios, defines priorities).
- **Implementer**: ChatGPT (solution architect, developer, documenter).
- **Community contributors**: testers, add-on developers.

---

## 2. Objectives
- Provide a **realistic AI Pilot** capable of full gate-to-gate automation.
- Deliver a **Passenger Mode** experience.
- Mirror the **MSFS “checkride” style** (objectives + pass/fail).
- Extend realism: taxi pathfinding, SID/STAR compliance, crosswinds, reroutes, diversions.

---

## 3. Acceptance Criteria — Levels 1–3

### 3.1 Level 1 — GA Trial Flight
- **Aircraft**: Cessna 172  
- **Route**: LROP (Bucharest) → LRTR (Timișoara)

**Objectives**
1. Pushback or direct taxi from GA stand.
2. Taxi ≤ 15 kt, correct hold short.
3. Takeoff roll on runway centerline.
4. Climb to 4500 ft, maintain ±300 ft.
5. Land within runway boundaries.
6. Taxi to GA apron, engines off, brakes set.

**Pass/Fail**
- Off-pavement taxi = Fail
- Sustained >500 ft deviation = Fail
- Touchdown outside runway = Fail

---

### 3.2 Level 2 — Regional Airliner Flight
- **Aircraft**: A320neo  
- **Route**: LFPG (Paris CDG) → LSGG (Geneva)  
- **Procedures**: RWY 26R, SID STO → STAR + ILS 22

**Objectives**
1. Pushback coordination.
2. Taxi to RWY 26R, no runway incursion.
3. Takeoff and climb per SID.
4. Cruise at FL300 ±400 ft.
5. STAR descent compliance.
6. Stabilized ILS 22 approach (gear/flaps by 1000 ft AGL).
7. Taxi to gate, engines off, brakes set.

**Pass/Fail**
- Runway incursion = Fail
- SID/STAR bust >1000 ft = Fail
- Unstable approach not corrected by go-around = Fail

---

### 3.3 Level 3 — Stress Test Flight
- **Aircraft**: B737-800  
- **Route**: KJFK (New York JFK) → KBOS (Boston)  
- **Conditions**: Crosswind 20 kt, AI/real traffic active

**Objectives**
1. Taxi with congestion, respect hold instructions.
2. Crosswind takeoff, maintain centerline.
3. Comply with ATC reroute (direct-to fix, altitude change).
4. Execute go-around if unstable.
5. Land within crosswind envelope, taxi to gate.

**Pass/Fail**
- Collision with AI = Fail
- Reroute ignored >30s = Fail
- Hard landing >600 fpm = Fail

---

## 4. Comparison with MSFS 2024 Missions

| Category     | MSFS 2024 Missions/Checkrides | AI Pilot Add-On Criteria                   |
| ------------ | ----------------------------- | ------------------------------------------ |
| Taxi         | Simple markers, no collisions | Pathfinding, hold-short, incursion checks  |
| Takeoff      | Rotate in window              | Centerline, Vr/V2 speeds, crosswind logic  |
| Climb/Cruise | Altitude ± tolerance          | SID/STAR compliance, stability             |
| Approach     | Land in zone                  | Stabilized by 1000 ft AGL, go-around logic |
| Landing      | Touchdown inside runway       | Rollout, taxi-in to stand                  |
| Failures     | Rare                          | Crosswinds, reroutes, traffic              |
| Style        | Gamified lessons              | Real-world SOP checklists                  |

---

## 5. System Architecture

### 5.1 State Machine
````

+---------+      +---------+      +---------+      +---------+
\|  GATE   | ---> |  TAXI   | ---> | TAKEOFF | ---> |  CLIMB  |
+---------+      +---------+      +---------+      +---------+
^                                          |
\|                                          v
+---------+      +---------+      +---------+      +---------+
\|  PARK   | <--- | TAXI-IN | <--- | LANDING | <--- | DESCENT |
+---------+      +---------+      +---------+      +---------+

```

### 5.2 Core Modules
- SimConnect client (I/O).
- State machine engine (gate-to-gate).
- Taxi pathfinding (airport BGL + A\*).
- Procedure interpreter (SIDs, STARs, approaches).
- Flight control (LNAV, VNAV, AP modes).
- ATC adapter (internal virtual ATC + external bridges).
- EFB panel (start/stop, progress, announcements).

### 5.3 Data Flow
```

\[ Flight Plan ] → \[ Procedure Engine ] → \[ State Machine ]
\|                |
+-----+-----+    +-----+-----+
\| Taxi AI  |    | LNAV/VNAV |
+-----------+    +-----------+
|
\[ SimConnect ]
|
\[ Aircraft ]

```

---

## 6. ATC Integration Architecture
```

\[ AI Pilot Core ] → \[ ATC Layer ] → +--------------------------+
\| Internal Virtual ATC     |
\| (built-in, TTS)          |
+--------------------------+
\| External ATC Bridges     |
\| (Pilot2ATC, FSHud, BATC) |
+--------------------------+

```
- Internal ATC: deterministic rules, TTS voices.
- External ATC: adapters normalize instructions.
- Guardrails reject unsafe or contradictory instructions.
- Every ATC message logged for replay/debug.

---

## 7. Stakeholder Notes
- Stakeholder does **not** need ICAO expertise.
- Reviews scenarios in tables with pass/fail criteria.
- May request custom routes (e.g., Bucharest–Prague).
- Can influence realism (night ops, mountain, diversions).

---

## 8. Roadmap (Weeks 1–18)
- **POC (1–3):** SimConnect loop, taxi + takeoff/landing.
- **Level 1 (4–6):** GA LROP→LRTR gate-to-gate.
- **Level 2 (7–11):** A320 LFPG→LSGG with SID/STAR.
- **Level 3 (12–18):** B737 KJFK→KBOS stress test.

```

Weeks →   1   2   3   4   5   6   7   8   9  10  11  12  13  14  15  16  17  18
\---------------------------------------------------------------------
POC       ███
Level 1           ███
Level 2                   █████
Level 3                                   ███████

```

---

## 9. Risks & Mitigations
- SDK gaps (FMS) → fallback LNAV/VNAV.
- ATC menu limits → internal ATC + external bridges.
- Taxi steering → tiller events, tuned profiles.
- SimConnect load → scoped subscriptions.
- Crosswinds → go-around logic.
- Compliance → SDK-only, no copied assets.

---

## 10. Future Scope (Levels 4–6)
**Level 4 (Weeks 19–23): Mountain Airports**  
- LOWI Innsbruck, LPMA Madeira.  
- Terrain-aware VNAV, windshear.

**Level 5 (Weeks 24–28): Long-Haul Ops**  
- EGLL → KJFK.  
- Step climbs, fuel management, oceanic clearances.

**Level 6 (Weeks 29–30): Advanced Scenarios**  
- Night ops, diversions, failures (engine-out, flaps).

```

Weeks →  19  20  21  22  23  24  25  26  27  28  29  30
\------------------------------------------------
Level 4  █████
Level 5                  █████
Level 6                                    ██

```

---

## 11. Deliverables
1. FRD v1.0 (this document).
2. POC SimConnect skeleton (C#).
3. Acceptance checklists per Level.
4. EFB prototype panel (HTML/JS).
5. Roadmap tracker (timeline updates).
