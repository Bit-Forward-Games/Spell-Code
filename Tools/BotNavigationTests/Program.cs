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
        Run("NPC held still by a screen transition doesn't jump on its first step", NoJumpAfterHeldInput);
        Run("NPC drops from anywhere on its disk platform down to its Gamba", LobbyGambaDescent);
        Run("Enhance spells are cast with room, never up close, and lose to an attack in band", EnhanceSpellChoice);
        Run("a bot backed into a ledge or wall turns to face its opponent", CorneredBotFacesTarget);
        Run("on Dual Duel a bot reaches the other floor through the loop border", DualDuelThroughTheLoop);
        Run("a bot with only unreachable opponents stands its ground instead of jump-looping", UnreachableTargetHoldsStill);
        Run("on every arena, from every spawn pair, a bot gets into the fight without jump-looping", ArenaSweep);
        Run("a long-range kit backs off to where it can cast instead of parking too close", LongRangeKitSpacing);
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
        var stage = LoadStage("Arenas/Lobby and Tutorials/Lobby_Arena StageDataSO.asset");
        Check(stage.solidCenter.Length == 14 && stage.platformCenter.Length == 4, "Active lobby geometry fixture failed to load.");
        return stage;
    }

    // Every arena asset is copied next to the binary under Arenas/ (see the csproj).
    private static StageDataSO LoadStage(string path)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        string yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, path));
        Vector2[] ReadVectors(string name)
        {
            string section = System.Text.RegularExpressions.Regex.Match(yaml,
                @"(?m)^  " + name + @":\r?\n((?:  - \{[^\r\n]+\r?\n)*)").Groups[1].Value;
            return System.Text.RegularExpressions.Regex.Matches(section, @"x: ([^,]+), y: ([^,}]+)")
                .Select(m => new Vector2(float.Parse(m.Groups[1].Value, invariant),
                    float.Parse(m.Groups[2].Value, invariant))).ToArray();
        }
        Vector3 ReadBorder(string name)
        {
            var m = System.Text.RegularExpressions.Regex.Match(yaml, @"(?m)^  " + name + @": \{x: ([^,]+), y: ([^,]+), z");
            return new Vector3(float.Parse(m.Groups[1].Value, invariant), float.Parse(m.Groups[2].Value, invariant), 0);
        }
        return new StageDataSO { solidCenter = ReadVectors("solidCenter"), solidExtent = ReadVectors("solidExtent"),
            platformCenter = ReadVectors("platformCenter"), platformExtent = ReadVectors("platformExtent"),
            playerSpawnTransform = ReadVectors("playerSpawnTransform"),
            borderMin = ReadBorder("borderMin"), borderMax = ReadBorder("borderMax"),
            borderType = (BorderType)int.Parse(System.Text.RegularExpressions.Regex.Match(yaml, @"(?m)^  borderType: (\d+)").Groups[1].Value) };
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

    // Enhance spells are tagged Short in SpellTactics, so scoring them by range band like an attack
    // zeroed every one of them: in band only inside 90, while the Enhance rule wants 115+. Medium and
    // Hard bots picked them up and never cast them.
    private static void EnhanceSpellChoice()
    {
        SpellData cashOut = new SpellData { spellName = "Cash Out", spellType = SpellType.Active, spellInput = 3 };
        SpellData asuranBlades = new SpellData { spellName = "Asuran Blades", spellType = SpellType.Active, spellInput = 3 };

        SpellData ChooseAt(float distance, params SpellData[] held)
        {
            var bot = new PlayerController { position = new FixedPosition(0, 0), isGrounded = true, pID = 1 };
            var target = new PlayerController { position = new FixedPosition(distance, 0), isGrounded = true, pID = 2 };
            bot.spellList.AddRange(held);
            GameManager.Instance = new GameManager { players = new[] { bot, target }, playerCount = 2 };
            var npc = new ProbeNpc { owner = bot };
            npc.Tick(); // perception
            return npc.Choose();
        }

        Check(ChooseAt(150f, cashOut) == cashOut, "A lone Enhance spell was never chosen, even with room to cast it.");
        Check(ChooseAt(60f, cashOut) == null, "An Enhance spell was chosen up close, where its recovery gets punished.");
        Check(ChooseAt(120f, cashOut, asuranBlades) == asuranBlades, "An Enhance spell beat an attack sitting in its band.");
    }

    // Backing off turns a bot away from its target. When a ledge or wall then cut the retreat short,
    // the turn-to-face fix-up had already run, so the bot stood cornered with its back to them --
    // and TryAttack refuses to swing unless it's facing its target.
    private static void CorneredBotFacesTarget()
    {
        var ledge = Stage(new[] { (-200f, 400f, 0f, 20f) }, Array.Empty<(float,float,float,float)>());
        var wall = Stage(new[] { (-400f, 400f, 0f, 20f), (-260f, -200f, 200f, 200f) }, Array.Empty<(float,float,float,float)>());

        foreach (var (name, stage) in new[] { ("ledge", ledge), ("wall", wall) })
        {
            var bot = new PlayerController { position = new FixedPosition(-150f, 0f), isGrounded = true, facingRight = true,
                playerWidth = 24, runSpeed = 3, pID = 1 };
            var target = new PlayerController { position = new FixedPosition(-110f, 0f), isGrounded = true, pID = 2 };
            GameManager.Instance = new GameManager { stage = stage, players = new[] { bot, target }, playerCount = 2 };
            var chase = new ChaseAI { owner = bot };
            var simulation = new MovementSimulation(bot, stage);
            float furthestBack = -150f;

            for (int tick = 0; tick < 300; tick++)
            {
                // An opponent pressing in: always closer than the bot's basic-attack window starts, so
                // it keeps wanting to back off.
                target.position = new FixedPosition(bot.position.X.ToFloat() + 16f, 0f);
                chase.Tick();
                simulation.Step(chase.npcInputSnapshot);
                furthestBack = MathF.Min(furthestBack, bot.position.X.ToFloat());
                Check(bot.position.Y.ToFloat() > -10f, $"Backed off the {name} and fell.");
            }

            Check(furthestBack < -155f, $"Never backed up to the {name}; the scenario didn't happen (furthest {furthestBack:0}).");
            Check(bot.facingRight, $"Cornered at the {name} with its back to the target (x={bot.position.X.ToFloat():0}).");
            Check(bot.position.X.ToFloat() < -150f, $"Wandered away from the {name} instead of standing its ground (x={bot.position.X.ToFloat():0}).");
        }
    }

    private static PlayerController Fighter(float x, float y, int pID) =>
        new PlayerController { position = new FixedPosition(x, y), isGrounded = true, facingRight = true,
            playerWidth = 24, runSpeed = 3, pID = pID };

    private static (string name, StageDataSO stage)[] AllArenas() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Arenas"), "*StageDataSO*.asset", SearchOption.AllDirectories)
            .Where(f => !f.Contains("Lobby and Tutorials") && !f.Contains("pfb_GameManager"))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetFileName(f).Replace(" StageDataSO.asset", "").Replace("StageDataSO.asset", "").Replace("_Arena", ""),
                LoadStage(Path.GetRelativePath(AppContext.BaseDirectory, f))))
            .ToArray();

    // One chase on a real arena: both players drop onto whatever is under their spawns, then the bot
    // goes after a player who stands still. Counts jump presses, attack presses and actual spell casts.
    private static (int jumps, int attacks, int spellCasts) ArenaChase(StageDataSO stage, Vector2 botSpawn, Vector2 targetSpawn,
        SpellData spell = null, BotDifficulty difficulty = BotDifficulty.Medium, int ticks = 900)
    {
        var bot = Fighter(botSpawn.x, botSpawn.y, 1);
        var target = Fighter(targetSpawn.x, targetSpawn.y, 2);
        bot.isGrounded = target.isGrounded = false;
        bot.botDifficulty = difficulty;
        if (spell != null) bot.spellList.Add(spell);
        GameManager.Instance = new GameManager { stage = stage, players = new[] { bot, target }, playerCount = 2 };

        var botSim = new MovementSimulation(bot, stage);
        var targetSim = new MovementSimulation(target, stage);
        var neutral = new InputSnapshot(5, new[] { ButtonState.None, ButtonState.None, ButtonState.None });
        for (int tick = 0; tick < 120; tick++) { botSim.Step(neutral); targetSim.Step(neutral); }

        var chase = new ChaseAI { owner = bot };
        var castCode = typeof(NpcAI).GetField("castCode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int jumps = 0, attacks = 0, spellCasts = 0;
        for (int tick = 0; tick < ticks; tick++)
        {
            chase.Tick();
            botSim.Step(chase.npcInputSnapshot);
            if (chase.npcInputSnapshot.ButtonStates[1] == ButtonState.Pressed) jumps++;
            if (chase.npcInputSnapshot.ButtonStates[0] == ButtonState.Pressed)
            {
                attacks++;
                if ((uint)castCode.GetValue(chase) != 0) spellCasts++;
            }
        }
        return (jumps, attacks, spellCasts);
    }

    // BigQuadrupleDuel's centre column leaves only 32px gaps, too low for a 48px body: its left and
    // right halves never meet, so a bot there has nobody to chase and should stand its ground.
    private static bool SealedApart(string arena, Vector2 a, Vector2 b) =>
        arena == "BigQuadrupleDuel" && MathF.Sign(a.x) != MathF.Sign(b.x);

    // Every arena, every ordered pair of spawn points, with no spell and with a short starter spell on
    // Hard: the bot must get into a fight -- attack at least once in 15 seconds -- and must not jump-loop.
    // Before this sweep existed a quarter of pairs failed: stopped at gaps between same-height ledges,
    // parked out of attack range, hopped under ceilings, and treated every loop border as a wall.
    private static void ArenaSweep()
    {
        var starter = new SpellData { spellName = "Amon Slash", spellType = SpellType.Active, spellInput = 2 };
        // The longest code in the game (8 steps) on an Enhance spell, which isn't aimed at anyone.
        var codemehameha = new SpellData { spellName = "Codemehameha", spellType = SpellType.Active,
            spellInput = 0b_0000_0000_1000_0111_1000_0111_0000_1000 };
        var failures = new List<string>();
        int starterCasts = 0;
        foreach (var (name, stage) in AllArenas())
        {
            Vector2[] spawns = stage.playerSpawnTransform;
            for (int i = 0; i < spawns.Length; i++)
            for (int j = 0; j < spawns.Length; j++)
            {
                if (i == j || (spawns[i].x == spawns[j].x && spawns[i].y == spawns[j].y)
                    || SealedApart(name, spawns[i], spawns[j])) continue;
                foreach (var (spell, difficulty) in new[] { ((SpellData)null, BotDifficulty.Medium),
                    (starter, BotDifficulty.Hard), (codemehameha, BotDifficulty.Medium) })
                {
                    var (jumps, attacks, spellCasts) = ArenaChase(stage, spawns[i], spawns[j], spell, difficulty);
                    starterCasts += spellCasts;
                    if (attacks == 0)
                        failures.Add($"{name} {i}->{j} {(spell == null ? "no spell" : spell.spellName + " " + difficulty)}: "
                            + $"never attacked ({jumps} jumps)");
                }
            }
        }
        Check(failures.Count == 0, $"{failures.Count} chase(s) failed: " + string.Join("; ", failures.Take(8)));
        Check(starterCasts > 0, "No bot ever cast its short-range starter spell.");
    }

    // Dual Duel stacks two chambers split by a wall-to-wall slab, and looked sealed -- but it's a loop
    // stage: drop through the bottom-middle platform, fall out of the bottom, come back in at the top.
    private static void DualDuelThroughTheLoop()
    {
        var stage = LoadStage("Arenas/General (3-4 players)/DualDuel_Arena StageDataSO.asset");
        Check(stage.borderType == BorderType.Loop, "Dual Duel fixture isn't a loop stage.");
        var (jumps, attacks, _) = ArenaChase(stage, new Vector2(-224f, -192f), new Vector2(-224f, 0f));
        Check(attacks > 0, $"Never reached the upper floor through the loop border ({jumps} jumps).");
        Check(jumps <= 12, $"Jumped {jumps} times getting there.");
    }

    // With nobody reachable -- BigQuadrupleDuel's other half -- climbing at them is a jump loop against
    // whatever is in the way; the bot should stand its ground instead.
    private static void UnreachableTargetHoldsStill()
    {
        var stage = LoadStage("Arenas/Party (4 players)/BigQuadrupleDuel_Arena StageDataSO.asset");
        var (jumps, _, _) = ArenaChase(stage, stage.playerSpawnTransform[0], stage.playerSpawnTransform[1]);
        Check(jumps == 0, $"Jumped {jumps} times at an opponent there's no route to.");
    }

    // The fixed 56-124 band sat inside the room a 4-step code needs (128 at Medium), so a bot holding
    // only Sickle Of The Night parked there and threw nothing but basic attacks all round.
    private static void LongRangeKitSpacing()
    {
        var stage = Stage(new[] { (-600f, 600f, 0f, 20f) }, Array.Empty<(float,float,float,float)>());
        var bot = Fighter(-50f, 0f, 1);
        var target = Fighter(0f, 0f, 2);
        bot.spellList.Add(new SpellData { spellName = "Sickle Of The Night", spellType = SpellType.Active, spellInput = 4 });
        GameManager.Instance = new GameManager { stage = stage, players = new[] { bot, target }, playerCount = 2 };
        var chase = new ChaseAI { owner = bot };
        var simulation = new MovementSimulation(bot, stage);

        int castsFromRange = 0;
        for (int tick = 0; tick < 600; tick++)
        {
            chase.Tick();
            simulation.Step(chase.npcInputSnapshot);
            // Past ThreatRange a Code press can only be a spell -- the basic attack is kept closer in.
            if (chase.npcInputSnapshot.ButtonStates[0] == ButtonState.Pressed
                && MathF.Abs(target.position.X.ToFloat() - bot.position.X.ToFloat()) > 110f)
            {
                castsFromRange++;
            }
        }

        Check(castsFromRange > 0, $"Never cast its long-range spell; parked at {MathF.Abs(bot.position.X.ToFloat()):0} away.");
    }

    // The Gamba only takes a hit at its own height, so a bot on the disk platform above it has to
    // drop through first. The drop needs the bot at rest near its takeoff, and like the jump it
    // used to overshoot that window from either side forever from about one start in nine.
    private static void LobbyGambaDescent()
    {
        var stage = LobbyStage();
        for (float x = 80f; x <= 220f; x += 5f)
        {
            try { Simulate(stage, new Vector2(x, -112f), new Vector2(233f, -192f), true, 24, 3); }
            catch (InvalidOperationException error)
            {
                throw new InvalidOperationException($"From ({x}, -112) to P4's Gamba: {error.Message}");
            }
        }
    }

    // While the screen-transition cover is up the game drops every slot's input, so the bot asks to
    // walk and goes nowhere. Those frames must not count as stuck, or its first real step is an
    // "unstick" jump -- which put rematch-lobby bots on the disk platform over their own Gamba.
    private static void NoJumpAfterHeldInput()
    {
        var stage = LobbyStage();
        var npc = Probe(-192f);
        npc.owner.position = new FixedPosition(145f, -192f);
        npc.owner.playerWidth = 24;
        npc.owner.runSpeed = 3;
        GameManager.Instance = new GameManager { stage = stage };
        npc.Action = () => npc.TravelX(213f);

        // P4's spawn, walking toward its Gamba, with the owner frozen: no simulation step.
        for (int tick = 0; tick < 60; tick++)
        {
            npc.Tick();
        }

        var simulation = new MovementSimulation(npc.owner, stage);
        for (int tick = 0; tick < 120; tick++)
        {
            npc.Tick();
            simulation.Step(npc.npcInputSnapshot);
            Check(npc.owner.isGrounded,
                $"Jumped {tick} ticks after input came back, at ({npc.owner.position.X.ToFloat():0},{npc.owner.position.Y.ToFloat():0}).");
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
        public void TravelX(float x) => TravelTowardX(x);
        public SpellData Choose() => ChooseSpell();
        public void Drop(float targetY) { if (!TryDropToward(targetY)) Neutral(); }
    }
}





