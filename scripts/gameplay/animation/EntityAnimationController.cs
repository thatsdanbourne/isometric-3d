using Godot;

public partial class EntityAnimationController : Node
{
	private const string LocomotionBlendPath = "parameters/Locomotion/blend_position";

	private CharacterBody3D _owner;
	private AnimationTree _animTree;
	private float _locomotionBlend;
	private float _locomotionBlendTarget;
	private string _animState = "";
	private int _attackBufferIndex;

	private const string CombatStatePath = "parameters/CombatState/transition_request";
	private const string AttackBuffer0AnimNode = "DynamicAttack0";
	private const string AttackBuffer1AnimNode = "DynamicAttack1";
	private const string AttackBuffer0Timescale = "parameters/AttackBuffer0/TimeScale/scale";
	private const string AttackBuffer1Timescale = "parameters/AttackBuffer1/TimeScale/scale";
	private const string SwingBladeSmallSound = "swing_blade_small";
	private const string SwingFistSound = "swing_fist";


	public void Init(CharacterBody3D owner, AnimationTree animTree)
	{
		_owner = owner;
		_animTree = animTree;
	}

	public void SetLocomotionState(bool isMoving)
	{
		SetLocomotionBlend(isMoving ? 1f : 0f);
	}

	public void SetLocomotionBlend(float blend)
	{
		_locomotionBlendTarget = Mathf.Clamp(blend, 0f, 1f);
		_animState = _locomotionBlendTarget > 0.01f ? "run" : "idle";
	}

	public void Tick(float blendAlpha)
	{
		_locomotionBlend = Mathf.Lerp(_locomotionBlend, _locomotionBlendTarget, blendAlpha);
		_animTree.Set(LocomotionBlendPath, _locomotionBlend);
	}

	public void FaceDirection(Vector3 dir)
	{
		if (dir.LengthSquared() < 0.001f)
			return;

		var targetAngle = Mathf.Atan2(dir.X, dir.Z);
		_owner.Rotation = new Vector3(
			_owner.Rotation.X,
			targetAngle,
			_owner.Rotation.Z
		);
	}

	public void ReturnToIdle()
	{
		_animTree.Set(AttackBuffer0Timescale, 1f);
		_animTree.Set(AttackBuffer1Timescale, 1f);
		_animTree.Set(CombatStatePath, "Normal");
	}

	public void HoldCurrentAttackPose()
	{
		var timescalePath = _attackBufferIndex == 0 ? AttackBuffer1Timescale : AttackBuffer0Timescale;

		_animTree.Set(timescalePath, 0f);
	}

	public void PlayUseTool(ToolItem tool, Vector3 swingDir, bool isCharged, int comboIndex = 0)
	{
		if (isCharged)
			PlayChargedRelease(tool);
		else
			PlayLightAttack(tool, comboIndex);

		FaceDirection(swingDir);
	}

	public void PlayLightAttack(ToolItem tool, int comboIndex)
	{
		var animation = tool.ToolType switch
		{
			"sword" => $"attack_axe_light_{comboIndex + 1}",
			"axe" => $"attack_axe_light_{comboIndex + 1}",
			_ => "attack_fist"
		};

		PlayBufferedAttack(animation, tool.ToolType is "axe" or "sword" ? SwingBladeSmallSound : SwingFistSound);
	}

	public void PlayChargeStart(ToolItem tool)
	{
		_animTree.Set(CombatStatePath, "Charging");
	}

	public void PlayChargedRelease(ToolItem tool)
	{
		PlayBufferedAttack("attack_sword_charge_release", SwingBladeSmallSound);
	}

	private void PlayBufferedAttack(string animation, string soundKey)
	{
		var useA = _attackBufferIndex == 0;
		_attackBufferIndex = useA ? 1 : 0;

		var treeRoot = (AnimationNodeBlendTree)_animTree.TreeRoot;

		var animNodeName = useA ? AttackBuffer0AnimNode : AttackBuffer1AnimNode;
		var timescalePath = useA ? AttackBuffer0Timescale : AttackBuffer1Timescale;
		var stateName = useA ? "AttackBuffer0" : "AttackBuffer1";

		if (treeRoot.GetNode(stateName) is not AnimationNodeBlendTree buffer)
			return;

		if (buffer.GetNode(animNodeName) is not AnimationNodeAnimation dynamicAnim)
			return;

		dynamicAnim.Animation = animation;

		_animTree.Set(timescalePath, 1f);
		_animTree.Set(CombatStatePath, stateName);

		AudioManager.Instance.PlayVariantAt(soundKey, _owner.GlobalPosition, AudioManager.BusTools, 0.1f);
	}

	public void PlayStagger()
	{
		_animTree.Set(CombatStatePath, "Staggered");
	}

	public void PlayBlockStart()
	{
		_animTree.Set(CombatStatePath, "Blocking");
	}

	public void PlayCombatAnim(PlayerCombatAnimEvent animEvent, ToolItem tool, Vector3 swingDir, int comboIndex = 0)
	{
		switch (animEvent)
		{
			case PlayerCombatAnimEvent.LightAttack:
				PlayUseTool(tool, swingDir, false, comboIndex);
				break;
			case PlayerCombatAnimEvent.ChargeStart:
				PlayChargeStart(tool);
				break;
			case PlayerCombatAnimEvent.ChargeCancel:
				ReturnToIdle();
				break;
			case PlayerCombatAnimEvent.ChargeRelease:
				PlayUseTool(tool, swingDir, true);
				break;
			case PlayerCombatAnimEvent.BlockStart:
				PlayBlockStart();
				break;
			case PlayerCombatAnimEvent.BlockEnd:
				ReturnToIdle();
				break;
			default:
				ReturnToIdle();
				break;
		}
	}
}