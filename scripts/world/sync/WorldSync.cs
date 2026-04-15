using System.Threading.Tasks;
using Godot;

public partial class WorldSync : Node
{
	private World _world;

	public void Init(World world)
	{
		_world = world;
	}

	private Player GetRequestingPlayer()
	{
		var senderId = Multiplayer.GetRemoteSenderId();
		return _world.GetPlayerById(senderId);
	}
}