using UnityEngine;

// Small deterministic movement model for integration regressions. Mirrors the
// idle/run/jump acceleration, held-jump gravity and AABB landing rules used by
// PlayerController. It intentionally excludes combat, animation and scene logic.
internal sealed class MovementSimulation
{
    private readonly PlayerController player;
    private readonly StageDataSO stage;
    private int lerpDelay;

    public MovementSimulation(PlayerController player, StageDataSO stage)
    {
        this.player = player;
        this.stage = stage;
    }

    public void Step(InputSnapshot input)
    {
        float x = player.position.X.ToFloat(), y = player.position.Y.ToFloat();
        float h = player.hSpd.ToFloat(), v = player.vSpd.ToFloat();
        bool grounded = player.isGrounded;
        bool jumpPressed = input.ButtonStates[1] == ButtonState.Pressed;
        bool jumpHeld = jumpPressed || input.ButtonStates[1] == ButtonState.Held;
        int direction = input.Direction % 3 == 0 ? 1 : input.Direction % 3 == 1 ? -1 : 0;
        bool drop = input.Direction == 2 && jumpHeld;
        if (!grounded) v -= v > 0f ? PlayerController.baseGravity : PlayerController.baseGravity * 0.5f;
        else player.jumpCount = player.maxJumpCount;

        switch (player.state)
        {
            case PlayerState.Idle:
                if (!grounded) { player.state = PlayerState.Jump; break; }
                if (drop && player.onPlatform) break;
                if (jumpPressed && player.jumpCount > 0) { Jump(ref v); break; }
                if (direction != 0) { player.facingRight = direction > 0; player.state = PlayerState.Run; break; }
                Lerp(ref h, 0f, 3);
                break;
            case PlayerState.Run:
                if (!grounded) { player.state = PlayerState.Jump; break; }
                if (drop && jumpPressed) throw new InvalidOperationException("Drop incorrectly triggered while running (slide).");
                if (jumpPressed && player.jumpCount > 0) { Jump(ref v); break; }
                if (direction == 0) { player.state = PlayerState.Idle; break; }
                if (player.facingRight != direction > 0) { player.facingRight = direction > 0; break; }
                Lerp(ref h, direction * player.runSpeed.ToFloat(), 1);
                break;
            case PlayerState.Jump:
                if (grounded) { player.state = PlayerState.Idle; break; }
                if (v < 0f && input.Direction <= 3) v -= 0.3f;
                if (v > 0f && !jumpHeld) v -= PlayerController.baseGravity * 2f;
                if (jumpPressed && !drop && player.jumpCount > 0) { Jump(ref v); break; }
                Lerp(ref h, direction * player.runSpeed.ToFloat(), direction == 0 ? 2 : 3);
                break;
        }

        float nextX = x + h, nextY = y + v;
        float halfWidth = player.playerWidth.ToFloat() / 2f, height = player.playerHeight.ToFloat();
        bool landing = false, oneWay = false;
        player.touchingLeftWall = player.touchingRightWall = false;
        for (int i = 0; i < stage.solidCenter.Length; i++)
        {
            Vector2 center = stage.solidCenter[i], extent = stage.solidExtent[i];
            float left = center.x - extent.x, right = center.x + extent.x;
            float top = center.y + extent.y, bottom = center.y - extent.y;
            if (nextX + halfWidth <= left || nextX - halfWidth >= right || nextY > top || nextY + height <= bottom) continue;
            if (y >= top - 0.01f && v <= 0f) { nextY = top; v = 0; landing = true; continue; }
            if (y + height <= bottom + 0.01f && v > 0f) { nextY = bottom - height; v = 0; continue; }
            if (x < center.x) { nextX = left - halfWidth; player.touchingRightWall = true; }
            else { nextX = right + halfWidth; player.touchingLeftWall = true; }
            h = 0f;
        }
        if (!drop)
        {
            for (int i = 0; i < stage.platformCenter.Length; i++)
            {
                Vector2 center = stage.platformCenter[i], extent = stage.platformExtent[i];
                float top = center.y + extent.y;
                if (nextX + halfWidth <= center.x - extent.x || nextX - halfWidth >= center.x + extent.x) continue;
                if (y >= top && nextY <= top && v <= 0f) { nextY = top; v = 0f; landing = oneWay = true; }
            }
        }
        player.position = new FixedPosition(nextX, nextY);
        player.hSpd = h;
        player.vSpd = v;
        player.isGrounded = landing;
        player.onPlatform = oneWay;
    }

    private void Jump(ref float speed)
    {
        speed = player.jumpForce.ToFloat();
        player.jumpCount--;
        player.state = PlayerState.Jump;
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

