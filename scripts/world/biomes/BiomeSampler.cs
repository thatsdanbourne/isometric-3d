using Godot;
using System;

public class BiomeSampler(
	FastNoiseLite tempNoise,
	FastNoiseLite humidityNoise,
	FastNoiseLite riverNoise,
	FastNoiseLite lakeNoise,
	FastNoiseLite drainageNoise,
	FastNoiseLite bankNoise,
	Vector2I worldOffset)
{
	public BiomeSample SampleTile(int globalX, int globalY)
	{
		var temp = tempNoise.GetNoise2D(globalX + worldOffset.X, globalY + worldOffset.Y);
		var humidity = humidityNoise.GetNoise2D(globalX + worldOffset.X, globalY + worldOffset.Y);

		temp = AdjustContrast((temp + 1f) * 0.5f);
		humidity = AdjustContrast((humidity + 1f) * 0.5f);

		var baseBiome = RuleRegistry.GetBiome(temp, humidity);
		var waterFeature = ResolveWaterFeature(globalX, globalY, humidity);
		var finalBiome = waterFeature.BiomeOverride ?? baseBiome;

		return new BiomeSample(temp, humidity, baseBiome, finalBiome, waterFeature);
	}

	public BiomeDefinition GetBaseBiomeAtTile(int globalX, int globalY)
	{
		return SampleTile(globalX, globalY).BaseBiome;
	}

	public BiomeDefinition GetFinalBiomeAtTile(int globalX, int globalY)
	{
		return SampleTile(globalX, globalY).FinalBiome;
	}

	public BiomeDefinition GetBaseBiomeForChunk(Vector2I chunkCoord)
	{
		var tile = TileUtils.GetChunkCenterTile(chunkCoord);
		return GetBaseBiomeAtTile(tile.X, tile.Y);
	}

	public BiomeDefinition GetFinalBiomeForChunk(Vector2I chunkCoord)
	{
		var tile = TileUtils.GetChunkCenterTile(chunkCoord);
		return GetFinalBiomeAtTile(tile.X, tile.Y);
	}

	public Vector2I? FindNearestChunkWithBaseBiome(BiomeId biomeId, Vector2I centerChunk, int maxRadius)
	{
		return FindNearestChunk(centerChunk, maxRadius, chunk =>
			GetBaseBiomeForChunk(chunk).Id == biomeId);
	}

	public Vector2I? FindNearestChunkWithFinalBiome(BiomeId biomeId, Vector2I centerChunk, int maxRadius)
	{
		return FindNearestChunk(centerChunk, maxRadius, chunk =>
			GetFinalBiomeForChunk(chunk).Id == biomeId);
	}

	private static Vector2I? FindNearestChunk(Vector2I centerChunk, int maxRadius, Func<Vector2I, bool> predicate)
	{
		for (var r = 0; r <= maxRadius; r++)
		for (var x = centerChunk.X - r; x <= centerChunk.X + r; x++)
		for (var y = centerChunk.Y - r; y <= centerChunk.Y + r; y++)
		{
			var isEdge =
				x == centerChunk.X - r || x == centerChunk.X + r ||
				y == centerChunk.Y - r || y == centerChunk.Y + r;

			if (!isEdge)
				continue;

			var chunk = new Vector2I(x, y);

			if (predicate(chunk))
				return chunk;
		}

		return null;
	}

	public WaterFeatureResult ResolveWaterFeature(
		int globalX,
		int globalY,
		float humidity)
	{
		float x = globalX + worldOffset.X;
		float y = globalY + worldOffset.Y;

		var biomeAllowsRivers = humidity > 0.45f;

		if (!biomeAllowsRivers)
			return new WaterFeatureResult(WaterFeatureType.None, null);

		var riverRaw = riverNoise.GetNoise2D(x, y);
		var lakeRaw = lakeNoise.GetNoise2D(x, y);
		var drainageRaw = drainageNoise.GetNoise2D(x, y);
		var bankRaw = bankNoise.GetNoise2D(x, y);

		var riverLine = Math.Abs(riverRaw);
		var drainage = (drainageRaw + 1f) * 0.5f;
		var lake = (lakeRaw + 1f) * 0.5f;

		var drainageMask = Mathf.SmoothStep(0.45f, 0.75f, drainage);
		var lakeMask = lake * humidity;

		var isLake = lakeMask > 0.34f;

		var bankJitter = bankRaw * 0.006f;

		var baseRiverWidth = 0.018f;
		// var drainageWidth = drainageMask * 0.018f;
		// var lakeWidthBoost = lakeMask > 0.26 ? 0.02f : 0f;

		var riverWidth = baseRiverWidth + drainageMask * 0.015f + (lakeMask > 0.72f ? 0.02f : 0f) + bankJitter;
		var riverBankWidth = riverWidth + 0.045f;

		var riverAllowed = humidity > 0.45f && drainageMask > 0.15f;

		var isRiver = riverAllowed && riverLine < riverWidth;
		var isRiverBank = riverAllowed && !isLake && riverLine < riverBankWidth;

		if (isLake)
			return new WaterFeatureResult(
				WaterFeatureType.Lake,
				RuleRegistry.GetBiomeById(BiomeId.Lake));


		if (isRiver)
			return new WaterFeatureResult(
				WaterFeatureType.River,
				RuleRegistry.GetBiomeById(BiomeId.River));

		if (isRiverBank)
			return new WaterFeatureResult(
				WaterFeatureType.RiverBank,
				RuleRegistry.GetBiomeById(BiomeId.Riverbank));

		return new WaterFeatureResult(WaterFeatureType.None, null);
	}

	private float AdjustContrast(float v)
	{
		var contrast = 1.4f;
		return Mathf.Clamp((v - 0.5f) * contrast + 0.5f, 0f, 1f);
	}
}

public readonly struct BiomeSample(
	float temperature,
	float humidity,
	BiomeDefinition baseBiome,
	BiomeDefinition finalBiome,
	WaterFeatureResult waterFeature)
{
	public readonly float Temperature = temperature;
	public readonly float Humidity = humidity;
	public readonly BiomeDefinition BaseBiome = baseBiome;
	public readonly BiomeDefinition FinalBiome = finalBiome;
	public readonly WaterFeatureResult WaterFeature = waterFeature;
}