using UnityEngine;

// Small deterministic movement model for integration regressions. A line-for-line port of the parts
// of PlayerController.PlayerUpdate that move a body: gravity (with terminal velocity), the Idle/Run/
// Jump/CodeWeave/CodeRelease movement, CheckStageDataSOCollision (smallest-penetration resolve, the
// border-spanning solid special cases, one-way platforms, borders) and then position += speed, in
// that order. Combat, animation and scene logic are left out.
//
// It used to be an approximation -- swept landings, borders after the move, no terminal velocity, and
// code entry walking the body around -- and the arena sweep passed on it while bots in the real game
// still jump-looped. Keep it a port: when PlayerController's movement changes, change this to match.
internal sealed class MovementSimulation
{
    private const float FastfallGravityMod = 0.3f;
    private const float TerminalVelocity = -20f;
    // CodeRelease runs on the cast animation's length; this is a stand-in for it.
    private const int CodeReleaseFrames = 24;

    private readonly PlayerController player;
    private readonly StageDataSO stage;
    private int lerpDelay;
    private int stateFrames;

    // Set when the bot asked for a slide, which it should never do: it's a commitment that sends the
    // body skidding wherever it was facing.
    public int Slides;

    public int Teleports;

    public MovementSimulation(PlayerController player, StageDataSO stage)
    {
        this.player = player;
        this.stage = stage;
    }

    public void Step(InputSnapshot input)
    {
        float h = player.hSpd.ToFloat(), v = player.vSpd.ToFloat();
        bool grounded = player.isGrounded;
        bool jumpPressed = input.ButtonStates[1] == ButtonState.Pressed;
        bool jumpHeld = jumpPressed || input.ButtonStates[1] == ButtonState.Held;
        bool codePressed = input.ButtonStates[0] == ButtonState.Pressed;
        bool codeHeld = codePressed || input.ButtonStates[0] == ButtonState.Held;
        int direction = input.Direction % 3 == 0 ? 1 : input.Direction % 3 == 1 ? -1 : 0;
        float run = player.runSpeed.ToFloat();

        if (!grounded)
        {
            if (v > 0f) v -= PlayerController.baseGravity;
            else if (v > TerminalVelocity) v -= PlayerController.baseGravity / 2f;
        }
        else player.jumpCount = player.maxJumpCount;

        stateFrames++;
        switch (player.state)
        {
            case PlayerState.Idle:
                if (!grounded) { SetState(PlayerState.Jump); break; }
                if (input.Direction < 4 && jumpPressed)
                {
                    if (input.Direction == 2 && player.onPlatform) break;
                    Slide();
                    break;
                }
                if (player.jumpCount > 0 && jumpPressed) { Jump(ref v); break; }
                if (input.Direction % 3 == 0) { player.facingRight = true; SetState(PlayerState.Run); break; }
                if (input.Direction % 3 == 1) { player.facingRight = false; SetState(PlayerState.Run); break; }
                if (codePressed && !player.collidingWithFloppy) { SetState(PlayerState.CodeWeave); break; }
                Lerp(ref h, 0f, 3);
                break;
            case PlayerState.Run:
                if (!grounded) { SetState(PlayerState.Jump); break; }
                if (input.Direction < 4 && jumpPressed) { Slide(); break; }
                if (codePressed && !player.collidingWithFloppy) { SetState(PlayerState.CodeWeave); break; }
                if (player.jumpCount > 0 && jumpPressed) { Jump(ref v); break; }
                if (input.Direction % 3 == (player.facingRight ? 1 : 0)) { player.facingRight = !player.facingRight; break; }
                if (input.Direction % 3 == (player.facingRight ? 0 : 1)) { Lerp(ref h, (player.facingRight ? 1 : -1) * run, 1); break; }
                SetState(PlayerState.Idle);
                break;
            case PlayerState.Jump:
                if (grounded) { SetState(PlayerState.Idle); break; }
                if (v < 0f && input.Direction <= 3) v -= FastfallGravityMod;
                if (v > 0f && !jumpHeld) v -= PlayerController.baseGravity * 2f;
                if (codePressed && !player.collidingWithFloppy) { SetState(PlayerState.CodeWeave); break; }
                if (input.Direction < 4 && jumpPressed) { Slide(); break; }
                if (player.jumpCount > 0 && jumpPressed) { Jump(ref v); break; }
                if (direction > 0) { player.facingRight = true; Lerp(ref h, run, 3); }
                else if (direction < 0) { player.facingRight = false; Lerp(ref h, -run, 3); }
                else Lerp(ref h, 0f, 2);
                break;
            case PlayerState.CodeWeave:
                if (!codeHeld) { SetState(PlayerState.CodeRelease); break; }
                Lerp(ref h, 0f, grounded ? 3 : 15);
                break;
            case PlayerState.CodeRelease:
                if (stateFrames >= CodeReleaseFrames) { SetState(grounded ? PlayerState.Idle : PlayerState.Jump); break; }
                Lerp(ref h, 0f, 3);
                break;
        }

        // UpdateResources, just before the collision pass.
        if (player.portalCooldown > 0) player.portalCooldown--;

        Collide(input, ref h, ref v);

        player.position = new FixedPosition(player.position.X.ToFloat() + h, player.position.Y.ToFloat() + v);
        player.hSpd = h;
        player.vSpd = v;
    }

