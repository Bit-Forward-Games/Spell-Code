using System.Collections.Generic;
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
                Mathf.Clamp(target.x, surfaces[goal].left, surfaces[goal].right), surfaces[goal].y), goal, 0);
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

    private int FindSurface(Vector2 point, bool supporting)
    {
        int best = -1;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < surfaces.Count; i++)
        {
            Surface s = surfaces[i];
            float dx = Mathf.Abs(point.x - Mathf.Clamp(point.x, s.left, s.right));
            float dy = point.y - s.y;
            if (supporting && (Mathf.Abs(dy) > 12f || dx > halfWidth + 2f)) continue;
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
        boundedX = stage.borderMax.x > stage.borderMin.x;
        boundedY = stage.borderMax.y > stage.borderMin.y;
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
            if (right <= left || (boundedY && (y < minY || y + height > maxY))) continue;
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
                if (b < a) continue;
                surfaces.Add(new Surface { left = a, right = b, y = y,
                    physicalLeft = left, physicalRight = right, oneWay = oneWay });
            }
        }
    }

    private bool TryConnect(int from, int to, out Step step, out float duration)
    {
        Surface a = surfaces[from], b = surfaces[to];
        step = default(Step);
        duration = 0f;
        float[] candidates = { Mathf.Clamp((b.left + b.right) * 0.5f, a.left, a.right),
            a.left, a.right, Mathf.Clamp(b.left, a.left, a.right), Mathf.Clamp(b.right, a.left, a.right),
            Mathf.Clamp(b.physicalLeft - halfWidth - 3f, a.left, a.right),
            Mathf.Clamp(b.physicalRight + halfWidth + 3f, a.left, a.right) };

        if (Mathf.Abs(a.y - b.y) < 1f)
        {
            float x = Mathf.Clamp((b.left + b.right) * 0.5f, a.left, a.right);
            float end = Mathf.Clamp(x, b.left, b.right);
            if (CanWalk(x, end, a.y))
            {
                step = MakeStep(Kind.Walk, new Vector2(x, a.y), new Vector2(end, b.y), to, 0);
                duration = Mathf.Abs(end - x) / runSpeed;
                return true;
            }
        }
        if (b.y < a.y - 2f)
        {
            if (a.oneWay)
                foreach (float x in candidates)
                    if (TryArc(from, to, Kind.Drop, x, 0, out step, out duration)) return true;
            // Walk off only when a simulated fall ends on a known lower surface.
            float[] edges = { a.physicalLeft - halfWidth - 3f, a.physicalRight + halfWidth + 3f };
            foreach (float x in edges)
                if (CanWalk(Mathf.Clamp(x, a.left, a.right), x, a.y, true)
                    && !Supported(x, a.y)
                    && TryArc(from, to, Kind.Fall, x, 0, out step, out duration)) return true;
        }
        for (int jumps = 1; jumps <= Mathf.Min(maxJumps, 4); jumps++)
        {
            // The six released ticks needed between jumps reduce the theoretical rise.
            float rise = jumpSpeed * (jumpSpeed + gravity) / (2f * gravity);
            if (jumpSpeed <= 0f || b.y - a.y > jumps * rise - (jumps - 1) * 10.5f * gravity - 2f) continue;
            foreach (float x in candidates)
                if (TryArc(from, to, Kind.Jump, x, jumps, out step, out duration)) return true;
        }
        return false;
    }

    private bool TryArc(int from, int to, Kind kind, float startX, int jumps,
        out Step step, out float duration)
    {
        Surface a = surfaces[from], b = surfaces[to];
        float landingInset = Mathf.Min(6f, (b.right - b.left) * 0.25f);
        float landingX = Mathf.Clamp(startX, b.left + landingInset, b.right - landingInset);
        float x = startX, y = a.y, velocity = kind == Kind.Jump ? jumpSpeed : 0f;
        int remaining = jumps - 1, releaseTicks = -1;
        float horizontalSpeed = 0f;
        bool clearedLedge = false;
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
            // Ground loss initially has zero vertical speed. Do not steer back onto the lip.
            if (kind == Kind.Fall && y > a.y - 2f) steerX = startX;
            // Rising beside a solid ledge requires staying outside it until feet clear its top.
            if (y >= b.y + 2f) clearedLedge = true;
            if (!b.oneWay && !clearedLedge && kind == Kind.Jump)
                steerX = startX < b.physicalLeft ? b.physicalLeft - halfWidth - 3f
                    : startX > b.physicalRight ? b.physicalRight + halfWidth + 3f : startX;
            // Match the input controller's air acceleration and neutral braking. A full-speed
            // ballistic arc would incorrectly accept narrow ledges reached from a standing jump.
            float offset = steerX - x;
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
                    step = MakeStep(kind, new Vector2(startX, a.y), new Vector2(landingX, b.y), to, jumps);
                    duration = tick + 1;
                    return true;
                }
            }
            x = nextX;
            y = nextY;
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

    private Step MakeStep(Kind kind, Vector2 takeoff, Vector2 landing, int to, int jumps)
    {
        Surface s = surfaces[to];
        return new Step { kind = kind, takeoff = takeoff, landing = landing,
            destinationLeft = s.physicalLeft, destinationRight = s.physicalRight,
            destinationOneWay = s.oneWay, jumpsRequired = jumps };
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

