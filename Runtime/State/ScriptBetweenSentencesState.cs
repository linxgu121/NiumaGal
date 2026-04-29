using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;

namespace NiumaGal.State
{
    public class ScriptBetweenSentencesState : StateBase
    {
        private readonly NiumaGalBlackboard _blackboard;

        public ScriptBetweenSentencesState(NiumaGalBlackboard blackboard) => _blackboard = blackboard;

        public override void Enter() => _blackboard.SetScriptState(DialogueScriptState.BetweenSentences);
        public override void LogicUpdate() { }
        public override void Exit() { }
    }
}
