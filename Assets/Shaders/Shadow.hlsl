#ifndef SHADOW_INCLUDED
#define SHADOW_INCLUDED

half SampleShadow_GetTriangleTexelArea(half triangleHeight)
{
	return triangleHeight - 0.5;
}

void SampleShadow_GetTexelAreas_Tent_3x3(half offset, out half4 computedArea, out half4 computedAreaUncut)
{
    // Compute the exterior areas
	half offset01SquaredHalved = (offset + 0.5) * (offset + 0.5) * 0.5;
	computedAreaUncut.x = computedArea.x = offset01SquaredHalved - offset;
	computedAreaUncut.w = computedArea.w = offset01SquaredHalved;

    // Compute the middle areas
    // For Y : We find the area in Y of as if the left section of the isoceles triangle would
    // intersect the axis between Y and Z (ie where offset = 0).
	computedAreaUncut.y = SampleShadow_GetTriangleTexelArea(1.5 - offset);
    // This area is superior to the one we are looking for if (offset < 0) thus we need to
    // subtract the area of the triangle defined by (0,1.5-offset), (0,1.5+offset), (-offset,1.5).
	half clampedOffsetLeft = min(offset, 0);
	half areaOfSmallLeftTriangle = clampedOffsetLeft * clampedOffsetLeft;
	computedArea.y = computedAreaUncut.y - areaOfSmallLeftTriangle;

    // We do the same for the Z but with the right part of the isoceles triangle
	computedAreaUncut.z = SampleShadow_GetTriangleTexelArea(1.5 + offset);
	half clampedOffsetRight = max(offset, 0);
	half areaOfSmallRightTriangle = clampedOffsetRight * clampedOffsetRight;
	computedArea.z = computedAreaUncut.z - areaOfSmallRightTriangle;
}

void SampleShadow_GetTexelWeights_Tent_3x3(half offset, out half4 computedWeight)
{
	half4 dummy;
	SampleShadow_GetTexelAreas_Tent_3x3(offset, computedWeight, dummy);
	computedWeight *= 0.44444; //0.44 == 1/(the triangle area)
}

// 3x3 Tent filter (45 degree sloped triangles in U and V)
void SampleShadow_ComputeSamples_Tent_3x3(half4 shadowMapTexture_TexelSize, float2 coord, out half fetchesWeights[4], out half2 fetchesUV[4])
{
    // tent base is 3x3 base thus covering from 9 to 12 texels, thus we need 4 bilinear PCF fetches
	half2 tentCenterInTexelSpace = coord.xy * shadowMapTexture_TexelSize.zw;
	half2 centerOfFetchesInTexelSpace = floor(tentCenterInTexelSpace + 0.5);
	half2 offsetFromTentCenterToCenterOfFetches = tentCenterInTexelSpace - centerOfFetchesInTexelSpace;

    // find the weight of each texel based
	half4 texelsWeightsU, texelsWeightsV;
	SampleShadow_GetTexelWeights_Tent_3x3(offsetFromTentCenterToCenterOfFetches.x, texelsWeightsU);
	SampleShadow_GetTexelWeights_Tent_3x3(offsetFromTentCenterToCenterOfFetches.y, texelsWeightsV);

    // each fetch will cover a group of 2x2 texels, the weight of each group is the sum of the weights of the texels
	half2 fetchesWeightsU = texelsWeightsU.xz + texelsWeightsU.yw;
	half2 fetchesWeightsV = texelsWeightsV.xz + texelsWeightsV.yw;

    // move the PCF bilinear fetches to respect texels weights
	half2 fetchesOffsetsU = texelsWeightsU.yw / fetchesWeightsU.xy + half2(-1.5, 0.5);
	half2 fetchesOffsetsV = texelsWeightsV.yw / fetchesWeightsV.xy + half2(-1.5, 0.5);
	fetchesOffsetsU *= shadowMapTexture_TexelSize.xx;
	fetchesOffsetsV *= shadowMapTexture_TexelSize.yy;

	half2 bilinearFetchOrigin = centerOfFetchesInTexelSpace * shadowMapTexture_TexelSize.xy;
	fetchesUV[0] = bilinearFetchOrigin + half2(fetchesOffsetsU.x, fetchesOffsetsV.x);
	fetchesUV[1] = bilinearFetchOrigin + half2(fetchesOffsetsU.y, fetchesOffsetsV.x);
	fetchesUV[2] = bilinearFetchOrigin + half2(fetchesOffsetsU.x, fetchesOffsetsV.y);
	fetchesUV[3] = bilinearFetchOrigin + half2(fetchesOffsetsU.y, fetchesOffsetsV.y);

	fetchesWeights[0] = fetchesWeightsU.x * fetchesWeightsV.x;
	fetchesWeights[1] = fetchesWeightsU.y * fetchesWeightsV.x;
	fetchesWeights[2] = fetchesWeightsU.x * fetchesWeightsV.y;
	fetchesWeights[3] = fetchesWeightsU.y * fetchesWeightsV.y;
}

#endif