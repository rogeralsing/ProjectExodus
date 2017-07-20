namespace Proto.TestFixtures
{
    public class TestProcess : Process
    {
        protected internal override void SendUserMessage(PID pid, object message)
        {
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
        }
    }
}
