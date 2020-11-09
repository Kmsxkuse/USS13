// ReSharper disable CppInconsistentNaming

// Fuck C++ naming. C# all the way, wooooo.

// Returns the cardinal direction grid data by passing two checks:
//     Direction is not out of bounds.
//     Direction does not contain a solid vector.
// Else, returns fail check value.
// Data outputted: NORTH EAST SOUTH WEST
float4x4 GetCardinal(float2 uv, float2 inverseSize, sampler2D obstacles, sampler2D success, float4 fail)
{
    float2 north = uv + float2(0, inverseSize.y);
    float2 west = uv + float2(-inverseSize.x, 0);
    float2 east = uv + float2(inverseSize.x, 0);
    float2 south = uv + float2(0, -inverseSize.y);

    // Bounds check not needed for tex2D with clamped textures. Woo.
    float4 dataNorth = tex2D(obstacles, north).x <= 0.0
        ? tex2D(success, north) : fail;
    float4 dataWest = tex2D(obstacles, west).x <= 0.0
        ? tex2D(success, west) : fail;
    float4 dataEast = tex2D(obstacles, east).x <= 0.0
        ? tex2D(success, east) : fail;
    float4 dataSouth = tex2D(obstacles, south).x <= 0.0
        ? tex2D(success, south) : fail;

    return float4x4(dataNorth, dataEast, dataSouth, dataWest);
}