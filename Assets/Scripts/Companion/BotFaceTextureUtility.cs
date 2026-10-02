using UnityEngine;

namespace AdaptiveVR.Companion
{
    /// <summary>
    /// Utility class that procedurally generates crisp, glowing electric-blue textures with
    /// 100% transparent backgrounds for the companion bot's eyes and mouth.
    /// This ensures NO visor, box, or background rectangle is drawn onto the bot's face.
    /// </summary>
    public static class BotFaceTextureUtility
    {
        private const int TexRes = 128; // Crisp resolution with soft anti-aliased glow

        /// <summary>
        /// Creates a standard transparent unlit/emissive material suitable for VR (Meta Quest).
        /// Compatible with Universal Render Pipeline (URP), Built-in, and Mobile shaders.
        /// </summary>
        public static Material CreateEmissiveFaceMaterial(Color glowColor, float intensity = 2.0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Mobile/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("UI/Default");

            Material mat = new Material(shader);
            mat.name = "CompanionBot_FaceFeature_Mat";

            Color hdrColor = glowColor * intensity;

            // Set properties for URP Unlit
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", glowColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", glowColor);

            // Emission setup
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", hdrColor);
            }

            // Ensure transparency and double-sided rendering
            mat.SetInt("_Surface", 1); // 1 = Transparent in URP
            mat.SetInt("_Blend", 0);   // Alpha blending
            mat.SetInt("_Cull", 0);    // Off (double-sided so it's always visible)
            mat.renderQueue = 3000;    // Transparent queue

