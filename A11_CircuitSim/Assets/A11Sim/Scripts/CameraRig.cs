using System;
using UnityEngine;

namespace A11Sim
{
    /// Orthographic pan / zoom. Zoom keeps the world point under the cursor fixed
    /// (same approach as SebLague/Digital-Logic-Sim CameraController, MIT).
    public class CameraRig : MonoBehaviour
    {
        public Camera cam;
        public Rect content;
        public Func<Vector2, bool> blocked;
        public float minSize = 1.5f;
        public bool Panning => panButton >= 0;

        int panButton = -1;
        Vector2 lastMouse;
        float fitSize = 100;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        /// Free part of the screen (pixels, y up) that is not covered by HUD panels.
        public Func<Rect> freeRect;

        public void Fit()
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            var f = freeRect != null ? freeRect() : screen;
            if (f.width < 50 || f.height < 50) f = screen;
            float upp = Mathf.Max(content.width / f.width, content.height / f.height) * 1.03f;
            fitSize = upp * Screen.height * 0.5f;
            cam.orthographicSize = fitSize;
            Vector2 off = (f.center - screen.center) * upp;
            var c = content.center - off;
            transform.position = new Vector3(c.x, c.y, -10);
        }

        public void Focus(Vector2 center, float size)
        {
            cam.orthographicSize = Mathf.Clamp(size, minSize, fitSize * 1.6f);
            transform.position = new Vector3(center.x, center.y, -10);
        }

        public void BeginPan(int button)
        {
            panButton = button;
            lastMouse = Input.mousePosition;
        }

        void ZoomAt(Vector2 screen, float factor)
        {
            Vector3 before = cam.ScreenToWorldPoint(screen);
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, minSize, fitSize * 1.6f);
            Vector3 after = cam.ScreenToWorldPoint(screen);
            transform.position += before - after;
        }

        void LateUpdate()
        {
            Vector2 mouse = Input.mousePosition;
            bool overUi = blocked != null && blocked(mouse);
            float dt = Time.unscaledDeltaTime;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f && !overUi) ZoomAt(mouse, Mathf.Pow(0.87f, scroll));
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.PageUp)) ZoomAt(center, 1f - 1.6f * dt);
            if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.PageDown)) ZoomAt(center, 1f + 1.6f * dt);

            if (panButton < 0 && !overUi)
            {
                if (Input.GetMouseButtonDown(1)) BeginPan(1);
                else if (Input.GetMouseButtonDown(2)) BeginPan(2);
            }
            if (panButton >= 0)
            {
                Vector3 a = cam.ScreenToWorldPoint(lastMouse);
                Vector3 b = cam.ScreenToWorldPoint(mouse);
                transform.position += a - b;
                lastMouse = mouse;
                if (!Input.GetMouseButton(panButton)) panButton = -1;
            }

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) move.x -= 1;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) move.x += 1;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) move.y += 1;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) move.y -= 1;
            transform.position += move * cam.orthographicSize * 1.3f * dt;
            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Home)) Fit();

            var p = transform.position;
            p.x = Mathf.Clamp(p.x, content.xMin - content.width * 0.3f, content.xMax + content.width * 0.3f);
            p.y = Mathf.Clamp(p.y, content.yMin - content.height * 0.3f, content.yMax + content.height * 0.3f);
            p.z = -10;
            transform.position = p;

            Shader.SetGlobalFloat("_WorldPerPixel", 2f * cam.orthographicSize / Mathf.Max(1, Screen.height));
        }
    }
}
