using UnityEngine;

internal static class Program
{
    private const float HalfWidth = 20f, Height = 48f, JumpSpeed = 10f, Gravity = 0.45f, RunSpeed = 4f;
    private static int failed;

    public static int Main()
    {
        Run("one-way platform directly overhead is reachable", AscendOneWay);
        Run("actual lobby pickup platforms are reachable both ways", ActualLobbyRoutes);
        Run("NPC reaches a shop platform above it", SimulateShopAscent);
        Run("NPC drops to the shop floor below it", SimulateShopDescent);
        Run("NPC detours and lands on a solid overhead platform", SimulateSolidAscent);
        Run("NPC walks off a solid platform to a lower goal", SimulateSolidDescent);
        Run("one-way platform drops toward a lower landing", DropOneWay);
        Run("solid overhead platform requires an outside takeoff", SolidUndersideDetour);
        Run("solid platform descends by walking past its edge", SolidWalkOff);
        Run("high destination uses intermediate platforms", MultiStepClimb);
        Run("a fall without a landing is rejected", NoVoidDrop);
        Run("disconnected destination is rejected", UnreachableDestination);
        Run("replacing stage geometry invalidates route cache", GeometryInvalidation);
        Run("changing stages invalidates route cache", StageInvalidation);
        Run("jump press is held through ascent and released at apex", JumpHold);
        Run("drop input continues until feet clear the platform", DropHold);
        Run("drop first stops a running bot to avoid a slide", StopBeforeDrop);
        Run("NPC reaches every lobby/shop disk from anywhere in its room", LobbyDiskApproach);
        Console.WriteLine(failed == 0 ? "All navigation regressions passed." : $"{failed} regression(s) failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine($"PASS {name}"); }
        catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error.Message}"); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static StageDataSO Stage((float left, float right, float top, float depth)[] solids,
        (float left, float right, float top, float depth)[] platforms)
    {
        return new StageDataSO
        {
            solidCenter = solids.Select(s => new Vector2((s.left + s.right) / 2f, s.top - s.depth / 2f)).ToArray(),
            solidExtent = solids.Select(s => new Vector2((s.right - s.left) / 2f, s.depth / 2f)).ToArray(),
            platformCenter = platforms.Select(s => new Vector2((s.left + s.right) / 2f, s.top - s.depth / 2f)).ToArray(),
            platformExtent = platforms.Select(s => new Vector2((s.right - s.left) / 2f, s.depth / 2f)).ToArray(),
            borderMin = new Vector3(-1000f, -300f, 0f), borderMax = new Vector3(1000f, 1000f, 0f)
        };
    }

    private static bool Route(BotPlatformNavigator navigator, StageDataSO stage, Vector2 start, Vector2 goal,
        out BotPlatformNavigator.Step step, int jumps = 2) =>
        navigator.TryGetNextStep(stage, start, goal, HalfWidth, Height, JumpSpeed, Gravity, RunSpeed, jumps, out step);

    private static BotPlatformNavigator.Step RequiredRoute(StageDataSO stage, Vector2 start, Vector2 goal, int jumps = 2)
    {
        Check(Route(new BotPlatformNavigator(), stage, start, goal, out var step, jumps), $"No route from {start} to {goal}.");
        return step;
    }

    private static void AscendOneWay()
    {
        var stage = Stage(new[] { (-400f, 400f, 0f, 20f) }, new[] { (-80f, 80f, 100f, 8f) });
        var step = RequiredRoute(stage, new Vector2(0, 0), new Vector2(0, 100));
        Check(step.kind == BotPlatformNavigator.Kind.Jump, $"Expected jump, got {step.kind}.");
        Check(step.destinationOneWay && MathF.Abs(step.landing.y - 100f) < 2f, "Must land on the overhead one-way platform.");
    }

    private static void DropOneWay()
    {
        var stage = Stage(new[] { (-400f, 400f, 0f, 20f) }, new[] { (-80f, 80f, 100f, 8f) });
        var step = RequiredRoute(stage, new Vector2(0, 100), new Vector2(0, 0));
        Check(step.kind == BotPlatformNavigator.Kind.Drop, $"Expected drop, got {step.kind}.");
        Check(MathF.Abs(step.landing.y) < 2f, "Drop must have the ground as its landing.");
    }

