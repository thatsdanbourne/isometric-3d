using System.Threading.Tasks;
using Godot;

public partial class WorldSync : Node
{
	private World _world;

	public void Init(World world)
	{
		_world = world;
	}

	public void BroadcastWorldTime(double worldTimeSeconds)
	{
		Rpc(nameof(ReceiveWorldTime), worldTimeSeconds);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
		TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void ReceiveWorldTime(double worldTimeSeconds)
	{
		_world.CorrectWorldTime(worldTimeSeconds);
	}

	private Player GetRequestingPlayer()
	{
		var senderId = Multiplayer.GetRemoteSenderId();
		return _world.GetPlayerById(senderId);
	}
}