#ifndef TINT_INCLUDED
#define TINT_INCLUDED

// common includes for tinted KSP replacement shaders
// includes colourspace conversion functions

// common variables
float _TintHue;
float _TintSat;
float _TintVal;
float3 _TintCol;
float _TintPoint;
float _TintBand;
float _TintFalloff;
float _TintSatThreshold;
float _SaturationWindow;
float _SaturationFalloff;
float _GlossMult;


// colourspace routines

		float Epsilon = 1e-10;

		float3 RGBtoHCV(in float3 RGB)
		{
			// Based on work by Sam Hocevar and Emil Persson
			float4 P = (RGB.g < RGB.b) ? float4(RGB.bg, -1.0, 2.0 / 3.0) : float4(RGB.gb, 0.0, -1.0 / 3.0);
			float4 Q = (RGB.r < P.x) ? float4(P.xyw, RGB.r) : float4(RGB.r, P.yzx);
			float C = Q.x - min(Q.w, Q.y);
			float H = abs((Q.w - Q.y) / (6 * C + Epsilon) + Q.z);
			return float3(H, C, Q.x);
		}

		float3 HUEtoRGB(in float H)
		{
			float R = abs(H * 6 - 3) - 1;
			float G = 2 - abs(H * 6 - 2);
			float B = 2 - abs(H * 6 - 4);
			return saturate(float3(R, G, B));
		}

		float3 HSVtoRGB(in float3 HSV)
		{
			float3 RGB = HUEtoRGB(HSV.x);
			return ((RGB - 1) * HSV.y + 1) * HSV.z;
		}

		float3 RGBtoHSV(in float3 RGB)
		{
			float3 HCV = RGBtoHCV(RGB);
			float S = HCV.y / (HCV.z + Epsilon);
			return float3(HCV.x, S, HCV.z);
		}

		inline fixed4 LightingNormalizedBlinnPhong(SurfaceOutput s, half3 lightDir, half3 viewDir, half atn)
		{
			fixed3 normalizedSurfNormal = normalize(s.Normal);
			fixed3 halfDir = normalize(lightDir + viewDir);

			fixed diff = max(0, dot(normalizedSurfNormal, lightDir));

			fixed nh = max(0, dot(normalizedSurfNormal, halfDir));
			fixed spec = pow(nh, s.Specular * 128) * s.Gloss;

			fixed4 c;
			c.rgb = (_LightColor0.rgb * ((s.Albedo * diff) + (spec *_SpecColor.rgb))) * atn;
			c.a = s.Alpha + _LightColor0.a * _SpecColor.a * spec * atn;
			return c;
		}

	//	inline fixed4 LightingCookTorrance(SurfaceOutput s, half3 lightDir, half3 viewDir)
	//	{
	//		fixed3 halfDir = normalize(lightDir + viewDir);
	//		fixed3 nN = normalize(s.Normal);
	//		fixed3 halfDotnN2 = max(0, dot(halfDir, nN)) * 2;
	//		fixed3 viewDotHalf = max( 0, dot(viewDir, halfDir));

	//		fixed atn = min(1, min((halfDotnN2 *dot(viewDir, nN)) / viewDotHalf, halfDotnN2* dot(lightDir, nN) / viewDotHalf));
	//		fixed4 c;
	////		c.rgb = (_LightColor0.rgb * ((s.Albedo * diff) + (spec *_SpecColor.rgb))) * atn;
	////		c.a = s.Alpha + _LightColor0.a * _SpecColor.a * spec * atn;
	//		return c;

	//	}

// General function to get blending value
// needs refining/refactoring, but works. Not sure why it needs so many saturates, didn't when I was working it out...
		// replace the function with a macro when satisfied

		inline float BlendFactor(in float3 Colour)
		{
			float3 asHSV = RGBtoHSV(Colour.rgb);

			return saturate((1 - (saturate(abs(asHSV.z - _TintPoint) - _TintBand) / _TintFalloff)) *
				(1 - saturate((asHSV.y - _SaturationFalloff) / _SaturationWindow))); // two divisions? do some better maths
		}
#endif