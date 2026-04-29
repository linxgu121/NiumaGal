using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;

namespace NiumaGal.State
{
    public class ScriptIdleState : StateBase
    {
        private readonly NiumaGalBlackboard _blackboard;

        public ScriptIdleState(NiumaGalBlackboard blackboard) => _blackboard = blackboard;

        public override void Enter()
        {
            _blackboard.SetScriptState(DialogueScriptState.Idle);
            _blackboard.SetLineState(LineState.Idle);
            _blackboard.SetVoiceState(VoiceState.Idle);
        }
        
        public override void LogicUpdate() { }
        public override void Exit() { }
    }
}