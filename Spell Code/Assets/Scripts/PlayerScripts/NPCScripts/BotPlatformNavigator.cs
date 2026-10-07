using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Routes between walkable tops in the same AABBs used by the deterministic player collision.
/// Positions are feet positions and speeds/gravity are measured per simulation tick.
/// The graph is cached; only the small shortest-path search follows a moving target.
/// </summary>
public sealed class BotPlatformNavigator
{
    public enum Kind { Walk, Jump, Drop, Fall }

    public struct Step
    {
        public Kind kind;
        public Vector2 takeoff, landing;
        // Physical collider edges, rather than the inset interval used for landing.
        public float destinationLeft, destinationRight;
        public bool destinationOneWay;
        public int jumpsRequired;
        // Surface indices into the cached graph, so a jump can be re-checked from another takeoff.
        public int fromSurface, toSurface;
        // Crosses a side border of a loop stage: the landing is on the far side of the screen, so
        // steer out through the edge rather than back across the stage toward it.
        public bool wrapsX;
    }

    private struct Box
    {
        public float left, right, bottom, top;
    }

    private struct Surface
    {
        public float left, right, y, physicalLeft, physicalRight;
        public bool oneWay;
    }

    private struct Edge
    {
        public int destination;
        public Step step;
        public float cost;
    }

    private readonly List<Box> solids = new List<Box>();
    private readonly List<Surface> surfaces = new List<Surface>();
    private List<Edge>[] graph;
    private float[] distances;
    private bool[] visited;
    private int[] previous;
    private Step[] incoming;
    private StageDataSO cachedStage;
    private int geometryHash;
    private float halfWidth, height, jumpSpeed, gravity, runSpeed;
    private int maxJumps;
    private bool boundedX, boundedY;
    private float minX, maxX, minY, maxY;
    // BorderType.Loop: crossing a border snaps the body to the opposite one (PlayerController's
    // #region Borders), so borders are passages rather than walls.
    private bool looping;
    private float loopMinX, loopMaxX, loopMinY, loopMaxY;

    /// <summary>
    /// Horizontal offset from one x to another, taking the short way round a loop stage. Only for
    /// steering a step already flagged wrapsX; everything else stays in plain screen coordinates.
    /// </summary>
    public float WrapDeltaX(float fromX, float toX)
    {
        float delta = toX - fromX;
        if (!looping)
            return delta;
        float width = loopMaxX - loopMinX;
        if (delta > width * 0.5f) delta -= width;
        else if (delta < -width * 0.5f) delta += width;
        return delta;
    }

    public bool TryGetNextStep(StageDataSO stage, Vector2 position, Vector2 target,
        float halfWidth, float height, float jumpSpeed, float gravity, float runSpeed,
        int maxJumps, out Step step)
    {
        step = default(Step);
        if (stage == null || gravity <= 0f || runSpeed <= 0f || halfWidth <= 0f || height <= 0f)
            return false;

        int hash = GeometryHash(stage);
        if (cachedStage != stage || hash != geometryHash || this.halfWidth != halfWidth
            || this.height != height || this.jumpSpeed != jumpSpeed || this.gravity != gravity
            || this.runSpeed != runSpeed || this.maxJumps != maxJumps)
        {
            cachedStage = stage;
            geometryHash = hash;
            this.halfWidth = halfWidth;
            this.height = height;
            this.jumpSpeed = jumpSpeed;
            this.gravity = gravity;
            this.runSpeed = runSpeed;
            this.maxJumps = maxJumps;
            BuildGraph(stage);
        }

        int start = FindSurface(position, true);
        int goal = FindSurface(target, false);
        if (start < 0 || goal < 0) return false;
        if (start == goal)
        {
            step = MakeStep(Kind.Walk, position, new Vector2(
                Mathf.Clamp(target.x, surfaces[goal].left, surfaces[goal].right), surfaces[goal].y), start, goal, 0);
            return true;
        }

        for (int i = 0; i < surfaces.Count; i++)
        {
            distances[i] = float.PositiveInfinity;
            visited[i] = false;
            previous[i] = -1;
        }
        distances[start] = 0f;
        for (int iteration = 0; iteration < surfaces.Count; iteration++)
        {
            int current = -1;
            for (int i = 0; i < surfaces.Count; i++)
                if (!visited[i] && (current < 0 || distances[i] < distances[current])) current = i;
            if (current < 0 || float.IsPositiveInfinity(distances[current])) break;
            if (current == goal)
            {
                while (previous[current] != start && previous[current] >= 0) current = previous[current];
                step = incoming[current];
                return true;
            }
            visited[current] = true;
            float fromX = current == start ? position.x : incoming[current].landing.x;
            foreach (Edge edge in graph[current])
            {
                float cost = distances[current] + edge.cost + Mathf.Abs(fromX - edge.step.takeoff.x) / runSpeed;
                if (cost >= distances[edge.destination]) continue;
                distances[edge.destination] = cost;
                previous[edge.destination] = current;
                incoming[edge.destination] = edge.step;
            }
        }
        return false;
    }

