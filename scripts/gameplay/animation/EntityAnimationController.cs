using Godot;

public partial class EntityAnimationController : Node
{
	private const string LocomotionBlendPath = "parameters/Locomotion/blend_position";

	private CharacterBody3D _owner;
	private AnimationTree _animTree;
	private float _locomotionBlend;
	private float _locomotionBlendTarget;
	private string _animState = "";

	public void Init(CharacterBody3D owner, AnimationTree animTree)
	{
		_owner = owner;
		_animTree = animTree;
	}

	public void SetLocomotionState(bool isMoving)
	{
		var name = isMoving ? "run" : "idle";
		if (_animState == name)
			return;

		_animState = name;
		_locomotionBlendTarget = name == "run" ? 1f : 0f;
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

	public void PlayUseTool(ToolItem tool, Vector3 swingDir, bool isCharged)
	{
		if (isCharged)
			PlayChargedRelease(tool);
		else
			PlayLightAttack(tool);

		FaceDirection(swingDir);
	}

	public void PlayLightAttack(ToolItem tool)
	{
		var treeRoot = (AnimationNodeBlendTree)_animTree.TreeRoot;

		if (treeRoot.GetNode("DynamicAttack") is AnimationNodeAnimation dynamicAnim)
		{
			dynamicAnim.Animation = tool.ToolType switch
			{
				"sword" => "attack_axe",
				"axe" => "attack_axe",
				_ => "attack_fist"
			};

			_animTree.Set("parameters/LightAttackOS/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}

		AudioManager.Instance.PlayVariantAt(tool.ToolType is "axe" or "sword" ? "swing_blade_small" : "swing_fist",
			_owner.GlobalPosition, AudioManager.BusTools, 0.1f);
	}

	public void PlayChargeStart(ToolItem tool)
	{
		_animTree.Set("parameters/CombatState/transition_request", "Charging");
	}

	public void PlayChargedRelease(ToolItem tool)
	{
		var treeRoot = (AnimationNodeBlendTree)_animTree.TreeRoot;

		if (treeRoot.GetNode("DynamicRelease") is AnimationNodeAnimation dynamicAnim)
		{
			dynamicAnim.Animation = tool.ToolType switch
			{
				"sword" => "attack_sword_charge_release",
				"axe" => "attack_axe_charge_release",
				_ => "attack_sword_charge_release"
			};

			_animTree.Set("parameters/HeavyAttackOS/request",
				(int)AnimationNodeOneShot.OneShotRequest.Fire);
		}

		AudioManager.Instance.PlayVariantAt("swing_blade_small", _owner.GlobalPosition, AudioManager.BusTools, 0.1f);
	}

	public void CancelCharge()
	{
		_animTree.Set("parameters/CombatState/transition_request", "Normal");
	}

	public void PlayCombatAnim(PlayerCombatAnimEvent animEvent, ToolItem tool, Vector3 swingDir)
	{
		switch (animEvent)
		{
			case PlayerCombatAnimEvent.LightAttack:
				PlayUseTool(tool, swingDir, false);
				break;
			case PlayerCombatAnimEvent.ChargeStart:
				PlayChargeStart(tool);
				break;
			case PlayerCombatAnimEvent.ChargeCancel:
				CancelCharge();
				break;
			case PlayerCombatAnimEvent.ChargeRelease:
				PlayUseTool(tool, swingDir, true);
				break;
		}
	}

	public bool IsHeavyAttackActive()
	{
		return (bool)_animTree.Get("parameters/HeavyAttackOS/active");
	}

	public void ReturnToIdle()
	{
		_animTree.Set("parameters/CombatState/transition_request", "Normal");
	}
}