    private static void SolidUndersideDetour()
    {
        var stage = Stage(new[] { (-400f, 400f, 0f, 20f), (-80f, 80f, 100f, 20f) }, Array.Empty<(float,float,float,float)>());
        var step = RequiredRoute(stage, new Vector2(0, 0), new Vector2(0, 100));
        Check(step.kind == BotPlatformNavigator.Kind.Jump, $"Expected planned jump, got {step.kind}.");
        Check(MathF.Abs(step.takeoff.x) >= 80f + HalfWidth, $"Takeoff {step.takeoff} is still under the solid ceiling.");
        Check(!step.destinationOneWay, "The solid ceiling must not become a one-way surface.");
    }

    private static void SolidWalkOff()
    {
        var stage = Stage(new[] { (-400f, 400f, 0f, 20f), (-80f, 80f, 100f, 20f) }, Array.Empty<(float,float,float,float)>());
        var step = RequiredRoute(stage, new Vector2(0, 100), new Vector2(0, 0));
        Check(step.kind == BotPlatformNavigator.Kind.Fall, $"Expected walk-off fall, got {step.kind}.");
        Check(MathF.Abs(step.takeoff.x) > 80f + HalfWidth, $"Walk-off point {step.takeoff} does not clear the source platform.");
        Check(MathF.Abs(step.landing.y) < 2f, "Walk-off must lead to the lower floor.");
    }

    private static void MultiStepClimb()
    {
        var stage = Stage(new[] { (-400f, 400f, 0f, 20f) },
            new[] { (-160f, 0f, 90f, 8f), (-20f, 140f, 180f, 8f), (-100f, 60f, 270f, 8f) });
        var navigator = new BotPlatformNavigator();
        var position = new Vector2(0, 0);
        var goal = new Vector2(0, 270);
        for (int i = 0; i < 3; i++)
        {
            Check(Route(navigator, stage, position, goal, out var step, 1), $"No route from intermediate {position}.");
            Check(step.kind == BotPlatformNavigator.Kind.Jump, $"Climb step {i} was {step.kind}.");
            Check(step.landing.y > position.y && step.landing.y - position.y <= 112f, "Each jump must reach a higher, physically reachable surface.");
            position = step.landing;
        }
        Check(MathF.Abs(position.y - goal.y) < 2f, $"Climb stopped at {position}.");
    }

    private static void NoVoidDrop()
    {
        var stage = Stage(Array.Empty<(float,float,float,float)>(), new[] { (-80f, 80f, 100f, 8f) });
        bool found = Route(new BotPlatformNavigator(), stage, new Vector2(0, 100), new Vector2(0, -200), out var step);
        Check(!found || step.kind == BotPlatformNavigator.Kind.Walk, $"Unsafe {step.kind} route was generated without a lower surface.");
    }

    private static void UnreachableDestination()
    {
        var stage = Stage(new[] { (-100f, 100f, 0f, 20f) }, new[] { (700f, 850f, 500f, 8f) });
        Check(!Route(new BotPlatformNavigator(), stage, new Vector2(0, 0), new Vector2(775, 500), out _), "Disconnected high platform unexpectedly has a route.");
    }

    private static void GeometryInvalidation()
    {
        var stage = Stage(new[] { (-400f, 400f, 0f, 20f) }, new[] { (-80f, 80f, 100f, 8f) });
        var navigator = new BotPlatformNavigator();
        Check(Route(navigator, stage, new Vector2(0, 0), new Vector2(0, 100), out _), "Initial route missing.");
        stage.platformCenter = new[] { new Vector2(0, 176) };
        stage.platformExtent = new[] { new Vector2(80, 4) };
        Check(Route(navigator, stage, new Vector2(0, 0), new Vector2(0, 180), out var updated), "Route missing after geometry replacement.");
        Check(MathF.Abs(updated.landing.y - 180f) < 2f, $"Cached old landing survived geometry replacement: {updated.landing}.");
        stage.platformCenter[0] = new Vector2(0, 96);
        Check(Route(navigator, stage, new Vector2(0, 0), new Vector2(0, 100), out updated), "Route missing after in-place geometry change.");
        Check(MathF.Abs(updated.landing.y - 100f) < 2f, "In-place geometry update was missed.");
    }

    private static void StageInvalidation()
    {
        var navigator = new BotPlatformNavigator();
        var first = Stage(new[] { (-400f, 400f, 0f, 20f) }, new[] { (-80f, 80f, 100f, 8f) });
        var next = Stage(new[] { (-400f, 400f, 0f, 20f) }, new[] { (-80f, 80f, 180f, 8f) });
        Check(Route(navigator, first, new Vector2(0, 0), new Vector2(0, 100), out _), "Initial stage route missing.");
        Check(Route(navigator, next, new Vector2(0, 0), new Vector2(0, 180), out var step), "New stage route missing.");
        Check(MathF.Abs(step.landing.y - 180f) < 2f, "Route from the previous stage was reused.");
    }

