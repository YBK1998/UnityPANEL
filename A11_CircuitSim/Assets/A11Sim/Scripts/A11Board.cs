using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace A11Sim
{
    using TA = TextAlignmentOptions;

    /// RichAuto A11E interface board + handheld logic. Inputs are low-level active (input to GND = active),
    /// outputs Y1–Y6 are sink outputs (output on = terminal connected to GND).
    public class A11Board : Device
    {
        public enum Mode { Idle, Jog, Homing, Job, ToolSet }

        public Machine m;
        public readonly StepperDrive[] drives = new StepperDrive[3];
        public Vfd vfd;

        public bool powered;
        public readonly bool[] X = new bool[9];
        public readonly bool[] Y = new bool[9];
        public bool spindleOn;
        public int grade = 4;
        public Mode mode;
        public string message = "";
        public string Alarm { get; private set; }
        public bool homed;
        public readonly int[] jog = new int[3];
        public readonly float[] vel = new float[3];  // commanded
        public readonly float[] dro = new float[3];  // A11 position (open loop)

        static readonly float[] JogSpeed = { 80, 80, 30 };
        static readonly float[] HomeSpeed = { 60, 60, 25 };
        static readonly int[] HomeOrder = { 2, 0, 1 };
        static readonly int[] HomeDir = { -1, -1, +1 };
        static readonly int[] HomeInput = { 1, 2, 3 };
        static readonly string[] Ax = { "X", "Y", "Z" };

        Terminal tDC24, tDCGND, tGND, tOGND, t24V;
        public bool sensorSupply;
        public bool jogLow;
        readonly Terminal[] tX = new Terminal[9];
        readonly Terminal[] tY = new Terminal[9];
        readonly Terminal[] t5V = new Terminal[3], tPUL = new Terminal[3], tDIR = new Terminal[3];

        int homeStage, homePhase;
        float phaseTime;
        int jobIndex;
        int toolPhase;
        readonly List<Vector3> job = new();

        public A11Board()
        {
            tag = "A1";
            title = "A1 — RichAuto A11E interface board (+ handheld, 50-pin cable)";
            info = "Inputs X1–X8: low-level active. X5 driver alarm, X6 hard limit, X7 E-stop (fixed functions).\nOutputs Y1–Y6: sink. Y1 spindle FWD, Y2–Y4 speed bits S1–S3, Y5 alarm lamp, Y6 work lamp.\nAxis blocks: common anode 5 V, 8 mA max. No 0–10 V output on this board.";
            BuildJob();
        }

        void BuildJob()
        {
            // A small demo program: square pocket outline, then a circle.
            job.Add(new Vector3(200, 150, 0));
            job.Add(new Vector3(200, 150, -6));
            job.Add(new Vector3(600, 150, -6));
            job.Add(new Vector3(600, 450, -6));
            job.Add(new Vector3(200, 450, -6));
            job.Add(new Vector3(200, 150, -6));
            job.Add(new Vector3(200, 150, 0));
            job.Add(new Vector3(400, 300, 0));
            for (int i = 0; i <= 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2;
                job.Add(new Vector3(400 + 120 * Mathf.Cos(a), 300 + 120 * Mathf.Sin(a), -4));
            }
            job.Add(new Vector3(520, 300, 0));
        }

        public override void Build()
        {
            Term("Shield", 200, 140);
            tDC24 = Term("DC24V", 200, 136);
            t24V = Term("24V", 200, 132);
            tX[7] = Term("X7", 200, 126);
            tX[1] = Term("X1", 200, 118);
            tX[2] = Term("X2", 200, 98);
            tX[3] = Term("X3", 200, 78);
            tX[6] = Term("X6", 200, 62);
            tX[4] = Term("X4", 200, 54);
            tX[8] = Term("X8", 200, 46);
            tGND = Term("GND", 200, 38);
            tDCGND = Term("DCGND", 200, 34);
            for (int i = 1; i <= 4; i++) tY[i] = Term("Y" + i, 200 + 4 * i, 142);
            tOGND = Term("OGND", 222, 142);
            tY[5] = Term("Y5", 210, 30);
            tY[6] = Term("Y6", 216, 30);
            float[] ax = { 134, 98, 62 };
            for (int i = 0; i < 3; i++)
            {
                t5V[i] = Term(Ax[i] + "_5V", 226, ax[i]);
                tPUL[i] = Term(Ax[i] + "_PUL", 226, ax[i] - 4);
                tDIR[i] = Term(Ax[i] + "_DIR", 226, ax[i] - 12);
            }
            tX[5] = Term("X5", 226, 36);
            C.Fixed(tGND, tDCGND);
            C.Fixed(tGND, tOGND);
            for (int i = 0; i < 3; i++) dro[i] = 0;
        }

        public void SyncDro()
        {
            for (int i = 0; i < 3; i++) dro[i] = m.pos[i];
        }

        public override bool Evaluate()
        {
            bool p = C.Has(tDC24, Pot.P24) && C.Has(tDCGND, Pot.Z0);
            // The input optocouplers are supplied from J8 24V (wire 168). Without it no input can be active,
            // so X7 (NC) reads open and the board shows E-STOP — fail-safe.
            sensorSupply = p && C.Has(t24V, Pot.P24);
            for (int n = 1; n <= 8; n++) X[n] = sensorSupply && C.Same(tX[n], tGND);
            bool ch = p != powered;
            powered = p;
            return ch;
        }

        public override void Links()
        {
            if (!powered) return;
            for (int n = 1; n <= 6; n++)
                if (Y[n]) C.Link(tY[n], tOGND);
        }

        public override void Sources()
        {
            if (!powered) return;
            for (int i = 0; i < 3; i++)
            {
                C.Source(t5V[i], Pot.P5);
                if (vel[i] != 0) C.Source(tPUL[i], Pot.ACT);
                if (vel[i] < 0) C.Source(tDIR[i], Pot.ACT);
            }
        }

        // ---- handheld commands ----
        public void ToggleSpindle()
        {
            if (!powered) return;
            if (!spindleOn && Alarm != null)
            {
                message = "Cannot start spindle: " + Alarm;
                return;
            }
            spindleOn = !spindleOn;
            message = spindleOn ? "Spindle started" : "Spindle stopped";
        }

        public void SetGrade(int g) => grade = Mathf.Clamp(g, 1, 8);

        public void HomeAll()
        {
            if (!powered || Alarm == "E-STOP" || Alarm == "DRIVER ALARM") return;
            mode = Mode.Homing;
            homeStage = 0;
            homePhase = 0;
            phaseTime = 0;
            message = "Homing Z, X, Y…";
        }

        public void RunJob()
        {
            if (!powered) return;
            if (Alarm != null)
            {
                message = "Cannot start: " + Alarm;
                return;
            }
            mode = Mode.Job;
            jobIndex = 0;
            spindleOn = true;
            message = $"Running demo job (S{grade})";
        }

        public void ToolSet()
        {
            if (!powered || Alarm != null) return;
            mode = Mode.ToolSet;
            toolPhase = 0;
            message = "Tool setting: Z down to the plate…";
        }

        public void Stop()
        {
            if (mode != Mode.Idle) message = mode + " stopped";
            mode = Mode.Idle;
            spindleOn = false;
        }

        public override void Tick(float dt)
        {
            for (int i = 0; i < 3; i++) vel[i] = 0;
            if (!powered)
            {
                mode = Mode.Idle;
                spindleOn = false;
                homed = false;
                Alarm = null;
                message = "";
                for (int n = 1; n <= 8; n++) Y[n] = false;
                MovePhysical(dt);
                return;
            }

            bool estop = !X[7];
            bool drv = X[5];
            bool hard = X[6];
            Alarm = estop ? "E-STOP" : drv ? "DRIVER ALARM" : hard ? "HARD LIMIT" : null;
            if (Alarm != null)
            {
                if (mode is Mode.Job or Mode.Homing or Mode.ToolSet)
                {
                    message = $"{mode} stopped: {Alarm}";
                    mode = Mode.Idle;
                }
                spindleOn = false;
            }

            switch (mode)
            {
                case Mode.Idle:
                case Mode.Jog:
                    bool any = false;
                    for (int i = 0; i < 3; i++)
                    {
                        if (jog[i] == 0 || estop || drv) continue;
                        vel[i] = jog[i] * JogSpeed[i] * (jogLow ? 0.2f : 1f);
                        any = true;
                    }
                    mode = any ? Mode.Jog : Mode.Idle;
                    break;
                case Mode.Homing:
                    HomingStep(dt);
                    break;
                case Mode.Job:
                    JobStep(dt);
                    break;
                case Mode.ToolSet:
                    ToolStep(dt);
                    break;
            }

            for (int i = 0; i < 3; i++) dro[i] += vel[i] * dt;
            MovePhysical(dt);

            int b = grade - 1;
            Y[1] = spindleOn;
            Y[2] = (b & 1) != 0;
            Y[3] = (b & 2) != 0;
            Y[4] = (b & 4) != 0;
            Y[5] = Alarm != null;
            Y[6] = mode == Mode.Job;
        }

        void MovePhysical(float dt)
        {
            for (int i = 0; i < 3; i++)
            {
                bool can = drives[i] != null && drives[i].CanMove;
                m.Move(i, can ? vel[i] : 0, dt);
            }
        }

        void HomingStep(float dt)
        {
            if (homeStage >= 3)
            {
                mode = Mode.Idle;
                homed = true;
                message = "Homed: X−, Y−, Z+ references set";
                return;
            }
            int ax = HomeOrder[homeStage];
            bool sensor = X[HomeInput[ax]];
            phaseTime += dt;
            if (homePhase == 0)
            {
                if (sensor)
                {
                    dro[ax] = 0;
                    homePhase = 1;
                    phaseTime = 0;
                    return;
                }
                vel[ax] = HomeDir[ax] * HomeSpeed[ax];
                float travel = m.max[ax] - m.min[ax];
                if (phaseTime > travel / HomeSpeed[ax] + 2f)
                {
                    mode = Mode.Idle;
                    message = $"HOME FAILED: {Ax[ax]} home sensor not seen";
                }
            }
            else
            {
                vel[ax] = -HomeDir[ax] * HomeSpeed[ax] * 0.25f;
                if (!sensor && phaseTime > 0.25f)
                {
                    homeStage++;
                    homePhase = 0;
                    phaseTime = 0;
                }
                else if (phaseTime > 3f)
                {
                    mode = Mode.Idle;
                    message = $"HOME FAILED: {Ax[ax]} home sensor stays active";
                }
            }
        }

        void JobStep(float dt)
        {
            if (jobIndex >= job.Count) jobIndex = 1;
            var target = job[jobIndex];
            var cur = new Vector3(dro[0], dro[1], dro[2]);
            var d = target - cur;
            float feed = Mathf.Abs(d.z) > 0.01f && new Vector2(d.x, d.y).magnitude < 0.01f ? 20f : 160f;
            float step = feed * dt;
            if (d.magnitude <= step)
            {
                for (int i = 0; i < 3; i++) vel[i] = d[i] / Mathf.Max(dt, 1e-4f);
                jobIndex++;
                return;
            }
            var v = d.normalized * feed;
            for (int i = 0; i < 3; i++) vel[i] = v[i];
        }

        void ToolStep(float dt)
        {
            if (toolPhase == 0)
            {
                if (X[4])
                {
                    toolPhase = 1;
                    message = $"Tool length set at Z = {dro[2]:0.00}";
                    return;
                }
                vel[2] = -15f;
                if (dro[2] < -125f)
                {
                    mode = Mode.Idle;
                    message = "TOOL SET FAILED: no touch on X4";
                }
            }
            else
            {
                vel[2] = 30f;
                if (dro[2] >= -2f) mode = Mode.Idle;
            }
        }

        public string StatusLine()
        {
            if (!powered) return "NO POWER (J10)";
            if (Alarm != null) return "ALARM: " + Alarm;
            return mode switch
            {
                Mode.Jog => "JOG",
                Mode.Homing => "HOMING",
                Mode.Job => "RUNNING JOB",
                Mode.ToolSet => "TOOL SET",
                _ => homed ? "READY" : "READY (not homed)",
            };
        }

        // ---- drawing ----
        static readonly string[] LeftNames = { "Shield n.c.", "J10 +24V", "J8 24V", "X7 E-stop", "X1 (XH)", "X2 (YH)", "X3 (ZH)", "X6 limit", "X4 (TS)", "X8 n.c.", "J8 GND", "J10 GND" };
        static readonly float[] LeftY = { 140, 136, 132, 126, 118, 98, 78, 62, 54, 46, 38, 34 };

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.color = Pal.Box;
            g.Rect(new Vector2(200, 30), new Vector2(226, 142));
            g.color = Pal.Sym;
            for (int i = 0; i < LeftNames.Length; i++)
                lb.Add(LeftNames[i], 201.0f, LeftY[i], 0.62f, Pal.TextDim, TA.Left);
            for (int i = 1; i <= 4; i++) lb.Add($"Y{i}", 200 + 4 * i, 140.6f, 0.6f, Pal.TextDim);
            lb.Add("(S0) (S1) (S2) (S3)", 210, 139.2f, 0.45f, Pal.TextDim);
            lb.Add("GND", 222, 140.6f, 0.6f, Pal.TextDim);
            lb.Add("Y5", 210, 31.4f, 0.6f, Pal.TextDim);
            lb.Add("Y6", 216, 31.4f, 0.6f, Pal.TextDim);
            float[] ax = { 134, 98, 62 };
            for (int i = 0; i < 3; i++)
            {
                lb.Add($"{Ax[i]}_AXIS 5V", 225.0f, ax[i], 0.6f, Pal.TextDim, TA.Right);
                lb.Add("PULSE", 225.0f, ax[i] - 4, 0.6f, Pal.TextDim, TA.Right);
                lb.Add("DIR", 225.0f, ax[i] - 12, 0.6f, Pal.TextDim, TA.Right);
            }
            lb.Add("X5 drv alarm", 225.0f, 36, 0.6f, Pal.TextDim, TA.Right);
            lb.Add("A1", 213, 112, 2.0f, Pal.Title, TA.Center, 0, true);
            lb.Add("RichAuto A11E", 213, 108.6f, 1.05f, Pal.Title, TA.Center, 0, true);
            lb.Add("interface board", 213, 106.8f, 0.8f, Pal.TextDim);
            lb.Dyn(StatusLine, 213, 103.2f, 0.8f, Pal.Sig, TA.Center, true);
            lb.Dyn(() => powered ? $"X {dro[0]:0.0}" : "", 213, 101.2f, 0.75f, Pal.Text);
            lb.Dyn(() => powered ? $"Y {dro[1]:0.0}" : "", 213, 99.8f, 0.75f, Pal.Text);
            lb.Dyn(() => powered ? $"Z {dro[2]:0.0}" : "", 213, 98.4f, 0.75f, Pal.Text);
            lb.Dyn(() => powered ? (spindleOn ? $"SPINDLE ON · S{grade}" : $"spindle off · S{grade}") : "", 213, 96.6f, 0.7f, Pal.Text);
            lb.Add("low-level active", 213, 70.4f, 0.6f, Pal.TextDim);
            lb.Add("input to GND = active", 213, 69.2f, 0.6f, Pal.TextDim);
            lb.Add("sink outputs", 213, 44.4f, 0.6f, Pal.TextDim);
            lb.Add("50-pin → handheld", 213, 43.2f, 0.6f, Pal.TextDim);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            // Input LEDs next to their terminals.
            float[] iy = { 0, 118, 98, 78, 54, 36, 62, 126, 46 };
            for (int n = 1; n <= 8; n++)
            {
                var p = n == 5 ? new Vector2(219.0f, 36) : new Vector2(207.6f, iy[n]);
                Led(g, glow, p, X[n], n == 7 ? Pal.Sig : Pal.Sig);
            }
            for (int n = 1; n <= 4; n++) Led(g, glow, new Vector2(200 + 4 * n, 137.6f), powered && Y[n], Pal.LampYellow);
            Led(g, glow, new Vector2(210, 33.6f), powered && Y[5], Pal.LampRed);
            Led(g, glow, new Vector2(216, 33.6f), powered && Y[6], Pal.LampGreen);
            var pw = new Vector2(224.2f, 140.4f);
            Led(g, glow, pw, powered, Pal.LampGreen);
        }

        static void Led(Gfx g, Gfx glow, Vector2 p, bool on, Color c)
        {
            if (on)
            {
                glow.Disc(p, 1.3f, c);
                g.Disc(p, 0.42f, c);
            }
            else g.Circle(p, 0.42f, 10);
        }

        public override IEnumerable<Hotspot> Hotspots() { yield return Spot(Rect.MinMaxRect(200, 30, 226, 142)); }
        public override string Status() => StatusLine();
    }
}
