using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;

namespace NiumaGal.State
{
    public class InteractionIdleState : StateBase
    {
        private readonly NiumaGalBlackboard _blackboard;

        public InteractionIdleState(NiumaGalBlackboard blackboard) => _blackboard = blackboard;

        public override void Enter() => _blackboard.SetInteractionState(InteractionState.Idle);
        public override void LogicUpdate() { }
        public override void Exit() { }
    }
}
