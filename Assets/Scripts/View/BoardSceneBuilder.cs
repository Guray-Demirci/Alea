using System.Collections.Generic;
using UnityEngine;
using Tavla.Core.Board;

namespace Tavla.View
{
    /// <summary>
    /// Builds the parts of the board scene that never change during a game: the camera and its shake, the full-canvas
    /// art layers, the 24 points with their highlights and marks, the bear-off tray and the sprites generated at runtime.
    /// No game rules and no match state — <see cref="BoardManager"/> calls it once at startup and keeps what it returns.
    ///
    /// Every generated visual lives at the SCENE ROOT, at the world origin, not under BoardManager's object:
    /// "Create Empty" places objects where the Scene view is looking, not at (0,0,0), and the board background
    /// would otherwise drift away from the checkers.
    /// </summary>
    internal static class BoardSceneBuilder
    {
        /// <summary>
        /// Points the camera at the board, pixel-exact. Creates one if the scene has no camera at all —
        /// a safety net against the "Display 1: No cameras rendering" error.
        /// </summary>
        public static void ConfigureCamera(BoardLayout layout, Color background)
        {
            Camera cam = Camera.main;

            if (cam == null)
                cam = Object.FindAnyObjectByType<Camera>();

            if (cam == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                GameLog.Info("[View] The scene had no camera; one was created.");
            }

            cam.orthographic = true;
            cam.orthographicSize = layout.CameraOrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// The camera's shake for hits and blasts (<see cref="HitEffects"/>), moving in whole pixels of the board art.
        /// Put on the main camera whether or not BoardManager configures it; null if the scene has no camera.
        /// </summary>
        public static CameraShake AddCameraShake(BoardLayout layout)
        {
            Camera cam = Camera.main;

            if (cam == null)
                cam = Object.FindAnyObjectByType<Camera>();

            if (cam == null)
                return null;

            CameraShake shake = cam.GetComponent<CameraShake>();
            if (shake == null)
                shake = cam.gameObject.AddComponent<CameraShake>();

            shake.PixelsPerUnit = layout.PixelsPerUnit;
            return shake;
        }

        /// <summary>An empty object at the scene root and the world origin, to parent generated visuals under.</summary>
        public static Transform CreateWorldRoot(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        /// <summary>
        /// Places a full-canvas artwork (background, board, points). They all share the canvas size (320x180)
        /// and PPU, so placed at the origin each exactly fills what the camera sees; only the sorting order
        /// stacks them.
        /// </summary>
        public static void SpawnCanvasLayer(string name, Sprite sprite, int sortingOrder)
        {
            Transform layer = CreateWorldRoot(name);

            SpriteRenderer spriteRenderer = layer.gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        /// <summary>
        /// Builds the 24 points from the geometry. No need to place 24 objects by hand.
        /// The highlight is the outline artwork if assigned, otherwise <paramref name="solid"/> as a fill.
        /// Every point also gets its (hidden) score mark, <paramref name="mark"/>.
        /// </summary>
        /// <param name="prefab">The point prefab; null builds plain points with invisible colliders.</param>
        /// <returns>The points, indexed by point index (0-23).</returns>
        public static List<NodeView> SpawnNodes(BoardLayout layout, NodeView prefab, Sprite highlight, Sprite solid,
                                                Sprite mark, Color markColor)
        {
            Transform root = CreateWorldRoot("Nodes");
            Vector2 size = layout.GetNodeSize();
            List<NodeView> nodes = new List<NodeView>(GameState.NodeCount);

            for (int i = 0; i < GameState.NodeCount; i++)
            {
                NodeView view;

                if (prefab != null)
                {
                    view = Object.Instantiate(prefab, root);
                }
                else
                {
                    GameObject go = new GameObject("Node");
                    go.transform.SetParent(root, false);
                    go.AddComponent<BoxCollider2D>();
                    view = go.AddComponent<NodeView>();
                }

                view.Initialize(i, layout.GetNodeCenter(i), size);

                // The artwork is drawn for the top row (base up); the bottom row points the other way.
                if (highlight != null)
                    view.BuildArtworkHighlight(highlight, !layout.IsTopRow(i));
                else
                    view.BuildFillHighlight(solid, size);

                view.BuildMark(mark, markColor, layout.GetMarkPosition(i));

                nodes.Add(view);
            }

            return nodes;
        }

        /// <summary>
        /// Bear-off drop target. Mechanically an ordinary NodeView — the only difference is that its index is
        /// <see cref="GameState.BearOffIndex"/>. CheckerController therefore sends RequestMove(checker, BearOffIndex)
        /// without knowing anything, and TryMove already interprets it as a bear-off. The point prefab is
        /// deliberately not used: its highlight is sized for a triangle and would not fit this strip.
        /// </summary>
        /// <param name="trayColor">The faint, always-visible fill that shows the drop zone is there.</param>
        public static NodeView SpawnBearOffTarget(BoardLayout layout, Sprite solid, Color trayColor)
        {
            Transform root = CreateWorldRoot("BearOff");
            Vector2 size = layout.GetBearOffSize();

            GameObject go = new GameObject("BearOffTray");
            go.transform.SetParent(root, false);
            go.AddComponent<BoxCollider2D>();

            NodeView view = go.AddComponent<NodeView>();
            view.Initialize(GameState.BearOffIndex, layout.GetBearOffCenter(), size);
            view.BuildFillHighlight(solid, size);
            go.name = "BearOffTray"; // Initialize renames it to "Node_-02".

            GameObject tray = new GameObject("Tray");
            tray.transform.SetParent(go.transform, false);
            tray.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer spriteRenderer = tray.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = solid;
            spriteRenderer.color = trayColor;
            spriteRenderer.sortingOrder = SortingOrders.BearOffTray;

            return view;
        }

        /// <summary>
        /// 1x1 white sprite for highlights — generated in code so no asset is required.
        /// The caller owns it and its texture, and destroys both when done.
        /// </summary>
        public static Sprite CreateSolidSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.name = "SolidWhite";
            texture.filterMode = FilterMode.Point;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            // pixelsPerUnit = 1 -> the sprite is exactly 1 world unit and is sized via scale.
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "SolidWhite";
            return sprite;
        }

        /// <summary>
        /// The built-in score mark: a pair of footprints, white so the tint colours it.
        /// The caller owns it and its texture, and destroys both when done.
        /// </summary>
        public static Sprite CreateFootprintSprite(float pixelsPerUnit)
        {
            return CreatePatternSprite("Footprints", pixelsPerUnit,
                "......XX",
                ".....XXX",
                ".....XXX",
                "XX...XX.",
                "XXX...X.",
                "XXX.....",
                ".XX.....",
                ".X......");
        }

        /// <summary>
        /// The built-in mark of a checker a volley can hit (Bowman): four corners around the 12-pixel disc, white so the
        /// tint colours it. The caller owns it and its texture, and destroys both when done.
        /// </summary>
        public static Sprite CreateTargetSprite(float pixelsPerUnit)
        {
            return CreatePatternSprite("VolleyTarget", pixelsPerUnit,
                "XXX........XXX",
                "X............X",
                "X............X",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "..............",
                "X............X",
                "X............X",
                "XXX........XXX");
        }

        /// <summary>
        /// The built-in mark of a checker picked as a volley's target: the target's corners with a cross on the disc.
        /// The caller owns it and its texture, and destroys both when done.
        /// </summary>
        public static Sprite CreateAimedSprite(float pixelsPerUnit)
        {
            return CreatePatternSprite("VolleyAimed", pixelsPerUnit,
                "XXX........XXX",
                "X............X",
                "X............X",
                "..............",
                "..............",
                "......XX......",
                ".....XXXX.....",
                ".....XXXX.....",
                "......XX......",
                "..............",
                "..............",
                "X............X",
                "X............X",
                "XXX........XXX");
        }

        /// <summary>
        /// A white sprite drawn from a pattern written top-down, 'X' for a pixel, one texel per canvas pixel. Keep width
        /// and height even, so the centred pivot lands on a pixel edge and the sprite stays pixel-exact.
        /// </summary>
        private static Sprite CreatePatternSprite(string name, float pixelsPerUnit, params string[] rows)
        {
            int width = rows[0].Length;
            int height = rows.Length;

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            // Texture rows run bottom-up; the pattern is written top-down.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, height - 1 - y, rows[y][x] == 'X' ? Color.white : Color.clear);
            }
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.name = name;
            return sprite;
        }
    }
}
