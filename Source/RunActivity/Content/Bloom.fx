

// Bloom filter by Kosmonaut3d

////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  VARIABLES
////////////////////////////////////////////////////////////////////////////////////////////////////////////

//Needed for pixel offset
float2 InverseResolution;

//The threshold of pixels that are brighter than that.
float Threshold = 0.9f;

//MODIFIED DURING RUNTIME, CHANGING HERE MAKES NO DIFFERENCE;
float Radius;
float Strength;

//How far we stretch the pixels
float StreakLength = 1;

// Input texture
Texture2D ScreenTexture;

SamplerState LinearSampler
{
	Texture = <ScreenTexture>;

	MagFilter = LINEAR;
	MinFilter = LINEAR;
	Mipfilter = LINEAR;

	AddressU = CLAMP;
	AddressV = CLAMP;
};

// Variables for the merge
const float BloomSaturation = 1;
const float BloomIntensity = 1;
const float BaseSaturation = 1;
const float BaseIntensity = 1;

Texture2D BloomTexture;

SamplerState BloomSampler
{
    Texture = <BloomTexture>;

    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;

    AddressU = CLAMP;
    AddressV = CLAMP;
};

////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  STRUCTS
////////////////////////////////////////////////////////////////////////////////////////////////////////////

struct VertexShaderInput
{
	float3 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
}; 

////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  FUNCTIONS
////////////////////////////////////////////////////////////////////////////////////////////////////////////

	////////////////////////////////////////////////////////////////////////////////////////////////////////////
	//  VERTEX SHADER
	////////////////////////////////////////////////////////////////////////////////////////////////////////////

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
	VertexShaderOutput output;
	output.Position = float4(input.Position, 1);
	output.TexCoord = input.TexCoord;
	return output;
}
////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	//  PIXEL SHADER
	////////////////////////////////////////////////////////////////////////////////////////////////////////////

//Just an average of 4 values.
float4 Box4(float4 p0, float4 p1, float4 p2, float4 p3)
{
	return (p0 + p1 + p2 + p3) * 0.25f;
}

//Extracts the pixels we want to blur
float4 ExtractPS(float4 pos : SV_POSITION,  float2 texCoord : TEXCOORD0) : SV_TARGET0
{
    float4 color = ScreenTexture.Sample(LinearSampler, texCoord);

    float avg = (color.r + color.g + color.b) / 3;

    if (avg > Threshold)
	{
		return color * (avg - Threshold) / (1 - Threshold);
	}

	return float4(0, 0, 0, 0);
}

//Extracts the pixels we want to blur, but considers luminance instead of average rgb
float4 ExtractLuminancePS(float4 pos : SV_POSITION,  float2 texCoord : TEXCOORD0) : SV_TARGET0
{
    float4 color = ScreenTexture.Sample(LinearSampler, texCoord);

    const float exposure = 1.;
    float brightness = clamp(dot(color.rgb * exposure, float3(0.2126, 0.7152, 0.0722)), 0, 1);
    return step(Threshold, brightness) * color;

    float luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;

    if(luminance > Threshold)
    {
		return color * (luminance - Threshold) / (1 - Threshold);
        //return saturate((color - Threshold) / (1 - Threshold));
    }

    return float4(0, 0, 0, 0);
}

//Downsample to the next mip, blur in the process
float4 DownsamplePS(float4 pos : SV_POSITION,  float2 texCoord : TEXCOORD0) : SV_TARGET0
{
    float2 offset = float2(StreakLength * InverseResolution.x, 1 * InverseResolution.y);
        
    float4 c0 = ScreenTexture.Sample(LinearSampler, texCoord + float2(-2, -2) * offset);
    float4 c1 = ScreenTexture.Sample(LinearSampler, texCoord + float2(0, -2) * offset);
    float4 c2 = ScreenTexture.Sample(LinearSampler, texCoord + float2(2, -2) * offset);
    float4 c3 = ScreenTexture.Sample(LinearSampler, texCoord + float2(-1, -1) * offset);
    float4 c4 = ScreenTexture.Sample(LinearSampler, texCoord + float2(1, -1) * offset);
    float4 c5 = ScreenTexture.Sample(LinearSampler, texCoord + float2(-2, 0) * offset);
    float4 c6 = ScreenTexture.Sample(LinearSampler, texCoord);
    float4 c7 = ScreenTexture.Sample(LinearSampler, texCoord + float2(2, 0) * offset);
    float4 c8 = ScreenTexture.Sample(LinearSampler, texCoord + float2(-1, 1) * offset);
    float4 c9 = ScreenTexture.Sample(LinearSampler, texCoord + float2(1, 1) * offset);
    float4 c10 = ScreenTexture.Sample(LinearSampler, texCoord + float2(-2, 2) * offset);
    float4 c11 = ScreenTexture.Sample(LinearSampler, texCoord + float2(0, 2) * offset);
    float4 c12 = ScreenTexture.Sample(LinearSampler, texCoord + float2(2, 2) * offset);

    return Box4(c0, c1, c5, c6) * 0.125f +
    Box4(c1, c2, c6, c7) * 0.125f +
    Box4(c5, c6, c10, c11) * 0.125f +
    Box4(c6, c7, c11, c12) * 0.125f +
    Box4(c3, c4, c8, c9) * 0.5f;
}

