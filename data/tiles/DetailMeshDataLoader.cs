using Godot;

public partial class DetailMeshDataLoader
{
    public static void LoadAllDetailMeshes()
    {
        DetailMeshRegistry.Register(new DetailMeshDefinition
        {
            Id = "grass",
            mesh = GD.Load<Mesh>("res://assets/meshes/GrassQuad.tres"),
            material = GD.Load<Material>("res://resources/materials/DetailMaterial.tres"),
        });
    }
}
