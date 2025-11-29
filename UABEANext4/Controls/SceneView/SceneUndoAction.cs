using System.Numerics;
using UABEANext4.Logic.Scene;

namespace UABEANext4.Controls.SceneView;

/// <summary>
/// Represents an undoable action in the scene editor.
/// </summary>
public abstract class SceneUndoAction
{
    public abstract void Undo();
    public abstract void Redo();
}

/// <summary>
/// Undo action for moving an object.
/// </summary>
public class MoveObjectAction : SceneUndoAction
{
    public SceneObject Target { get; }
    public Vector3 OldPosition { get; }
    public Vector3 NewPosition { get; }

    public MoveObjectAction(SceneObject target, Vector3 oldPosition, Vector3 newPosition)
    {
        Target = target;
        OldPosition = oldPosition;
        NewPosition = newPosition;
    }

    public override void Undo()
    {
        Target.LocalPosition = OldPosition;
        Target.ComputeWorldMatrix();
        Target.ComputeBounds();
    }

    public override void Redo()
    {
        Target.LocalPosition = NewPosition;
        Target.ComputeWorldMatrix();
        Target.ComputeBounds();
    }
}
