// [수정] WorkPartner/shaders/HueShift.fx (안전성 강화)
sampler2D InputSampler : register(s0);
float HueShiftValue : register(c0); // 0.0 to 1.0 (Normalized Hue Shift 0-360 -> 0-1)

// Helper: Hue to RGB component
float HueToComponent(float p, float q, float t)
{
    // Ensure t is within [0, 1] range safely
	t = fmod(t + 1.0f, 1.0f);
	if (t < 1.0f / 6.0f)
		return p + (q - p) * 6.0f * t;
	if (t < 1.0f / 2.0f)
		return q;
	if (t < 2.0f / 3.0f)
		return p + (q - p) * (2.0f / 3.0f - t) * 6.0f;
	return p;
}

// HSL to RGB
float3 HslToRgb(float3 hsl)
{
	float h = hsl.x;
	float s = hsl.y;
	float l = hsl.z;
	float r, g, b;

	if (s == 0.0f)
	{
		r = g = b = l; // Achromatic
	}
	else
	{
		float q = l < 0.5f ? l * (1.0f + s) : l + s - l * s;
		float p = 2.0f * l - q;
        // Ensure hue values passed are positive and wrapped
		r = HueToComponent(p, q, h + 1.0f / 3.0f);
		g = HueToComponent(p, q, h);
		b = HueToComponent(p, q, h - 1.0f / 3.0f);
	}
    // Clamp results to avoid potential issues
	return saturate(float3(r, g, b));
}

// RGB to HSL
float3 RgbToHsl(float3 color)
{
	float r = color.r;
	float g = color.g;
	float b = color.b;
	float maxVal = max(r, max(g, b));
	float minVal = min(r, min(g, b));
	float h = 0.0f, s = 0.0f, l = (maxVal + minVal) / 2.0f;
	float d = maxVal - minVal;

	if (d > 0.00001f) // Check for non-achromatic color with tolerance
	{
		s = l > 0.5f ? d / (2.0f - maxVal - minVal) : d / (maxVal + minVal);
		if (maxVal == r)
			h = fmod(((g - b) / d) / 6.0f + 1.0f, 1.0f); // Normalize and wrap hue
		else if (maxVal == g)
			h = (2.0f + (b - r) / d) / 6.0f;
		else
			h = (4.0f + (r - g) / d) / 6.0f;
	}
	return float3(h, s, l);
}

// Main Pixel Shader
float4 main(float2 uv : TEXCOORD) : COLOR
{
	float4 originalColor = tex2D(InputSampler, uv);

    // Skip transparent pixels early
	if (originalColor.a <= 0.0f)
	{
		return float4(0.0f, 0.0f, 0.0f, 0.0f); // Return fully transparent
	}

    // 1. RGB to HSL
	float3 hslColor = RgbToHsl(originalColor.rgb);

    // 2. Apply Hue Shift only if saturation is high enough (avoids shifting grays)
	if (hslColor.y > 0.01f)
	{
		hslColor.x = fmod(hslColor.x + HueShiftValue + 1.0f, 1.0f); // Add shift and wrap safely
	}

    // 3. HSL back to RGB
	float3 shiftedRgb = HslToRgb(hslColor);

	return float4(shiftedRgb, originalColor.a); // Keep original alpha
}