//Upsample to the former MIP, blur in the process
float4 UpsamplePS(float4 pos : SV_POSITION,  float2 texCoord : TEXCOORD0) : SV_TARGET0
{
    float2 offset = float2(StreakLength * InverseResolution.x, 1 * InverseResolution.y) * Radius;

    float4 c0 = ScreenTexture.Sample(LinearSampler, texCoord + float2(-1, -1) * offset);
    float4 c1 = ScreenTexture.Sample(LinearSampler, texCoord + float2(0, -1) * offset);
    float4 c2 = ScreenTexture.Sample(LinearSampler, texCoord + float2(1, -1) * offset);
    float4 c3 = ScreenTexture.Sample(LinearSampler, texCoord + float2(-1, 0) * offset);
    float4 c4 = ScreenTexture.Sample(LinearSampler, texCoord);
    float4 c5 = ScreenTexture.Sample(LinearSampler, texCoord + float2(1, 0) * offset);
    float4 c6 = ScreenTexture.Sample(LinearSampler, texCoord + float2(-1,1) * offset);
    float4 c7 = ScreenTexture.Sample(LinearSampler, texCoord + float2(0, 1) * offset);
    float4 c8 = ScreenTexture.Sample(LinearSampler, texCoord + float2(1, 1) * offset);

    //Tentfilter  0.0625f    
    return 0.0625f * (c0 + 2 * c1 + c2 + 2 * c3 + 4 * c4 + 2 * c5 + c6 + 2 * c7 + c8) * Strength + float4(0, 0,0,0); //+ 0.5f * ScreenTexture.Sample(c_texture, texCoord);

}

//Upsample to the former MIP, blur in the process, change offset depending on luminance
float4 UpsampleLuminancePS(float4 pos : SV_POSITION,  float2 texCoord : TEXCOORD0) : SV_TARGET0
{
    float4 c4 = ScreenTexture.Sample(LinearSampler, texCoord);  //middle one
 
    /*float luminance = c4.r * 0.21f + c4.g * 0.72f + c4.b * 0.07f;
    luminance = max(luminance, 0.4f);
*/
	float2 offset = float2(StreakLength * InverseResolution.x, 1 * InverseResolution.y) * Radius; /// luminance;

    float4 c0 = ScreenTexture.Sample(LinearSampler, texCoord + float2(-1, -1) * offset);
    float4 c1 = ScreenTexture.Sample(LinearSampler, texCoord + float2(0, -1) * offset);
    float4 c2 = ScreenTexture.Sample(LinearSampler, texCoord + float2(1, -1) * offset);
    float4 c3 = ScreenTexture.Sample(LinearSampler, texCoord + float2(-1, 0) * offset);
    float4 c5 = ScreenTexture.Sample(LinearSampler, texCoord + float2(1, 0) * offset);
    float4 c6 = ScreenTexture.Sample(LinearSampler, texCoord + float2(-1, 1) * offset);
    float4 c7 = ScreenTexture.Sample(LinearSampler, texCoord + float2(0, 1) * offset);
    float4 c8 = ScreenTexture.Sample(LinearSampler, texCoord + float2(1, 1) * offset);
 
    return 0.0625f * (c0 + 2 * c1 + c2 + 2 * c3 + 4 * c4 + 2 * c5 + c6 + 2 * c7 + c8) * Strength + float4(0, 0, 0, 0); //+ 0.5f * ScreenTexture.Sample(c_texture, texCoord);

}

float4 AdjustSaturation(float4 color, float saturation)
{
    // The constants 0.3, 0.59, and 0.11 are chosen because the 
    // human eye is more sensitive to green light, and less to blue. 
    float grey = dot(color, float3(0.3, 0.59, 0.11));
    return lerp(grey, color, saturation);
}

float4 MergePS(float4 pos : SV_POSITION, float2 texCoord : TEXCOORD0) : SV_TARGET0
{
    float4 base = ScreenTexture.Sample(LinearSampler, texCoord);
    float4 bloom = BloomTexture.Sample(LinearSampler, texCoord);

    //base = AdjustSaturation(base, BaseSaturation) * BaseIntensity;
    //bloom = AdjustSaturation(bloom, BloomSaturation) * BloomIntensity;

    return base + bloom;
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  TECHNIQUES
////////////////////////////////////////////////////////////////////////////////////////////////////////////

technique Extract
{
	pass Pass1
	{
		VertexShader = compile vs_4_0 VertexShaderFunction();
		PixelShader = compile ps_4_0 ExtractPS();
	}
}

technique ExtractLuminance
{
	pass Pass1
	{
		VertexShader = compile vs_4_0 VertexShaderFunction();
		PixelShader = compile ps_4_0 ExtractLuminancePS();
	}
}

technique Downsample
{
    pass Pass1
    {
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 DownsamplePS();
    }
}

technique Upsample
{
    pass Pass1
    {
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 UpsamplePS();
    }
}

technique UpsampleLuminance
{
    pass Pass1
    {
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 UpsampleLuminancePS();
    }
}

technique Merge
{
    pass Pass1
    {
        VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 MergePS();
    }
}
