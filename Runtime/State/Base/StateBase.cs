 namespace NiumaGal.State
{
    public abstract class StateBase
    {
        /// <summary>
        /// 所属状态机引用,由状态机在状态切换或初始化时注入
        /// 供状态内部自主转换使用
        /// </summary>
        public StateMachine OwnerStateMachine { get; set; }
        public abstract void Enter();
        public abstract void LogicUpdate();
        public virtual void PhysicsUpdate()
        {
            
        }
        public abstract void Exit();
    }
}