using Godot;

public partial class EntityAnimationController : Node
{
	private const string LocomotionBlendPath = "parameters/Locomotion/blend_position";
	private const string CombatStatePath = "parameters/FullBodyState/transition_request";
	private const string ComboMuxPath = "parameters/ComboMux/transition_request";
	private const string UpperBodyStatePath = "parameters/UpperBodyState/transition_request";
	private const string UpperBodyBlendPath = "parameters/UpperBodyFilter/blend_amount";

	private const string AttackBuffer0 = "AttackBuffer0";
	private const string AttackBuffer1 = "AttackBuffer1";
	private const string AttackBuffer0AnimNode = "DynamicAttack0";
	private const string AttackBuffer1AnimNode = "DynamicAttack1";

	private const string NormalState = "Normal";
	private const string AttackState = "Attack";
	private const string BlockState = "Block";
	private const string ChargingState = "Charging";
	private const string StaggeredState = "Staggered";

	private const string SwingBladeSmallSound = "swing_blade_small";
	private const string SwingFistSound = "swing_fist";
	private const float UpperBodyBlendDuration = 0.2f;

	private CharacterBody3D _owner;
	private AnimationTree _animTree;
	private float _locomotionBlend;
	private float _locomotionBlendTarget;
	private int _attackBufferIndex;
	private Tween _upperBodyTween;


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
		SetUpperBodyBlend(0f);
		SetCombatState(NormalState);
	}

	public void SetUpperBodyBlend(float blend)
	{
		_upperBodyTween?.Kill();
		_upperBodyTween = CreateTween();

		_upperBodyTween.TweenProperty(
				_animTree,
				UpperBodyBlendPath, blend, UpperBodyBlendDuration)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
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
		PlayBufferedAttack(GetLightAttackAnimation(tool, comboIndex), GetSwingSound(tool));
	}

	public void PlayChargeStart(ToolItem tool)
	{
		SetCombatState(ChargingState);
	}

	public void PlayChargedRelease(ToolItem tool)
	{
		PlayBufferedAttack(GetChargedReleaseAnimation(tool), GetSwingSound(tool));
	}

	private static string GetLightAttackAnimation(ToolItem tool, int comboIndex)
	{
		return tool.ToolType switch
		{
			"sword" => $"attack_axe_light_{comboIndex + 1}",
			"axe" => $"attack_axe_light_{comboIndex + 1}",
			_ => "attack_fist"
		};
	}

	private static string GetChargedReleaseAnimation(ToolItem tool)
	{
		return tool.ToolType switch
		{
			"sword" => "attack_sword_charge_release",
			"axe" => "attack_sword_charge_release",
			_ => "attack_fist"
		};
	}

	private static string GetSwingSound(ToolItem tool)
	{
		return tool.ToolType is "axe" or "sword" ? SwingBladeSmallSound : SwingFistSound;
	}

	private void PlayBufferedAttack(string animation, string soundKey)
	{
		var useFirstBuffer = _attackBufferIndex == 0;

		if (!TryGetAttackBuffer(useFirstBuffer, out var bufferName, out var buffer, out var animNodeName))
			return;

		if (buffer.GetNode(animNodeName) is not AnimationNodeAnimation dynamicAnim)
			return;

		dynamicAnim.Animation = animation;
		_attackBufferIndex = useFirstBuffer ? 1 : 0;

		PlayAttackBuffer(bufferName);

		AudioManager.Instance.PlayVariantAt(soundKey, _owner.GlobalPosition, AudioManager.BusTools, 0.1f);
	}

	private bool TryGetAttackBuffer(bool useFirstBuffer, out string bufferName, out AnimationNodeBlendTree buffer,
		out string animNodeName)
	{
		bufferName = useFirstBuffer ? AttackBuffer0 : AttackBuffer1;
		animNodeName = useFirstBuffer ? AttackBuffer0AnimNode : AttackBuffer1AnimNode;
		buffer = null;

		if (_animTree.TreeRoot is not AnimationNodeBlendTree treeRoot)
			return false;

		buffer = treeRoot.GetNode(bufferName) as AnimationNodeBlendTree;
		return buffer != null;
	}

	private void PlayAttackBuffer(string bufferName)
	{
		SetCombatState(NormalState);
		_animTree.Set(ComboMuxPath, bufferName);
		_animTree.Set(UpperBodyStatePath, AttackState);
		SetUpperBodyBlend(1f);
	}

	public void PlayStagger()
	{
		SetCombatState(StaggeredState);
	}

	public void PlayBlockStart()
	{
		_animTree.Set(UpperBodyStatePath, BlockState);
		SetUpperBodyBlend(1f);
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

	private void SetCombatState(string state)
	{
		_animTree.Set(CombatStatePath, state);
	}
}
