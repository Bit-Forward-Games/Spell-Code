using UnityEngine;
using UnityEngine.InputSystem;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class OnlineHostDoor : MonoBehaviour
{
    private Animator animator;
    private bool isOpen;

    [SerializeField] private float colliderRadius = 32f;
    [SerializeField] private bool requireSoloPlayer = true;
    [SerializeField] private bool requireButtonPress = true;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float triggerCooldownSeconds = 0.5f;
    [SerializeField] private bool debugLogs = false;
    private float nextTriggerTime;
    private bool interactPressedThisFrame;

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (!requireButtonPress)
        {
            interactPressedThisFrame = false;
            return;
        }

        bool pressed = false;
        if (Keyboard.current != null)
        {
            pressed = GetKeyPressedThisFrame(interactKey);
        }

        if (!pressed)
        {
            pressed = Input.GetKeyDown(interactKey);
        }

        interactPressedThisFrame = pressed;
    }

    public bool CheckOpenDoor()
    {
        if (GameManager.Instance == null)
        {
            return false;
        }

        bool shouldBeOpen =
            !GameManager.Instance.isOnlineMatchActive
            && GameManager.Instance.playerCount > 0
            && (!requireSoloPlayer || GameManager.Instance.playerCount == 1);

        isOpen = shouldBeOpen;

        if (debugLogs)
        {
            Debug.Log($"[OnlineHostDoor] Open={isOpen} OnlineActive={GameManager.Instance.isOnlineMatchActive} Players={GameManager.Instance.playerCount}");
        }

        if (animator != null && animator.GetBool("open") != isOpen)
        {
            animator.SetBool("open", isOpen);
        }

        return isOpen;
    }

    public bool CheckHostTrigger()
    {
        if (!isOpen)
        {
            return false;
        }

        if (SteamLobbyManager.Instance == null)
        {
            if (debugLogs)
            {
                Debug.Log($"[OnlineHostDoor] Host blocked. LobbyMgr? {(SteamLobbyManager.Instance != null)} HostingFlow={SteamLobbyManager.Instance?.IsHostingFlow} InLobby={SteamLobbyManager.Instance?.IsInLobby}");
            }
            return false;
        }

        if (GameManager.Instance == null)
        {
            return false;
        }

        if (Time.unscaledTime < nextTriggerTime)
        {
            return false;
        }

        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {
            PlayerController player = GameManager.Instance.players[i];
            if (player == null)
            {
                continue;
            }

            if (IsPlayerInRange(player))
            {
                if (requireButtonPress && !interactPressedThisFrame)
                {
                    return false;
                }

                if (debugLogs)
                {
                    Debug.Log("[OnlineHostDoor] Player in range. Starting host flow.");
                }
                nextTriggerTime = Time.unscaledTime + Mathf.Max(0.1f, triggerCooldownSeconds);

                if (SteamLobbyManager.Instance.IsInLobby)
                {
                    bool opened = SteamLobbyManager.Instance.TryOpenInviteOverlay();
                    if (!opened)
                    {
                        SteamLobbyManager.Instance.HostAndInvite();
                    }
                }
                else
                {
                    SteamLobbyManager.Instance.HostAndInvite();
                }
                return true;
            }
        }

        return false;
    }

    private bool IsPlayerInRange(PlayerController player)
    {
        FixedVec2 doorPos = FixedVec2.FromFloat(transform.position.x, transform.position.y);

        Fixed dx = Fixed.Abs(player.position.X - doorPos.X) / Fixed.FromInt(10);
        Fixed dy = Fixed.Abs(player.position.Y - doorPos.Y) / Fixed.FromInt(10);
        Fixed distSq = (dx * dx) + (dy * dy);

        Fixed radius = Fixed.FromFloat(colliderRadius / 10f);
        Fixed radiusSq = radius * radius;

        return distSq <= radiusSq;
    }

    private bool GetKeyPressedThisFrame(KeyCode key)
    {
        return key switch
        {
            KeyCode.E => Keyboard.current.eKey.wasPressedThisFrame,
            KeyCode.F => Keyboard.current.fKey.wasPressedThisFrame,
            KeyCode.Space => Keyboard.current.spaceKey.wasPressedThisFrame,
            KeyCode.Return => Keyboard.current.enterKey.wasPressedThisFrame,
            _ => false
        };
    }
}