    private static ProbeNpc Probe(float feet = 0, bool platform = false)
    {
        GameManager.Instance = null;
        return new ProbeNpc { owner = new PlayerController { position = new FixedPosition(0, feet), isGrounded = true, onPlatform = platform } };
    }

    private static void JumpHold()
    {
        var npc = Probe();
        npc.Action = npc.Jump;
        npc.Tick();
        Check(npc.JumpButton == ButtonState.Pressed, "Jump should start with Pressed.");
        npc.Action = npc.Stand;
        npc.owner.isGrounded = false;
        npc.owner.vSpd = 10;
        npc.Tick();
        Check(npc.JumpButton == ButtonState.Held, "The first rising tick released the jump, shortening every hop.");
        npc.owner.vSpd = 1;
        npc.Tick();
        Check(npc.JumpButton == ButtonState.Held, "Jump must remain held while rising.");
        npc.owner.vSpd = 0;
        npc.Tick();
        Check(npc.JumpButton == ButtonState.Released, "Jump should release at the apex.");
    }

    private static void DropHold()
    {
        var npc = Probe(100, platform: true);
        npc.Action = () => npc.Drop(0);
        npc.Tick();
        Check(npc.npcInputSnapshot.Direction == 2 && npc.JumpButton == ButtonState.Pressed, "Drop must start with down+jump.");
        npc.owner.isGrounded = false;
        npc.owner.onPlatform = false;
        npc.owner.vSpd = 0;
        npc.Tick();
        Check(npc.npcInputSnapshot.Direction == 2 && npc.JumpButton == ButtonState.Held,
            "Drop released while feet were still at the platform surface; collision would catch the bot again.");
        npc.owner.position = new FixedPosition(0, 70);
        npc.owner.vSpd = -4;
        npc.Tick();
        Check(npc.JumpButton != ButtonState.Held && npc.JumpButton != ButtonState.Pressed, "Drop should finish once feet clear the source platform.");
    }

    private static void StopBeforeDrop()
    {
        var npc = Probe(100, platform: true);
        npc.owner.state = PlayerState.Run;
        npc.Action = () => npc.Drop(0);
        npc.Tick();
        Check(npc.JumpButton != ButtonState.Pressed && npc.JumpButton != ButtonState.Held,
            "Down+jump while Run triggers slide before platform drop.");
        Check(npc.npcInputSnapshot.Direction == 5, "Bot should stop before requesting a platform drop.");
        npc.owner.state = PlayerState.Idle;
        npc.Tick();
        Check(npc.npcInputSnapshot.Direction == 2 && npc.JumpButton == ButtonState.Pressed, "Idle bot should begin dropping.");
    }

    private static void SimulateShopAscent()
    {
        var stage = Stage(new[] { (-400f, 400f, 16f, 20f) },
            new[] { (-224f, -64f, 96f, 8f), (64f, 224f, 96f, 8f) });
        Simulate(stage, new Vector2(144, 16), new Vector2(144, 96), false, 24, 3);
    }

    private static void SimulateShopDescent()
    {
        var stage = Stage(new[] { (-400f, 400f, 16f, 20f) },
            new[] { (-224f, -64f, 96f, 8f), (64f, 224f, 96f, 8f) });
        Simulate(stage, new Vector2(144, 96), new Vector2(144, 16), true, 24, 3);
    }

    private static void SimulateSolidAscent()
    {
        var stage = Stage(new[] { (-400f, 400f, 0f, 20f), (-80f, 80f, 100f, 20f) }, Array.Empty<(float,float,float,float)>());
        Simulate(stage, new Vector2(0, 0), new Vector2(0, 100), false);
    }

    private static void SimulateSolidDescent()
    {
        var stage = Stage(new[] { (-400f, 400f, 0f, 20f), (-80f, 80f, 100f, 20f) }, Array.Empty<(float,float,float,float)>());
        Simulate(stage, new Vector2(0, 100), new Vector2(0, 0), false);
    }