            return mat;
        }

        #region Eye Textures

        /// <summary>
        /// Generates a cute, bright open eye (soft-glowing rounded circle/pill).
        /// </summary>
        public static Texture2D CreateEyeOpenTexture(Color blue)
        {
            Texture2D tex = CreateEmptyTexture();
            Vector2 center = new Vector2(0.5f, 0.5f);
            float innerRadius = 0.28f;
            float outerRadius = 0.40f;

            for (int y = 0; y < TexRes; y++)
            {
                for (int x = 0; x < TexRes; x++)
                {
                    Vector2 uv = new Vector2((float)x / TexRes, (float)y / TexRes);
                    // Slightly oblong/cute vertical ratio
                    Vector2 diff = uv - center;
                    diff.y *= 0.85f;
                    float dist = diff.magnitude;

                    float alpha = 0f;
                    if (dist < innerRadius)
                    {
                        alpha = 1f;
                    }
                    else if (dist < outerRadius)
                    {
                        alpha = Mathf.SmoothStep(1f, 0f, (dist - innerRadius) / (outerRadius - innerRadius));
                    }

                    // Soft specular inner glint for cute life in the eyes
                    Vector2 glintOffset = uv - new Vector2(0.60f, 0.65f);
                    if (glintOffset.magnitude < 0.08f)
                    {
                        alpha = Mathf.Max(alpha, 1f);
                    }

                    if (alpha > 0.001f)
                    {
                        Color c = blue;
                        c.a *= alpha;
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Generates a half-closed eye slit (for smooth blinking transitions).
        /// </summary>
        public static Texture2D CreateEyeHalfBlinkTexture(Color blue)
        {
            Texture2D tex = CreateEmptyTexture();
            Vector2 center = new Vector2(0.5f, 0.5f);
            float halfWidth = 0.35f;
            float halfHeight = 0.08f;
            float feather = 0.04f;

            for (int y = 0; y < TexRes; y++)
            {
                for (int x = 0; x < TexRes; x++)
                {
                    Vector2 uv = new Vector2((float)x / TexRes, (float)y / TexRes);
                    float dx = Mathf.Abs(uv.x - center.x);
                    float dy = Mathf.Abs(uv.y - center.y);

                    if (dx < halfWidth + feather && dy < halfHeight + feather)
                    {
                        float ax = dx <= halfWidth ? 1f : Mathf.SmoothStep(1f, 0f, (dx - halfWidth) / feather);
                        float ay = dy <= halfHeight ? 1f : Mathf.SmoothStep(1f, 0f, (dy - halfHeight) / feather);
                        float alpha = ax * ay;

                        if (alpha > 0.001f)
                        {
                            Color c = blue;
                            c.a *= alpha;
                            tex.SetPixel(x, y, c);
                        }
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Generates a fully closed eyelid line.
        /// </summary>
        public static Texture2D CreateEyeClosedTexture(Color blue)
        {
            Texture2D tex = CreateEmptyTexture();
            Vector2 center = new Vector2(0.5f, 0.5f);
            float halfWidth = 0.35f;
            float halfThickness = 0.035f;
            float feather = 0.03f;

            for (int y = 0; y < TexRes; y++)
            {
                for (int x = 0; x < TexRes; x++)
                {
                    Vector2 uv = new Vector2((float)x / TexRes, (float)y / TexRes);
                    float dx = Mathf.Abs(uv.x - center.x);
                    float dy = Mathf.Abs(uv.y - center.y);

                    if (dx < halfWidth + feather && dy < halfThickness + feather)
                    {
                        float ax = dx <= halfWidth ? 1f : Mathf.SmoothStep(1f, 0f, (dx - halfWidth) / feather);
                        float ay = dy <= halfThickness ? 1f : Mathf.SmoothStep(1f, 0f, (dy - halfThickness) / feather);
                        float alpha = ax * ay;

                        if (alpha > 0.001f)
                        {
                            Color c = blue;
                            c.a *= alpha;
                            tex.SetPixel(x, y, c);
                        }
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Generates cute joyful happy inverted arc eyes (^ ^).
        /// </summary>
        public static Texture2D CreateEyeHappyTexture(Color blue)
        {
            Texture2D tex = CreateEmptyTexture();
            float halfWidth = 0.32f;
            float arcHeight = 0.22f;
            float thickness = 0.055f;
            float feather = 0.035f;

            for (int y = 0; y < TexRes; y++)
            {
                for (int x = 0; x < TexRes; x++)
                {
                    Vector2 uv = new Vector2((float)x / TexRes, (float)y / TexRes);
                    float nx = (uv.x - 0.5f) / halfWidth; // -1 to 1

                    if (Mathf.Abs(nx) <= 1.15f)
                    {
                        // Parabolic arch: y reaches apex at center
                        float targetY = 0.36f + arcHeight * (1f - (nx * nx));
                        float dy = Mathf.Abs(uv.y - targetY);

                        float alpha = 0f;
                        if (dy <= thickness)
                        {
                            alpha = 1f;
                        }
                        else if (dy <= thickness + feather)
                        {
                            alpha = Mathf.SmoothStep(1f, 0f, (dy - thickness) / feather);
                        }

                        // Fade at extreme ends of the arc
                        float edgeFade = Mathf.SmoothStep(1.15f, 0.85f, Mathf.Abs(nx));
                        alpha *= edgeFade;

                        if (alpha > 0.001f)
                        {
                            Color c = blue;
                            c.a *= alpha;
                            tex.SetPixel(x, y, c);
                        }
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Generates a thinking/curious tilted eye (narrowed inquisitive look).
        /// </summary>
        public static Texture2D CreateEyeThinkingTexture(Color blue)
        {
            Texture2D tex = CreateEmptyTexture();
            Vector2 center = new Vector2(0.5f, 0.5f);
            float innerRadius = 0.22f;
            float outerRadius = 0.34f;

            for (int y = 0; y < TexRes; y++)
            {
                for (int x = 0; x < TexRes; x++)
                {
                    Vector2 uv = new Vector2((float)x / TexRes, (float)y / TexRes);
                    // Tilted elliptical squish
                    Vector2 diff = uv - center;
                    float rot = 25f * Mathf.Deg2Rad;
                    float rx = diff.x * Mathf.Cos(rot) - diff.y * Mathf.Sin(rot);
                    float ry = (diff.x * Mathf.Sin(rot) + diff.y * Mathf.Cos(rot)) * 1.5f;
                    float dist = Mathf.Sqrt(rx * rx + ry * ry);

                    float alpha = 0f;
                    if (dist < innerRadius)
                    {
                        alpha = 1f;
                    }
                    else if (dist < outerRadius)
                    {
                        alpha = Mathf.SmoothStep(1f, 0f, (dist - innerRadius) / (outerRadius - innerRadius));
                    }

                    if (alpha > 0.001f)
                    {
                        Color c = blue;
                        c.a *= alpha;
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        #endregion

        #region Mouth Textures

        /// <summary>
        /// Generates a pleasant resting smile arc.
        /// </summary>
        public static Texture2D CreateMouthSmileTexture(Color blue)
        {
            Texture2D tex = CreateEmptyTexture();
            float halfWidth = 0.34f;
            float arcDepth = 0.16f;
            float thickness = 0.045f;
            float feather = 0.035f;

            for (int y = 0; y < TexRes; y++)
            {
                for (int x = 0; x < TexRes; x++)
                {
                    Vector2 uv = new Vector2((float)x / TexRes, (float)y / TexRes);
                    float nx = (uv.x - 0.5f) / halfWidth;

                    if (Mathf.Abs(nx) <= 1.1f)
                    {
                        // Smile dips down in center
                        float targetY = 0.62f - arcDepth * (1f - (nx * nx));
                        float dy = Mathf.Abs(uv.y - targetY);

                        float alpha = 0f;
                        if (dy <= thickness)
                        {
                            alpha = 1f;
                        }
                        else if (dy <= thickness + feather)
                        {
                            alpha = Mathf.SmoothStep(1f, 0f, (dy - thickness) / feather);
                        }

                        float edgeFade = Mathf.SmoothStep(1.1f, 0.85f, Mathf.Abs(nx));
                        alpha *= edgeFade;

                        if (alpha > 0.001f)
                        {
                            Color c = blue;
                            c.a *= alpha;
                            tex.SetPixel(x, y, c);
                        }
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Generates slightly open talking mouth (frame 1 for speech animation).
        /// </summary>
        public static Texture2D CreateMouthTalkingSmallTexture(Color blue)
        {
            Texture2D tex = CreateEmptyTexture();
            Vector2 center = new Vector2(0.5f, 0.5f);
            float rx = 0.26f;
            float ry = 0.13f;
            float feather = 0.035f;

            for (int y = 0; y < TexRes; y++)
            {
                for (int x = 0; x < TexRes; x++)
                {
                    Vector2 uv = new Vector2((float)x / TexRes, (float)y / TexRes);
                    float normX = (uv.x - center.x) / rx;
                    float normY = (uv.y - center.y) / ry;
                    float d = Mathf.Sqrt(normX * normX + normY * normY);

                    float alpha = 0f;
                    if (d <= 1f)
                    {
                        alpha = 1f;
                    }
                    else if (d <= 1f + feather / ry)
                    {
                        alpha = Mathf.SmoothStep(1f, 0f, (d - 1f) / (feather / ry));
                    }

                    if (alpha > 0.001f)
                    {
                        Color c = blue;
                        c.a *= alpha;
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Generates open round mouth (frame 2 for talking / surprised / guidance).
        /// </summary>
        public static Texture2D CreateMouthTalkingOpenTexture(Color blue)
        {
            Texture2D tex = CreateEmptyTexture();
            Vector2 center = new Vector2(0.5f, 0.5f);
            float radius = 0.22f;
            float feather = 0.035f;

            for (int y = 0; y < TexRes; y++)
            {
                for (int x = 0; x < TexRes; x++)
                {
                    Vector2 uv = new Vector2((float)x / TexRes, (float)y / TexRes);
                    float d = Vector2.Distance(uv, center);

                    float alpha = 0f;
                    if (d <= radius)
                    {
                        alpha = 1f;
                    }
                    else if (d <= radius + feather)
                    {
                        alpha = Mathf.SmoothStep(1f, 0f, (d - radius) / feather);
                    }

                    if (alpha > 0.001f)
                    {
                        Color c = blue;
                        c.a *= alpha;
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Generates a wide joyful open grin for celebratory / happy moments.
        /// </summary>
        public static Texture2D CreateMouthHappyWideTexture(Color blue)
        {
            Texture2D tex = CreateEmptyTexture();
            float halfWidth = 0.38f;
            float depth = 0.24f;

            for (int y = 0; y < TexRes; y++)
            {
                for (int x = 0; x < TexRes; x++)
                {
                    Vector2 uv = new Vector2((float)x / TexRes, (float)y / TexRes);
                    float nx = (uv.x - 0.5f) / halfWidth;

                    if (Mathf.Abs(nx) <= 1f)
                    {
                        float topY = 0.65f;
                        float bottomY = topY - depth * (1f - (nx * nx));

                        if (uv.y <= topY && uv.y >= bottomY)
                        {
                            Color c = blue;
                            // Outer glow edge
                            float edgeDist = Mathf.Min(Mathf.Abs(uv.y - topY), Mathf.Abs(uv.y - bottomY));
                            float alpha = Mathf.Clamp01(edgeDist / 0.03f);
                            c.a = Mathf.Lerp(0.85f, 1f, alpha);
                            tex.SetPixel(x, y, c);
                        }
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Generates a wavy thinking mouth.
        /// </summary>
        public static Texture2D CreateMouthThinkingTexture(Color blue)
        {
            Texture2D tex = CreateEmptyTexture();
            float halfWidth = 0.30f;
            float thickness = 0.04f;
            float feather = 0.03f;

            for (int y = 0; y < TexRes; y++)
            {
                for (int x = 0; x < TexRes; x++)
                {
                    Vector2 uv = new Vector2((float)x / TexRes, (float)y / TexRes);
                    float nx = (uv.x - 0.5f) / halfWidth;

                    if (Mathf.Abs(nx) <= 1.05f)
                    {
                        float waveY = 0.50f + 0.05f * Mathf.Sin(nx * Mathf.PI * 2f);
                        float dy = Mathf.Abs(uv.y - waveY);

                        float alpha = 0f;
                        if (dy <= thickness)
                        {
                            alpha = 1f;
                        }
                        else if (dy <= thickness + feather)
                        {
                            alpha = Mathf.SmoothStep(1f, 0f, (dy - thickness) / feather);
                        }

                        float edgeFade = Mathf.SmoothStep(1.05f, 0.85f, Mathf.Abs(nx));
                        alpha *= edgeFade;

                        if (alpha > 0.001f)
                        {
                            Color c = blue;
                            c.a *= alpha;
                            tex.SetPixel(x, y, c);
                        }
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        #endregion

        #region Speech Bubble & Booster Graphics

        /// <summary>
        /// Generates a high-quality anti-aliased rounded rectangle sprite with a glowing cyan border
        /// and semi-transparent cosmic dark blue fill.
        /// </summary>
        public static Sprite CreateRoundedCardSprite(int width, int height, float cornerRadius, Color fillColor, Color borderColor, float borderWidth = 3f)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = (x + 0.5f) - halfW;
                    float py = (y + 0.5f) - halfH;

                    // Signed Distance Field (SDF) of rounded box
                    Vector2 q = new Vector2(Mathf.Abs(px) - (halfW - cornerRadius), Mathf.Abs(py) - (halfH - cornerRadius));
                    float dist = Mathf.Min(Mathf.Max(q.x, q.y), 0.0f) + new Vector2(Mathf.Max(q.x, 0.0f), Mathf.Max(q.y, 0.0f)).magnitude - cornerRadius;

                    Color pixelColor = Color.clear;

                    if (dist <= 0f)
                    {
                        // Inside the shape
                        if (dist >= -borderWidth)
                        {
                            // Border region with smooth inner blend
                            float borderT = Mathf.Clamp01((-dist) / borderWidth);
                            pixelColor = Color.Lerp(borderColor, fillColor, borderT * 0.3f);
                        }
                        else
                        {
                            // Interior fill
                            pixelColor = fillColor;
                        }

                        // Anti-aliased outer edge (dist near 0)
                        if (dist > -1.2f)
                        {
                            float edgeAlpha = Mathf.SmoothStep(0f, 1f, -dist / 1.2f);
                            pixelColor.a *= edgeAlpha;
                        }
                    }
                    else if (dist < 1.2f)
                    {
                        // Soft antialiased sub-pixel fade outside
                        float edgeAlpha = Mathf.SmoothStep(1f, 0f, dist / 1.2f);
                        pixelColor = borderColor;
                        pixelColor.a = edgeAlpha * 0.75f;
                    }

                    tex.SetPixel(x, y, pixelColor);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Generates an emissive energetic plasma jet flame texture for the bottom floating booster.
        /// Features a brilliant white-blue core near the top nozzle and a soft dissipating electric-blue tail.
        /// </summary>
        public static Texture2D CreateBoosterFlameTexture(Color blue)
        {
            int w = 64;
            int h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < h; y++)
            {
                float ny = (float)y / (h - 1); // 0 (bottom tip) to 1 (top nozzle)

                // Flame shape: wider near top (y=0.85), tapering down to sharp tip at y=0
                float widthFactor;
                if (ny > 0.85f)
                {
                    widthFactor = Mathf.Lerp(0.35f, 0.44f, (ny - 0.85f) / 0.15f);
                }
                else
                {
                    // Taper down to point
                    widthFactor = Mathf.Pow(ny / 0.85f, 0.72f) * 0.44f;
                }

                for (int x = 0; x < w; x++)
                {
                    float nx = ((float)x / (w - 1)) - 0.5f; // -0.5 to +0.5
                    float distFromCenter = Mathf.Abs(nx);

                    if (distFromCenter < widthFactor && ny > 0.02f)
                    {
                        float normX = distFromCenter / Mathf.Max(widthFactor, 0.001f);
                        float horizAlpha = Mathf.SmoothStep(1f, 0f, normX);
                        float vertAlpha = Mathf.SmoothStep(0f, 1f, ny / 0.22f);

                        float alpha = horizAlpha * vertAlpha;

                        // Color gradient: white-hot core at top, blue glow body
                        Color coreColor = Color.Lerp(blue, Color.white, Mathf.Pow(ny, 1.8f) * (1f - normX * 0.9f));
                        coreColor.a = alpha * 0.95f;

                        tex.SetPixel(x, y, coreColor);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Generates a glowing circular antigravity levitation ring texture for the booster.
        /// </summary>
        public static Texture2D CreateBoosterRingTexture(Color blue)
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(0.5f, 0.5f);
            float ringRadius = 0.38f;
            float thickness = 0.07f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 uv = new Vector2((float)x / size, (float)y / size);
                    float d = Vector2.Distance(uv, center);
                    float diff = Mathf.Abs(d - ringRadius);

                    float alpha = 0f;
                    if (diff < thickness)
                    {
                        alpha = Mathf.SmoothStep(1f, 0f, diff / thickness);
                    }

                    if (alpha > 0.001f)
                    {
                        Color c = blue;
                        c.a = alpha * 0.85f;
                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Creates an Additive glow material for soft antigravity floating elements.
        /// Additive blending physically guarantees ZERO black box borders or dark fringes!
        /// </summary>
        public static Material CreateAdditiveGlowMaterial(Color glowColor, float intensity = 1.8f)
        {
            Shader shader = Shader.Find("Mobile/Particles/Additive");
            if (shader == null) shader = Shader.Find("Particles/Additive");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

            Material mat = new Material(shader);
            mat.name = "CompanionBot_AdditiveGlow_Mat";

            Color finalColor = glowColor * intensity;

            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", finalColor);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", finalColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", finalColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", finalColor);
            }

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent in URP
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);     // 1 = Additive
            mat.renderQueue = 3000;

            return mat;
        }

        /// <summary>
        /// Generates a perfectly soft, ethereal antigravity halo disc texture.
        /// Completely seamless radial falloff from a glowing soft center to 0 alpha,
        /// with zero black edges/borders.
        /// </summary>
        public static Texture2D CreateSoftGlowHaloTexture(Color blue)
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(0.5f, 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 uv = new Vector2((float)x / (size - 1), (float)y / (size - 1));
                    float dist = Vector2.Distance(uv, center) * 2.0f; // 0 at center, 1 at edge

                    float alpha = 0f;
                    if (dist < 1.0f)
                    {
                        // Cosine smooth bell-curve falloff
                        alpha = Mathf.Cos(dist * Mathf.PI * 0.5f);
                        alpha = alpha * alpha;
                    }

                    // Bright soft cyan/white core fading to blue
                    Color col = Color.Lerp(blue, Color.white, Mathf.Pow(Mathf.Clamp01(1.0f - dist), 2.5f) * 0.5f);
                    col.a = alpha * 0.85f;

                    // Crucial: Keep RGB tinted blue when alpha=0 so texture filtering never darkens
                    if (alpha <= 0.001f)
                    {
                        col = new Color(blue.r, blue.g, blue.b, 0f);
                    }

                    tex.SetPixel(x, y, col);
                }
            }

            tex.Apply();
            return tex;
        }

        #endregion

        private static Texture2D CreateEmptyTexture()
        {
            Texture2D tex = new Texture2D(TexRes, TexRes, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] clear = new Color[TexRes * TexRes];
            for (int i = 0; i < clear.Length; i++)
            {
                clear[i] = new Color(0, 0, 0, 0); // 100% transparent background
            }
            tex.SetPixels(clear);
            return tex;
        }
    }
}
