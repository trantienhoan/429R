using Enemies;

public class StateMachine
{
    private IState currentState;

    public void ChangeState(IState newState)
    {
        if (currentState == newState) return;
        currentState?.OnExit();
        currentState = newState;
        currentState.OnEnter();
    }

    public void Tick()
    {
        currentState?.Tick();
    }
}