 namespace NiumaGal.State
{
    public abstract class StateBase
    {
        public abstract void Enter();
        public abstract void LogicUpdate();
        public virtual void PhysicsUpdate()
        {
            
        }
        public abstract void Exit();
    }
}