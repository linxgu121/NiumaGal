using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;

namespace NiumaGal.State
{
    public class ScriptRunningState : StateBase
    {
        private readonly NiumaGalBlackboard _blackboard;

        public ScriptRunningState(NiumaGalBlackboard blackboard) => _blackboard = blackboard;

        public override void Enter()
        {
            _blackboard.SetScriptState(DialogueScriptState.Running);
            /*LineState 由 TypewriterSystem.Start 设置，此处不越界
            _blackboard.SetLineState(LineState.Playing);*/

            var sentence = _blackboard.CurrentDialogue.Sentences[_blackboard.CurrentSentenceIndex];
            _blackboard.CurrentSpeaker = sentence.Speaker;
            _blackboard.CurrentText = sentence.Text;
        }

        public override void LogicUpdate()
        {
            // TypewriterSystem 完成逐字后写入 LineState.Completed
            // 此处检测到即转入等待推进状态
            if (_blackboard.LineState == LineState.Completed)
            {
                OwnerStateMachine?.ChangeState(new ScriptBetweenSentencesState(_blackboard));
            }
        }

        public override void Exit() { }
    }
}