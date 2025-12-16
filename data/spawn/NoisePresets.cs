using Godot;

public static class NoisePresets
{
    public static NoiseAlgorithm BaseTree(int seed, Vector2I offset)
    {
        return new NoiseAlgorithm(
            seed,
            offset,

            // BASE NOISE (broad placement)
            FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            baseNoiseFrequency: 0.4f,
            baseNoiseThreshold: 0.05f,
            baseNoiseOctaves: 2,
            baseNoiseGain: 0.6f,
            baseNoiseLacunarity: 2.5f,

            // DETAIL NOISE (cluster breakup)
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
            baseFreq: 0.45f,          // slightly smaller clusters than forest
            baseThreshold: 0.15f,    // more trees than forest edges
            detailFreq: 0.22f,       // more breakup -> more scattered trees
            detailThreshold: 0.05f
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
            baseFreq: 0.35f,         // slightly larger clusters
            baseThreshold: 0.04f,    // extremely tree-friendly area
            detailFreq: 0.045f,
            detailThreshold: 0.18f   // more dense than plains
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
            baseFreq: 0.3f,
            baseThreshold: 0.06f,
            detailFreq: 0.05f,
            detailThreshold: 0.22f
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
            baseFreq: 0.4f,
            baseThreshold: 0.17f,    // high threshold -> very few trees
            detailFreq: 0.27f,
            detailThreshold: 0.09f
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
            baseFreq: 0.6f,
            baseThreshold: 0.5f,     // extremely restrictive
            detailFreq: 0.07f,
            detailThreshold: 0.5f
        );
    }


    // ---------------------------------------------------------------------
    // FLOWERS / SMALL DECOR — Gentle scatter
    // ---------------------------------------------------------------------
    public static NoiseAlgorithm Flowers(int seed, Vector2I offset)
    {
        return new NoiseAlgorithm(
            seed,
            offset,
            FastNoiseLite.NoiseTypeEnum.Perlin,
            baseNoiseFrequency: 0.12f,
            baseNoiseThreshold: 0.3f,
            baseNoiseOctaves: 2,
            baseNoiseGain: 0.45f,
            baseNoiseLacunarity: 2f,
            useDetailNoise: false
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
            FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
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