using UnityEngine;
using UnityEngine.SceneManagement;

namespace Witch.Bootstrap
{
    [DefaultExecutionOrder(-1100)]
    public sealed class FixedAspectRatioController : MonoBehaviour
    {
        private const float TargetAspectRatio = 16f / 9f;

        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;
        private Rect viewport = new Rect(0f, 0f, 1f, 1f);

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ApplyViewport();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Update()
        {
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                ApplyViewport();
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            GUI.color = Color.black;
            GUI.depth = int.MinValue;

            float left = viewport.x * Screen.width;
            float right = (viewport.x + viewport.width) * Screen.width;
            float bottom = viewport.y * Screen.height;
            float top = (viewport.y + viewport.height) * Screen.height;

            DrawBar(new Rect(0f, 0f, left, Screen.height));
            DrawBar(new Rect(right, 0f, Screen.width - right, Screen.height));
            DrawBar(new Rect(left, 0f, viewport.width * Screen.width, Screen.height - top));
            DrawBar(new Rect(left, Screen.height - bottom, viewport.width * Screen.width, bottom));

            GUI.depth = previousDepth;
            GUI.color = previousColor;
        }

        public static Rect CalculateViewport(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            float screenAspectRatio = (float)screenWidth / screenHeight;
            if (screenAspectRatio > TargetAspectRatio)
            {
                float width = TargetAspectRatio / screenAspectRatio;
                return new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }

            if (screenAspectRatio < TargetAspectRatio)
            {
                float height = screenAspectRatio / TargetAspectRatio;
                return new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }

            return new Rect(0f, 0f, 1f, 1f);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshViewport();
            ApplyViewport(scene);
        }

        private void ApplyViewport()
        {
            RefreshViewport();
            ApplyViewport(SceneManager.GetActiveScene());
        }

        private void RefreshViewport()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            viewport = CalculateViewport(lastScreenWidth, lastScreenHeight);
        }

        private void ApplyViewport(Scene scene)
        {
            Camera sceneCamera = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (sceneCamera == null)
                {
                    sceneCamera = root.GetComponentInChildren<Camera>(true);
                }
            }

            if (sceneCamera == null)
            {
                return;
            }

            sceneCamera.rect = viewport;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.isRootCanvas)
                    {
                        continue;
                    }

                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = sceneCamera;
                }
            }
        }

        private static void DrawBar(Rect rect)
        {
            if (rect.width > 0f && rect.height > 0f)
            {
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }
        }
    }
}