    // CheckStageDataSOCollision. Detection uses the next frame's box, computed once up front, while
    // resolution snaps the CURRENT position to the solid's edge and zeroes speed on that axis; the
    // move happens afterwards. Borders are applied here too, before the move.
    private void Collide(InputSnapshot input, ref float h, ref float v)
    {
        player.isGrounded = false;
        player.onPlatform = false;
        player.touchingLeftWall = false;
        player.touchingRightWall = false;

        float x = player.position.X.ToFloat(), y = player.position.Y.ToFloat();
        float halfW = player.playerWidth.ToFloat() / 2f, height = player.playerHeight.ToFloat();
        bool hasStageBounds = stage.borderMin != stage.borderMax;
        float stageMinX = stage.borderMin.x, stageMaxX = stage.borderMax.x;
        float stageMinY = stage.borderMin.y, stageMaxY = stage.borderMax.y;

        float pMinX = x + h - halfW, pMaxX = x + h + halfW, pMinY = y + v, pMaxY = y + v + height;
        int solidCount = Math.Min(stage.solidCenter.Length, stage.solidExtent.Length);
        for (int i = 0; i < solidCount; i++)
        {
            Vector2 center = stage.solidCenter[i], extent = stage.solidExtent[i];
            float sMinX = center.x - extent.x, sMaxX = center.x + extent.x;
            float sMinY = center.y - extent.y, sMaxY = center.y + extent.y;
            if (pMaxX < sMinX || pMinX > sMaxX || pMaxY < sMinY || pMinY > sMaxY) continue;

            float overlapX = Math.Min(pMaxX, sMaxX) - Math.Max(pMinX, sMinX);
            float overlapY = Math.Min(pMaxY, sMaxY) - Math.Max(pMinY, sMinY);
            if (overlapX < 0f || overlapY < 0f) continue;

            bool skipHorizontal = hasStageBounds && sMinX < stageMinX && sMaxX > stageMaxX;
            bool skipVertical = hasStageBounds && sMinY < stageMinY && sMaxY > stageMaxY;
            if (skipHorizontal && skipVertical) continue;

            bool resolveHorizontally = !skipHorizontal && (skipVertical || overlapX < overlapY);
            if (resolveHorizontally)
            {
                if (x < center.x)
                {
                    if (hasStageBounds && sMinX < stageMinX) { x = sMaxX + halfW; player.touchingLeftWall = true; }
                    else { x = sMinX - halfW; player.touchingRightWall = true; }
                }
                else
                {
                    if (hasStageBounds && sMaxX > stageMaxX) { x = sMinX - halfW; player.touchingRightWall = true; }
                    else { x = sMaxX + halfW; player.touchingLeftWall = true; }
                }
                h = 0f;
            }
            else
            {
                if (y < center.y)
                {
                    if (hasStageBounds && sMinY < stageMinY) { y = sMaxY; player.isGrounded = true; }
                    else y = sMinY - height;
                }
                else
                {
                    if (hasStageBounds && sMaxY > stageMaxY) y = sMinY - height;
                    else { y = sMaxY; player.isGrounded = true; }
                }
                v = 0f;
            }
        }

        // One-way platforms, against the box as the solids left it.
        pMinX = x + h - halfW; pMaxX = x + h + halfW; pMinY = y + v; pMaxY = y + v + height;
        int platformCount = Math.Min(stage.platformCenter.Length, stage.platformExtent.Length);
        for (int i = 0; i < platformCount; i++)
        {
            Vector2 center = stage.platformCenter[i], extent = stage.platformExtent[i];
            float sMinX = center.x - extent.x, sMaxX = center.x + extent.x;
            float top = center.y + extent.y, bottom = center.y - extent.y;
            if (pMaxX < sMinX || pMinX > sMaxX) continue;
            if (pMaxY <= bottom) continue;
            if (Math.Min(pMaxX, sMaxX) - Math.Max(pMinX, sMinX) <= 0f) continue;
            if (pMinY <= top && y >= top && v <= 0f)
            {
                // Down+jump drops through -- and returns straight out of the collision pass, so the
                // borders below are skipped for this frame.
                if (input.Direction == 2 && (input.ButtonStates[1] == ButtonState.Pressed || input.ButtonStates[1] == ButtonState.Held))
                {
                    player.position = new FixedPosition(x, y);
                    return;
                }
                y = top;
                v = 0f;
                player.isGrounded = true;
                player.onPlatform = true;
            }
        }

        if (hasStageBounds)
        {
            if (stage.borderType == BorderType.Collision)
            {
                x = Math.Clamp(x, stageMinX, stageMaxX);
                y = Math.Clamp(y, stageMinY, stageMaxY);
            }
            else if (stage.borderType == BorderType.Loop)
            {
                if (x > stageMaxX) x = stageMinX;
                else if (x < stageMinX) x = stageMaxX;
                if (y > stageMaxY) y = stageMinY;
                else if (y < stageMinY) y = stageMaxY;
            }
        }
        player.position = new FixedPosition(x, y);

        // Portals: feet within 40 of either end of a pair, judged on next frame's position, sends the body
        // to the other end with its speed intact. The teleport re-runs this pass at the destination
        // before the cooldown is set, where portalPrimed (already cleared) stops it bouncing straight back.
        if (player.portalCooldown == 0 && stage.portalAs != null && stage.portalBs != null)
        {
            float nextX = x + h, nextY = y + v;
            bool teleported = false;
            for (int i = 0; i < Math.Min(stage.portalAs.Length, stage.portalBs.Length); i++)
            {
                Vector2 a = stage.portalAs[i], b = stage.portalBs[i];
                bool inA = InPortal(nextX, nextY, a);
                if (!inA && !InPortal(nextX, nextY, b)) continue;
                if (!player.portalPrimed) return;
                player.portalPrimed = false;
                player.position = new FixedPosition((inA ? b : a).x - h, (inA ? b : a).y - v);
                Collide(input, ref h, ref v);
                player.portalCooldown = 120;
                Teleports++;
                teleported = true;
                break;
            }
            if (!teleported) player.portalPrimed = true;
        }
    }

    private static bool InPortal(float x, float y, Vector2 portal) =>
        (x - portal.x) * (x - portal.x) + (y - portal.y) * (y - portal.y) <= 40f * 40f;

    private void SetState(PlayerState state)
    {
        player.state = state;
        stateFrames = 0;
    }

    private void Slide()
    {
        Slides++;
        throw new InvalidOperationException($"Bot asked for a slide at ({player.position.X.ToFloat():0},{player.position.Y.ToFloat():0}) in {player.state}.");
    }

    private void Jump(ref float speed)
    {
        speed = player.jumpForce.ToFloat();
        player.jumpCount--;
        SetState(PlayerState.Jump);
    }

    private void Lerp(ref float speed, float target, int delay)
    {
        if (lerpDelay >= delay)
        {
            lerpDelay = 0;
            speed += MathF.Sign(target - speed);
            if (MathF.Abs(speed) < 1f) speed = 0;
        }
        else lerpDelay++;
    }
}
