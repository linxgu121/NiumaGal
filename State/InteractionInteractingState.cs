using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;

namespace NiumaGal.State
{
    public class InteractionInteractingState : StateBase
    {
        private readonly NiumaGalBlackboard _blackboard;

        public InteractionInteractingState(NiumaGalBlackboard blackboard) => _blackboard = blackboard;

        public override void Enter() => _blackboard.SetInteractionState(InteractionState.Interacting);
        public override void LogicUpdate() { }
        public override void Exit() { }
    }
}
