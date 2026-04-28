using Godot;

public static class NoisePresets
{
	public static NoiseAlgorithm BaseTree(int seed, Vector2I offset)
	{
		return new NoiseAlgorithm(
			seed,
			offset,
			baseNoiseFrequency: 0.4f,
			baseNoiseThreshold: 0.05f,
			baseNoiseOctaves: 2,
			baseNoiseGain: 0.6f,
			baseNoiseLacunarity: 2.5f,
			useDetailNoise: true,
			detailNoiseType: FastNoiseLite.NoiseTypeEnum.Perlin,
			detailNoiseFrequency: 0.05f,
			detailNoiseThreshold: 0.2f,
			detailNoiseOctaves: 4,
			detailNoiseGain: 0.4f,
			detailNoiseLacunarity: 2.0f
		);
	}


	// ---------------------------------------------------------------------
	// UTILITY — Override just the values you want to change
	// ---------------------------------------------------------------------
	public static NoiseAlgorithm Modify(
		NoiseAlgorithm algo,
		float? baseFreq = null,
		float? baseThreshold = null,
		float? detailFreq = null,
		float? detailThreshold = null)
	{
		if (baseFreq != null) algo.BaseNoiseFrequency = baseFreq.Value;
		if (baseThreshold != null) algo.BaseNoiseThreshold = baseThreshold.Value;
		if (detailFreq != null) algo.DetailNoiseFrequency = detailFreq.Value;
		if (detailThreshold != null) algo.DetailNoiseThreshold = detailThreshold.Value;

		return algo;
	}


	// ---------------------------------------------------------------------
	// PLAINS — Light scatter, small clusters, lots of lone trees
	// ---------------------------------------------------------------------
	public static NoiseAlgorithm PlainsTrees(int seed, Vector2I offset)
	{
		var algo = BaseTree(seed, offset);

		return Modify(
			algo,
			0.45f, // slightly smaller clusters than forest
			0.15f, // more trees than forest edges
			0.22f, // more breakup -> more scattered trees
			0.05f
		);
	}


	// ---------------------------------------------------------------------
	// FOREST — Dense, lush, continuous canopy
	// ---------------------------------------------------------------------
	public static NoiseAlgorithm ForestTrees(int seed, Vector2I offset)
	{
		var algo = BaseTree(seed, offset);

		return Modify(
			algo,
			0.35f, // slightly larger clusters
			0.04f, // extremely tree-friendly area
			0.045f,
			0.18f // more dense than plains
		);
	}


	// ---------------------------------------------------------------------
	// TAIGA — Dense but patchy conifer forest
	// ---------------------------------------------------------------------
	public static NoiseAlgorithm TaigaTrees(int seed, Vector2I offset)
	{
		var algo = BaseTree(seed, offset);

		return Modify(
			algo,
			0.3f,
			0.06f,
			0.05f,
			0.22f
		);
	}


	// ---------------------------------------------------------------------
	// TUNDRA — Rare, wind-swept isolated trees
	// ---------------------------------------------------------------------
	public static NoiseAlgorithm TundraTrees(int seed, Vector2I offset)
	{
		var algo = BaseTree(seed, offset);

		return Modify(
			algo,
			0.4f,
			0.17f,
			0.27f,
			0.09f
		);
	}


	// ---------------------------------------------------------------------
	// DESERT — Should usually have no trees, but included for shrubs
	// ---------------------------------------------------------------------
	public static NoiseAlgorithm DesertSparse(int seed, Vector2I offset)
	{
		var algo = BaseTree(seed, offset);

		return Modify(
			algo,
			0.6f,
			0.5f,
			0.07f,
			0.5f
		);
	}


	// ---------------------------------------------------------------------
	// FLOWERS / SMALL DECOR — Gentle scatter
	// ---------------------------------------------------------------------
	public static SpawnAlgorithm LooseStones(int seed, Vector2I offset)
	{
		return new ScatterAlgorithm(
			seed,
			offset,
			FastNoiseLite.NoiseTypeEnum.Perlin,
			0.04f,
			1.7f
		);
	}

	public static SpawnAlgorithm Flowers(int seed, Vector2I offset)
	{
		return new ScatterAlgorithm(
			seed,
			offset,
			FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
			0.04f,
			3.2f,
			3
		);
	}

	public static NoiseAlgorithm Reeds(int seed, Vector2I offset)
	{
		return new NoiseAlgorithm(
			seed,
			offset,
			FastNoiseLite.NoiseTypeEnum.Perlin,
			0.08f,
			0.1f,
			2,
			0.5f,
			2f,
			false
		);
	}

	// ---------------------------------------------------------------------
	// ROCKS / ORE — Small tight clusters
	// ---------------------------------------------------------------------
	public static NoiseAlgorithm OreClusters(int seed, Vector2I offset)
	{
		return new NoiseAlgorithm(
			seed,
			offset,
			baseNoiseFrequency: 0.08f,
			baseNoiseThreshold: 0.6f,
			baseNoiseOctaves: 4,
			baseNoiseGain: 0.45f,
			baseNoiseLacunarity: 2f,
			useDetailNoise: true,
			detailNoiseType: FastNoiseLite.NoiseTypeEnum.Perlin,
			detailNoiseFrequency: 0.25f,
			detailNoiseThreshold: 0.5f,
			detailNoiseOctaves: 2,
			detailNoiseGain: 0.4f,
			detailNoiseLacunarity: 2f
		);
	}
}