    /// <summary>
    /// False only when both ends sit on surfaces the graph knows and no route joins them: somewhere
    /// this body genuinely cannot get to, like the other floor of a two-level arena. Anything the
    /// graph can't place counts as reachable, so a gap in the model never makes a bot give up.
    /// </summary>
    public bool CanReach(StageDataSO stage, Vector2 position, Vector2 target,
        float halfWidth, float height, float jumpSpeed, float gravity, float runSpeed, int maxJumps)
    {
        if (TryGetNextStep(stage, position, target, halfWidth, height, jumpSpeed, gravity, runSpeed,
                maxJumps, out _))
            return true;
        // A false above has already built the graph for these parameters, unless its inputs were
        // unusable -- in which case there is nothing to judge by.
        if (stage == null || cachedStage != stage) return true;
        return FindSurface(position, true) < 0 || FindSurface(target, false) < 0;
    }

    private int FindSurface(Vector2 point, bool supporting)
    {
        int best = -1;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < surfaces.Count; i++)
        {
            Surface s = surfaces[i];
            float dx = Mathf.Abs(point.x - Mathf.Clamp(point.x, s.left, s.right));
            float dy = point.y - s.y;
            // Standing is judged the way the collision pass judges it: any overlap of the body with
            // the block's real edges. Measuring from the inset walkable span instead lost a bot stood
            // on a ledge's lip after a jump that only just made it, and with no start surface every
            // later plan failed and the no-route fallback hopped on the spot.
            if (supporting && (Mathf.Abs(dy) > 12f
                || point.x + halfWidth <= s.physicalLeft || point.x - halfWidth >= s.physicalRight
                || dx > halfWidth * 2f + 2f)) continue;
            // A pickup may float above the ground. Prefer the top underneath its feet.
            float score = dx * 2f + Mathf.Abs(dy) * (dy < -12f ? 4f : 1f);
            if (score < bestScore) { best = i; bestScore = score; }
        }
        return best;
    }

    private void BuildGraph(StageDataSO stage)
    {
        solids.Clear();
        surfaces.Clear();
        looping = stage.borderType == BorderType.Loop
            && stage.borderMax.x > stage.borderMin.x && stage.borderMax.y > stage.borderMin.y;
        loopMinX = stage.borderMin.x;
        loopMaxX = stage.borderMax.x;
        loopMinY = stage.borderMin.y;
        loopMaxY = stage.borderMax.y;
        // A loop border is a passage, not a wall: arcs wrap across it instead of being refused.
        boundedX = !looping && stage.borderMax.x > stage.borderMin.x;
        boundedY = !looping && stage.borderMax.y > stage.borderMin.y;
        minX = stage.borderMin.x + halfWidth + 2f;
        maxX = stage.borderMax.x - halfWidth - 2f;
        minY = stage.borderMin.y + 2f;
        maxY = stage.borderMax.y;
        int count = PairCount(stage.solidCenter, stage.solidExtent);
        for (int i = 0; i < count; i++)
        {
            Vector2 c = stage.solidCenter[i], e = stage.solidExtent[i];
            solids.Add(new Box { left = c.x - e.x, right = c.x + e.x, bottom = c.y - e.y, top = c.y + e.y });
        }
        AddSurfaces(stage.solidCenter, stage.solidExtent, false);
        AddSurfaces(stage.platformCenter, stage.platformExtent, true);
        MergeJoinedSurfaces();
        graph = new List<Edge>[surfaces.Count];
        distances = new float[surfaces.Count];
        visited = new bool[surfaces.Count];
        previous = new int[surfaces.Count];
        incoming = new Step[surfaces.Count];
        for (int i = 0; i < surfaces.Count; i++)
        {
            graph[i] = new List<Edge>();
            for (int j = 0; j < surfaces.Count; j++)
            {
                if (i == j) continue;
                Step candidate;
                float duration;
                if (TryConnect(i, j, out candidate, out duration))
                    graph[i].Add(new Edge { destination = j, step = candidate, cost = duration + 8f });
            }
        }
    }

    private void AddSurfaces(Vector2[] centers, Vector2[] extents, bool oneWay)
    {
        for (int i = 0; i < PairCount(centers, extents); i++)
        {
            float left = centers[i].x - extents[i].x, right = centers[i].x + extents[i].x;
            float y = centers[i].y + extents[i].y;
            if (right <= left || (boundedY && (y < minY || y + height > maxY))
                || (looping && (y < loopMinY || y > loopMaxY))) continue;
            // Small pillars can support a body wider than their top.
            float inset = Mathf.Min(halfWidth + 2f, (right - left) * 0.45f);
            var intervals = new List<Vector2> { new Vector2(left + inset, right - inset) };
            foreach (Box box in solids)
            {
                if (box.top <= y + 1f || box.bottom >= y + height - 1f) continue;
                for (int p = intervals.Count - 1; p >= 0; p--)
                {
                    Vector2 interval = intervals[p];
                    float cutLeft = box.left - halfWidth - 2f, cutRight = box.right + halfWidth + 2f;
                    if (cutRight < interval.x || cutLeft > interval.y) continue;
                    intervals.RemoveAt(p);
                    if (cutLeft > interval.x) intervals.Add(new Vector2(interval.x, cutLeft));
                    if (cutRight < interval.y) intervals.Add(new Vector2(cutRight, interval.y));
                }
            }
            foreach (Vector2 interval in intervals)
            {
                float a = boundedX ? Mathf.Max(minX, interval.x) : interval.x;
                float b = boundedX ? Mathf.Min(maxX, interval.y) : interval.y;
                // Past a loop border is off-screen: the body snaps across before it gets there.
                if (looping)
                {
                    a = Mathf.Max(loopMinX, a);
                    b = Mathf.Min(loopMaxX, b);
                }
                if (b < a) continue;
                surfaces.Add(new Surface { left = a, right = b, y = y,
                    physicalLeft = left, physicalRight = right, oneWay = oneWay });
            }
        }
    }

    /// <summary>
    /// Overlapping blocks make one floor, but each became its own surface -- so a bot and a target
    /// on the same floor could count as on different surfaces and get routed round, on Loony out
    /// through a loop border and straight back in. Join same-height, same-kind surfaces whose
    /// standing room overlaps. Only overlap: merging across a gap the body can straddle hid gaps
    /// the planner needs to see.
    /// </summary>
    private void MergeJoinedSurfaces()
    {
        bool merged = true;
        while (merged)
        {
            merged = false;
            for (int i = 0; i < surfaces.Count && !merged; i++)
            {
                for (int j = i + 1; j < surfaces.Count && !merged; j++)
                {
                    Surface a = surfaces[i], b = surfaces[j];
                    if (a.oneWay != b.oneWay || Mathf.Abs(a.y - b.y) > 0.5f
                        || b.left > a.right || a.left > b.right)
                        continue;
                    a.left = Mathf.Min(a.left, b.left);
                    a.right = Mathf.Max(a.right, b.right);
                    a.physicalLeft = Mathf.Min(a.physicalLeft, b.physicalLeft);
                    a.physicalRight = Mathf.Max(a.physicalRight, b.physicalRight);
                    surfaces[i] = a;
                    surfaces.RemoveAt(j);
                    merged = true;
                }
            }
        }
    }

    private bool TryConnect(int from, int to, out Step step, out float duration)
    {
        Surface a = surfaces[from], b = surfaces[to];
        step = default(Step);
        duration = 0f;
        // Several of these usually clamp to the same x; simulating the same arc twice buys nothing
        // and the graph build is a one-off hitch at the start of every round, so drop repeats.
        float[] candidates = new[] { Mathf.Clamp((b.left + b.right) * 0.5f, a.left, a.right),
            a.left, a.right, Mathf.Clamp(b.left, a.left, a.right), Mathf.Clamp(b.right, a.left, a.right),
            Mathf.Clamp(b.physicalLeft - halfWidth - 3f, a.left, a.right),
            Mathf.Clamp(b.physicalRight + halfWidth + 3f, a.left, a.right) }
            .Distinct().ToArray();

        if (Mathf.Abs(a.y - b.y) < 1f)
        {
            float x = Mathf.Clamp((b.left + b.right) * 0.5f, a.left, a.right);
            float end = Mathf.Clamp(x, b.left, b.right);
            if (CanWalk(x, end, a.y))
            {
                step = MakeStep(Kind.Walk, new Vector2(x, a.y), new Vector2(end, b.y), from, to, 0);
                duration = Mathf.Abs(end - x) / runSpeed;
                return true;
            }

            // A loop stage's floor running into one side border carries on from the other.
            bool outRight = a.right >= loopMaxX - 0.5f && b.left <= loopMinX + 0.5f;
            bool outLeft = a.left <= loopMinX + 0.5f && b.right >= loopMaxX - 0.5f;
            if (looping && (outRight || outLeft))
            {
                step = MakeStep(Kind.Walk, new Vector2(outRight ? a.right : a.left, a.y),
                    new Vector2(outRight ? b.left : b.right, b.y), from, to, 0);
                step.wrapsX = true;
                duration = 1f;
                return true;
            }
        }

        if (TryArcs(from, to, candidates, false, out step, out duration)) return true;
        // A loop stage also connects the long way round: out through a side border and back in.
        return looping && TryArcs(from, to, candidates, true, out step, out duration);
    }

    private bool TryArcs(int from, int to, float[] candidates, bool viaWrap, out Step step, out float duration)
    {
        Surface a = surfaces[from], b = surfaces[to];
        step = default(Step);
        duration = 0f;
        // On a loop stage a fall can end anywhere: out of the bottom, back in at the top.
        if (b.y < a.y - 2f || looping)
        {
            if (a.oneWay)
                foreach (float x in candidates)
                    if (TryArc(from, to, Kind.Drop, x, 0, out step, out duration, viaWrap)) return true;
            // Walk off only when a simulated fall ends on a known lower surface.
            float[] edges = { a.physicalLeft - halfWidth - 3f, a.physicalRight + halfWidth + 3f };
            foreach (float x in edges)
                if (CanWalk(Mathf.Clamp(x, a.left, a.right), x, a.y, true)
                    && !Supported(x, a.y)
                    && TryArc(from, to, Kind.Fall, x, 0, out step, out duration, viaWrap)) return true;
        }
        for (int jumps = 1; jumps <= Mathf.Min(maxJumps, 4); jumps++)
        {
            // The six released ticks needed between jumps reduce the theoretical rise.
            float rise = jumpSpeed * (jumpSpeed + gravity) / (2f * gravity);
            if (jumpSpeed <= 0f || b.y - a.y > jumps * rise - (jumps - 1) * 10.5f * gravity - 2f) continue;
            foreach (float x in candidates)
                if (TryArc(from, to, Kind.Jump, x, jumps, out step, out duration, viaWrap)) return true;
        }
        return false;
    }

    private bool TryArc(int from, int to, Kind kind, float startX, int jumps,
        out Step step, out float duration, bool viaWrap = false)
    {
        Surface a = surfaces[from], b = surfaces[to];
        float landingInset = Mathf.Min(6f, (b.right - b.left) * 0.25f);
        float landingX = Mathf.Clamp(startX, b.left + landingInset, b.right - landingInset);
        float x = startX, y = a.y, velocity = kind == Kind.Jump ? jumpSpeed : 0f;
        int remaining = jumps - 1, releaseTicks = -1;
        float horizontalSpeed = 0f;
        bool clearedLedge = false, crossedX = false;
        step = default(Step);
        duration = 0f;
        for (int tick = 0; tick < 240; tick++)
        {
            if (remaining > 0 && velocity <= 0f)
            {
                if (releaseTicks < 0) releaseTicks = 6;
                else if (--releaseTicks <= 0) { velocity = jumpSpeed; remaining--; releaseTicks = -1; }
            }
            float nextY = y + velocity;
            float steerX = landingX;
            // Ground loss initially has zero vertical speed. Do not steer back onto the lip. (Bounded
            // above too: a fall that wrapped out of the bottom comes back in far above the lip.)
            if (kind == Kind.Fall && y > a.y - 2f && y <= a.y + 2f) steerX = startX;
            // Rising beside a solid ledge requires staying outside it until feet clear its top.
            if (y >= b.y + 2f) clearedLedge = true;
            if (!b.oneWay && !clearedLedge && kind == Kind.Jump)
                steerX = startX < b.physicalLeft ? b.physicalLeft - halfWidth - 3f
                    : startX > b.physicalRight ? b.physicalRight + halfWidth + 3f : startX;
            // Match the input controller's air acceleration and neutral braking. A full-speed
            // ballistic arc would incorrectly accept narrow ledges reached from a standing jump.
            float offset = viaWrap ? WrapDeltaX(x, steerX) : steerX - x;
            float speed = Mathf.Abs(horizontalSpeed);
            bool braking = Mathf.Abs(offset) <= 4f
                || (horizontalSpeed * offset > 0f && Mathf.Abs(offset) <= speed * (speed + 1f) * 1.5f);
            if (braking && tick % 3 == 0)
                horizontalSpeed -= horizontalSpeed > 0f ? Mathf.Min(1f, speed) : -Mathf.Min(1f, speed);
            else if (!braking && tick % 4 == 0)
                horizontalSpeed = Mathf.Clamp(horizontalSpeed + (offset > 0f ? 1f : -1f), -runSpeed, runSpeed);
            float nextX = x + horizontalSpeed;
            if ((boundedX && (nextX < minX || nextX > maxX))
                || (boundedY && (nextY < minY || nextY + height > maxY))) return false;

            // Horizontal collision can hold a falling bot outside a ledge until it clears the underside.
            foreach (Box box in solids)
            {
                if (nextY >= box.top - 0.5f || nextY + height <= box.bottom + 0.5f) continue;
                if (nextX + halfWidth <= box.left || nextX - halfWidth >= box.right) continue;
                if (x + halfWidth <= box.left + 0.5f) { nextX = box.left - halfWidth; horizontalSpeed = 0f; }
                else if (x - halfWidth >= box.right - 0.5f) { nextX = box.right + halfWidth; horizontalSpeed = 0f; }
                // Rising into a ceiling is a head bonk, not a failed jump: the collision pass stops
                // the rise and the body drops back down. Refusing these arcs outright made any
                // platform just under a ceiling -- Birdcage's top one -- look unreachable.
                else if (velocity > 0f && y + height <= box.bottom + 0.5f) { nextY = box.bottom - height; velocity = 0f; }
                else if (!(velocity <= 0f && y >= box.top - 0.5f)) return false;
            }
            if (velocity <= 0f)
            {
                int landed = -1;
                float highest = float.NegativeInfinity;
                for (int i = 0; i < surfaces.Count; i++)
                {
                    Surface s = surfaces[i];
                    if (kind == Kind.Drop && i == from) continue;
                    if (y < s.y - 0.5f || nextY > s.y || nextX < s.left - 4f || nextX > s.right + 4f) continue;
                    if (s.y > highest) { landed = i; highest = s.y; }
                }
                if (landed >= 0)
                {
                    if (landed != to || remaining > 0) return false;
                    step = MakeStep(kind, new Vector2(startX, a.y), new Vector2(landingX, b.y), from, to, jumps);
                    step.wrapsX = crossedX;
                    duration = tick + 1;
                    return true;
                }
            }
            x = nextX;
            y = nextY;
            // Loop borders, as PlayerController applies them after collision: snap to the other side.
            if (looping)
            {
                if (x > loopMaxX) { x = loopMinX; crossedX = true; }
                else if (x < loopMinX) { x = loopMaxX; crossedX = true; }
                if (y > loopMaxY) y = loopMinY;
                else if (y < loopMinY) y = loopMaxY;
            }
            velocity -= velocity > 0f ? gravity : gravity * 0.5f;
        }
        return false;
    }

    private bool CanWalk(float from, float to, float y, bool allowLeaving = false)
    {
        int samples = Mathf.CeilToInt(Mathf.Abs(to - from) / 8f) + 1;
        for (int i = 0; i <= samples; i++)
        {
            float x = from + (to - from) * i / samples;
            if (boundedX && (x < minX || x > maxX)) return false;
            foreach (Box box in solids)
                if (box.top > y + 1f && box.bottom < y + height - 1f
                    && box.left < x + halfWidth && box.right > x - halfWidth) return false;
            if (!allowLeaving && !Supported(x, y)) return false;
        }
        return true;
    }

    private bool Supported(float x, float y)
    {
        foreach (Surface s in surfaces)
            if (Mathf.Abs(s.y - y) < 1f && x + halfWidth > s.physicalLeft && x - halfWidth < s.physicalRight)
                return true;
        return false;
    }

    private Step MakeStep(Kind kind, Vector2 takeoff, Vector2 landing, int from, int to, int jumps)
    {
        Surface s = surfaces[to];
        return new Step { kind = kind, takeoff = takeoff, landing = landing,
            destinationLeft = s.physicalLeft, destinationRight = s.physicalRight,
            destinationOneWay = s.oneWay, jumpsRequired = jumps, fromSurface = from, toSurface = to };
    }

    /// <summary>
    /// Re-runs a planned jump's or drop's arc from startX instead of its planned takeoff, and returns
    /// the step rebuilt from there when it still lands on the same surface. A bot can't always stop
    /// exactly on a takeoff: from rest its smallest move is several pixels, so it can overshoot a
    /// narrow window forever. Only valid against the graph the step was planned on, i.e. before the
    /// next TryGetNextStep call can rebuild it.
    /// </summary>
    public bool TryTakeOffFrom(Step planned, float startX, out Step fromHere)
    {
        fromHere = planned;
        if ((planned.kind != Kind.Jump && planned.kind != Kind.Drop)
            || planned.fromSurface < 0 || planned.fromSurface >= surfaces.Count
            || planned.toSurface < 0 || planned.toSurface >= surfaces.Count)
            return false;
        return TryArc(planned.fromSurface, planned.toSurface, planned.kind, startX, planned.jumpsRequired,
            out fromHere, out _, planned.wrapsX);
    }

    private static int PairCount(Vector2[] centers, Vector2[] extents)
    {
        return centers == null || extents == null ? 0 : Mathf.Min(centers.Length, extents.Length);
    }

    private static int GeometryHash(StageDataSO stage)
    {
        unchecked
        {
            int hash = stage.borderMin.GetHashCode() * 397 ^ stage.borderMax.GetHashCode();
            hash = hash * 31 + (int)stage.borderType;
            hash = HashArray(hash, stage.solidCenter);
            hash = HashArray(hash, stage.solidExtent);
            hash = HashArray(hash, stage.platformCenter);
            hash = HashArray(hash, stage.platformExtent);
            return hash;
        }
    }

    private static int HashArray(int hash, Vector2[] array)
    {
        unchecked
        {
            hash = hash * 31 + (array == null ? -1 : array.Length);
            if (array != null) foreach (Vector2 point in array) hash = hash * 31 + point.GetHashCode();
            return hash;
        }
    }
}

