using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;

namespace NiumaGal.State
{
    public class ScriptUnitEndedState : StateBase
    {
        private readonly NiumaGalBlackboard _blackboard;

        public ScriptUnitEndedState(NiumaGalBlackboard blackboard) => _blackboard = blackboard;

        public override void Enter()
        {
            _blackboard.SetScriptState(DialogueScriptState.UnitEnded);
            _blackboard.SetLineState(LineState.Completed);
            _blackboard.SetVoiceState(VoiceState.Idle);
        }

        public override void LogicUpdate() { }
        public override void Exit() { }
    }
}