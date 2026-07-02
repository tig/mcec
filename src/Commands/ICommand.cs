namespace MCEControl;
// Implements Command Pattern
public interface ICommand {
    /// <summary>
    /// Called to execute the command.
    /// </summary>
    bool Execute();

    /// <summary>
    /// Clones the command (a table prototype) for execution with a fresh reply context.
    /// </summary>
    ICommand Clone(Reply reply);
}
