using Godot;

public enum FacingDir
{
    N, NE, E, SE, S, SW, W, NW
}

public static class AnimationManager
{

    public static FacingDir GetFacingFromInput(Vector2 input)
    {
        float degrees = Mathf.RadToDeg(Mathf.Atan2(input.X, input.Y));

        if (degrees < 0)
            degrees += 360;

        if (degrees >= 337.5f || degrees < 22.5f) return FacingDir.S;
        if (degrees < 67.5f) return FacingDir.SE;
        if (degrees < 112.5f) return FacingDir.E;
        if (degrees < 157.5f) return FacingDir.NE;
        if (degrees < 202.5f) return FacingDir.N;
        if (degrees < 247.5f) return FacingDir.NW;
        if (degrees < 292.5f) return FacingDir.W;
        return FacingDir.SW;
    }
}
