using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// World-space crosshair that tracks the mouse, hides the OS cursor,
/// and clamps within the camera's visible bounds.
/// </summary>
public class CrosshairController : MonoBehaviour
{
	[SerializeField] private Camera targetCamera;
	[SerializeField] private bool hideCursor = true;
	[SerializeField] private float zDepth = 0f; // world Z for the crosshair object

	private void Awake()
	{
		if (targetCamera == null) targetCamera = Camera.main;
		if (hideCursor) Cursor.visible = false;
	}

	private void OnDestroy()
	{
		// Restore cursor visibility on destroy
		if (hideCursor) Cursor.visible = true;
	}

	private void LateUpdate()
	{
		if (targetCamera == null) return;

		// New Input System mouse position
		Vector2 mpos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
		Vector3 mouse = new Vector3(mpos.x, mpos.y, Mathf.Abs(zDepth - targetCamera.transform.position.z));
		Vector3 world = targetCamera.ScreenToWorldPoint(mouse);

		// Clamp to camera view rect
		Vector3 min = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0f, mouse.z));
		Vector3 max = targetCamera.ViewportToWorldPoint(new Vector3(1f, 1f, mouse.z));
		world.x = Mathf.Clamp(world.x, min.x, max.x);
		world.y = Mathf.Clamp(world.y, min.y, max.y);
		world.z = zDepth;
		transform.position = world;
	}

	public Vector2 GetWorldPosition()
	{
		return transform.position;
	}
}

