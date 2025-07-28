namespace Enemies
{
    public class StateMachine
    {
        public IState CurrentState { get; private set; }

        public void ChangeState(IState newState)
        {
            CurrentState?.OnExit();
            CurrentState = newState;
            CurrentState?.OnEnter();
        }

        public void Tick()
        {
            CurrentState?.Tick();
        }
    }
}