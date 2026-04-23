
namespace NiumaGal.State
{
    public class StateMachine
    {
        public StateBase CurrentState { get; private set;}

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="startingState">初始状态</param>
        public void Initialize(StateBase startingState)
        {
            CurrentState = startingState;
            CurrentState.Enter();
        }
        /// <summary>
        /// 改变状态
        /// </summary>
        /// <param name="newState">改变的状态</param>
        public void ChangeState(StateBase newState)
        {
            CurrentState.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }
    }
}
