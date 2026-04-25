using NiumaGal.Enum;

namespace NiumaGal.Dialogue.Arbitration
{
    public readonly struct InputRequest
    {
        public readonly InputCommand Command;
        public readonly object Context;

        public InputRequest(InputCommand command, object context = null)
        {
            Command = command;
            Context = context;
        }
    }
}