    private static void Simulate(StageDataSO stage, Vector2 start, Vector2 goal, bool platform, float width = 40, float speed = 4)
    {
        var npc = Probe(start.y, platform);
        npc.owner.position = new FixedPosition(start.x, start.y);
        npc.owner.playerWidth = width;
        npc.owner.runSpeed = speed;
        GameManager.Instance = new GameManager { stage = stage };
        npc.Action = () => npc.Travel(goal);
        var simulation = new MovementSimulation(npc.owner, stage);
        var history = new Queue<string>();
        for (int tick = 0; tick < 900; tick++)
        {
            npc.Tick();
            simulation.Step(npc.npcInputSnapshot);
            float x = npc.owner.position.X.ToFloat(), y = npc.owner.position.Y.ToFloat();
            if (tick % 30 == 0)
            {
                history.Enqueue($"t{tick}: ({x:0.0},{y:0.0}) h{npc.owner.hSpd.ToFloat():0.0} v{npc.owner.vSpd.ToFloat():0.0} grounded{npc.owner.isGrounded} dir{npc.npcInputSnapshot.Direction}");
                if (history.Count > 8) history.Dequeue();
            }
            Check(y > -250, "Navigation fell into a void: " + string.Join("; ", history));
            if (npc.owner.isGrounded && MathF.Abs(x - goal.x) <= 12f && MathF.Abs(y - goal.y) < 2f) return;
        }
        throw new InvalidOperationException("Did not arrive within 900 simulation ticks: " + string.Join("; ", history));
    }
    private static StageDataSO LobbyStage()
    {
        string yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Lobby_Arena StageDataSO.asset"));
        Vector2[] ReadVectors(string name)
        {
            string section = System.Text.RegularExpressions.Regex.Match(yaml,
                @"(?m)^  " + name + @":\r?\n((?:  - \{[^\r\n]+\r?\n)*)").Groups[1].Value;
            return System.Text.RegularExpressions.Regex.Matches(section, @"x: ([^,]+), y: ([^,}]+)")
                .Select(m => new Vector2(float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        }
        var stage = new StageDataSO { solidCenter = ReadVectors("solidCenter"), solidExtent = ReadVectors("solidExtent"),
            platformCenter = ReadVectors("platformCenter"), platformExtent = ReadVectors("platformExtent"),
            borderMin = new Vector3(-350, -205, 0), borderMax = new Vector3(350, 205, 0) };
        Check(stage.solidCenter.Length == 14 && stage.platformCenter.Length == 4, "Active lobby geometry fixture failed to load.");
        return stage;
    }

    // The Shop reuses the lobby map. These are P4's three disk spots (GambaMachine.diskLocations 9-11)
    // with real character stats. From rest the bot's smallest move is ~5px, so the old 1px jump
    // takeoff window left it shuffling underneath its disk from about one start in nine.
    private static void LobbyDiskApproach()
    {
        var stage = LobbyStage();
        foreach (float diskX in new[] { 79f, 143f, 207f })
        {
            for (float x = 80f; x <= 240f; x += 5f)
            {
                try { Simulate(stage, new Vector2(x, -192f), new Vector2(diskX, -112f), false, 24, 3); }
                catch (InvalidOperationException error)
                {
                    throw new InvalidOperationException($"From ({x}, -192) to disk x={diskX}: {error.Message}");
                }
            }
        }
    }

    private static void ActualLobbyRoutes()
    {
        var stage = LobbyStage();
        var navigator = new BotPlatformNavigator();
        var missingRoutes = new List<string>();
        // Each lobby room's floor/pickup platform pair is a real bot destination.
        foreach (var route in new[] {
            (new Vector2(-145, -192), new Vector2(-144, -112)),
            (new Vector2(-144, -112), new Vector2(-145, -192)),
            (new Vector2(145, -192), new Vector2(144, -112)),
            (new Vector2(144, -112), new Vector2(145, -192)),
            (new Vector2(-145, 16), new Vector2(-144, 96)),
            (new Vector2(-144, 96), new Vector2(-145, 16)),
            (new Vector2(145, 17.313232f), new Vector2(144, 96)),
            (new Vector2(144, 96), new Vector2(145, 17.313232f)) })
        {
            if (!navigator.TryGetNextStep(stage, route.Item1, route.Item2, 12f, Height, JumpSpeed, Gravity, 3f, 2, out var step))
                missingRoutes.Add($"{route.Item1} to {route.Item2}");
        }
        Check(missingRoutes.Count == 0, "Actual lobby geometry has no route: " + string.Join("; ", missingRoutes));
    }
    private sealed class ProbeNpc : NpcAI
    {
        public Action Action;
        public override string BehaviorName => "Regression Probe";
        public override void NPCUpdate() => Action?.Invoke();
        public ButtonState JumpButton => npcInputSnapshot.ButtonStates[1];
        public void Jump() => TapJump();
        public void Stand() => Neutral();
        public void Travel(Vector2 goal) => TravelToward(goal);
        public void Drop(float targetY) { if (!TryDropToward(targetY)) Neutral(); }
    }
}





