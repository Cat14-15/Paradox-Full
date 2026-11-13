using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private Transform visualRoot;

    private Vector2 moveInput;
    private int facingSign = 1;

    /// <summary>현재 이동 입력값(정규화). 애니메이션 등 외부에서 참조용.</summary>
    public Vector2 MoveInput => moveInput;

    /// <summary>실제로 움직이는 중인지 여부.</summary>
    public bool IsMoving => moveInput.sqrMagnitude > 0.0001f;

    /// <summary>바라보는 방향(-1: 왼쪽, +1: 오른쪽).</summary>
    public int FacingSign => facingSign;

    private void Awake()
    {
        if (visualRoot == null && transform.childCount > 0)
        {
            visualRoot = transform.GetChild(0);
        }
    }

    private void Update()
    {
        ReadMoveInput();
        Move();
        UpdateFacing();
    }

    private void ReadMoveInput()
    {
        moveInput = Vector2.zero;

        // 퍼즐 UI 등이 열려 있으면 이동 입력을 막는다.
        if (Paradox.Language.InteractionState.InputLocked)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveInput.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveInput.y += 1f;

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    private void Move()
    {
        Vector3 delta = new Vector3(moveInput.x, moveInput.y, 0f) * moveSpeed * Time.deltaTime;
        transform.position += delta;
    }

    private void UpdateFacing()
    {
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            facingSign = moveInput.x < 0f ? -1 : 1;
        }

        if (visualRoot == null)
        {
            return;
        }

        Vector3 scale = visualRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * facingSign;
        visualRoot.localScale = scale;
    }
}
