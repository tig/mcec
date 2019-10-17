using System;
using WindowsInput.Native;

namespace WindowsInput
{
    /// <summary>
    /// The contract for a service that dispatches <see cref="INPUT"/> messages to the appropriate destination.
    /// </summary>
    public interface IInputMessageDispatcher
    {
#pragma warning disable CS3002, CS3003 // Return type is not CLS-compliant
                              /// <summary>
                              /// Dispatches the specified list of <see cref="INPUT"/> messages in their specified order.
                              /// </summary>
                              /// <param name="inputs">The list of <see cref="INPUT"/> messages to be dispatched.</param>
                              /// <returns>The number of <see cref="INPUT"/> messages that were successfully dispatched.</returns>
        UInt32 DispatchInput(INPUT[] inputs);
#pragma warning restore CS3002 // Return type is not CLS-compliant
    }
}
