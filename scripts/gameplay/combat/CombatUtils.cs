using Godot;
using System.Collections.Generic;

public static class CombatUtils
{
	public static IEnumerable<Vector3> GetHitArcDirections(Vector3 centerDir, float arcDegrees, int rayCount)
	{
		var halfArc = arcDegrees * 0.5f;
		var step = rayCount > 1 ? arcDegrees / (rayCount - 1) : 0f;

		for (var i = 0; i < rayCount; i++)
		{
			var angle = -halfArc + step * i;
			yield return centerDir.Rotated(Vector3.Up, Mathf.DegToRad(angle)).Normalized();
		}
	}

	public static IToolHittable FindToolHittable(Node node)
	{
		for (var n = node; n != null; n = n.GetParent())
			if (n is IToolHittable h)
				return h;

		return null;
	}

	public static ToolHitResult PerformMeleeHit(Node3D attacker, ToolItem tool, Vector3 swingDir,
		PhysicsDirectSpaceState3D space, PhysicsRayQueryParameters3D query, Node3D requiredTarget = null)
	{
		if (swingDir.LengthSquared() < 0.001f) return ToolHitResult.None;

		swingDir.Y = 0f;
		swingDir = swingDir.Normalized();

		var from = new Vector3(attacker.GlobalPosition.X, attacker.GlobalPosition.Y + 0.1f, attacker.GlobalPosition.Z);

		foreach (var dir in GetHitArcDirections(swingDir, tool.HitArcDegrees, tool.HitRayCount))
		{
			var to = from + dir * tool.HitRange;
			query.From = from;
			query.To = to;

			var result = space.IntersectRay(query);
			if (result.Count == 0)
				continue;

			if (!result.TryGetValue("collider", out var col))
				continue;

			var colliderNode = col.As<Node>();
			var hittable = FindToolHittable(colliderNode);
			if (hittable == null)
				continue;

			var hitRoot = hittable.GetHitRoot();

			// Ignore self
			if (hitRoot == attacker)
				continue;

			// Optional target filter
			if (requiredTarget != null && hitRoot != requiredTarget)
				continue;

			if (!result.TryGetValue("position", out var pos))
				continue;

			var hitPoint = pos.AsVector3();
			var hitDir = (hitRoot.GlobalPosition - hitPoint).Normalized();
			var toolResult = tool.UseOn(hittable, hitDir, hitPoint);
			return toolResult;
		}

		return ToolHitResult.None;
	}
}